using Application.Common;
using Application.DTOs.ActivityLogs;
using Application.DTOs.Files;
using Application.Interfaces.ActivityLogs;
using Application.Interfaces.Files;
using Application.Interfaces.Tenant;
using Domain.Entities;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Files;

public class FileService : IFileService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IActivityLogService _activityLogService;
    private readonly FileStorageSettings _settings;

    public FileService(
        ApplicationDbContext context,
        IFileStorageService fileStorageService,
        ICurrentTenantService currentTenantService,
        IActivityLogService activityLogService,
        IOptions<FileStorageSettings> settings)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _currentTenantService = currentTenantService;
        _activityLogService = activityLogService;
        _settings = settings.Value;
    }

    public async Task<IReadOnlyList<FileResponse>> GetAllAsync()
    {
        RequireTenantId();

        var files = await _context.Files
            .AsNoTracking()
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        return files.Select(MapToResponse).ToList();
    }

    public async Task<FileResponse> GetMetadataAsync(Guid id)
    {
        var file = await FindFileAsync(id);

        return MapToResponse(file);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> DownloadAsync(Guid id)
    {
        var file = await FindFileAsync(id);
        var stream = await _fileStorageService.OpenReadAsync(file.RelativePath);

        return (stream, file.ContentType, file.OriginalName);
    }

    public async Task<FileResponse> UploadAsync(IFormFile file)
    {
        var tenantId = RequireTenantId();

        if (file.Length <= 0)
        {
            throw new InvalidOperationException("File is empty.");
        }

        if (file.Length > _settings.MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"File exceeds maximum size of {_settings.MaxFileSizeBytes} bytes.");
        }

        await using var stream = file.OpenReadStream();

        var stored = await _fileStorageService.SaveAsync(
            tenantId,
            stream,
            file.FileName,
            file.ContentType ?? "application/octet-stream");

        var entity = new FileEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OriginalName = file.FileName,
            StoredName = stored.StoredName,
            RelativePath = stored.RelativePath,
            StorageType = stored.StorageType,
            ContentType = file.ContentType ?? "application/octet-stream",
            Extension = stored.Extension,
            Size = stored.Size,
            CreatedAt = DateTime.UtcNow,
        };

        _context.Files.Add(entity);
        await _context.SaveChangesAsync();

        await LogCurrentUserActivityAsync(
            ActivityActions.Files.Uploaded,
            $"Uploaded file '{entity.OriginalName}'.");

        return MapToResponse(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var file = await FindFileAsync(id);

        file.MarkDeleted();

        await _context.Users
            .Where(u => u.ProfileFileId == file.Id)
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(u => u.ProfileFileId, (Guid?)null));

        await _context.SaveChangesAsync();

        try
        {
            await _fileStorageService.DeletePhysicalAsync(file.RelativePath);
        }
        catch (Exception)
        {
            // Soft-deleted in DB even if physical cleanup fails.
        }

        await LogCurrentUserActivityAsync(
            ActivityActions.Files.Deleted,
            $"Deleted file '{file.OriginalName}'.");
    }

    private async Task<FileEntity> FindFileAsync(Guid id)
    {
        var file = await _context.Files.FirstOrDefaultAsync(f => f.Id == id);

        if (file == null)
        {
            throw new InvalidOperationException("File not found.");
        }

        return file;
    }

    private Guid RequireTenantId()
    {
        var tenantId = _currentTenantService.TenantId ?? Guid.Empty;

        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Files can only be managed within a tenant context.");
        }

        return tenantId;
    }

    private async Task LogCurrentUserActivityAsync(string action, string description)
    {
        var userId = _currentTenantService.UserId
            ?? throw new InvalidOperationException("User context is required.");

        await _activityLogService.LogAsync(new LogActivityRequest
        {
            UserId = userId,
            Action = action,
            Module = ActivityModules.Files,
            Description = description,
        });
    }

    private static FileResponse MapToResponse(FileEntity file) =>
        new()
        {
            Id = file.Id,
            OriginalName = file.OriginalName,
            ContentType = file.ContentType,
            Extension = file.Extension,
            Size = file.Size,
            StorageType = file.StorageType,
            CreatedAt = file.CreatedAt,
        };
}
