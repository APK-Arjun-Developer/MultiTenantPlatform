using Application.Common;
using Application.DTOs.ActivityLogs;
using Application.DTOs.Files;
using Application.Exceptions;
using Application.Interfaces.ActivityLogs;
using Application.Interfaces.Files;
using Application.Interfaces.Tenant;
using Domain.Entities;
using Infrastructure.Common;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Files;

public class FileService : TenantScopedService, IFileService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly IActivityLogService _activityLogService;
    private readonly FileStorageSettings _settings;
    private readonly ILogger<FileService> _logger;

    public FileService(
        ApplicationDbContext context,
        IFileStorageService fileStorageService,
        ICurrentTenantService currentTenantService,
        IActivityLogService activityLogService,
        IOptions<FileStorageSettings> settings,
        ILogger<FileService> logger)
        : base(currentTenantService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _activityLogService = activityLogService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FileResponse>> GetAllAsync()
    {
        var tenantId = RequireTenantId();

        var files = await _context.Files
            .AsNoTracking()
            .Where(f => f.TenantId == tenantId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        return files.Select(MapToResponse).ToList();
    }

    public async Task<FileResponse> GetMetadataAsync(Guid id)
    {
        return MapToResponse(await FindFileAsync(id));
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> DownloadAsync(Guid id)
    {
        var file = await FindFileAsync(id);
        var stream = await _fileStorageService.OpenReadAsync(file.RelativePath);

        return (stream, file.ContentType, file.OriginalName);
    }

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp",
        ".pdf", ".txt", ".csv", ".json", ".xml",
        ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".zip", ".mp4", ".mp3",
    };

    // Returns Guid.Empty for SystemAdmin (their files are stored under the empty tenant).
    private Guid GetEffectiveTenantId() =>
        IsSystemAdmin() ? Guid.Empty : RequireTenantId();

    public async Task<FileResponse> UploadAsync(IFormFile file)
    {
        var tenantId = GetEffectiveTenantId();

        if (file.Length <= 0)
            throw new InvalidOperationException("File is empty.");

        if (file.Length > _settings.MaxFileSizeBytes)
            throw new InvalidOperationException(
                $"File exceeds maximum size of {_settings.MaxFileSizeBytes / 1024 / 1024} MB.");

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidOperationException($"File type '{extension}' is not allowed.");

        var effectiveContentType = file.ContentType ?? "application/octet-stream";
        var effectiveFileName = file.FileName;
        MemoryStream? processedStream = null;

        await using var inputStream = file.OpenReadStream();

        if (_settings.ImageProcessing.Enabled && ImageProcessor.IsProcessable(effectiveContentType))
        {
            processedStream = await ImageProcessor.ProcessAsync(inputStream, _settings.ImageProcessing);
            effectiveContentType = "image/webp";
            effectiveFileName = Path.GetFileNameWithoutExtension(file.FileName) + ".webp";
        }

        try
        {
            Stream streamToSave = processedStream ?? inputStream;
            var stored = await _fileStorageService.SaveAsync(
                tenantId,
                streamToSave,
                effectiveFileName,
                effectiveContentType);

            var entity = new FileEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OriginalName = file.FileName,
                StoredName = stored.StoredName,
                RelativePath = stored.RelativePath,
                StorageType = stored.StorageType,
                ContentType = effectiveContentType,
                Extension = stored.Extension,
                Size = stored.Size,
                CreatedAt = DateTime.UtcNow,
            };

            _context.Files.Add(entity);
            await _context.SaveChangesAsync();

            await LogActivityAsync(ActivityActions.Files.Uploaded, $"Uploaded file '{entity.OriginalName}'.");

            return MapToResponse(entity);
        }
        finally
        {
            if (processedStream is not null)
                await processedStream.DisposeAsync();
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        var file = await FindFileAsync(id);

        file.MarkDeleted();

        await _context.Users
            .Where(u => u.ProfileFileId == file.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.ProfileFileId, (Guid?)null));

        await _context.Tenants
            .Where(t => t.ProfileFileId == file.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ProfileFileId, (Guid?)null));

        await _context.SaveChangesAsync();

        try
        {
            await _fileStorageService.DeletePhysicalAsync(file.RelativePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Physical file deletion failed for '{RelativePath}'. Record is soft-deleted; orphaned file requires manual cleanup.",
                file.RelativePath);
        }

        await LogActivityAsync(ActivityActions.Files.Deleted, $"Deleted file '{file.OriginalName}'.");
    }

    private async Task<FileEntity> FindFileAsync(Guid id)
    {
        var tenantId = GetEffectiveTenantId();
        return await _context.Files
            .FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenantId)
            ?? throw new NotFoundException($"File '{id}' was not found.");
    }

    private async Task LogActivityAsync(string action, string description)
    {
        await _activityLogService.LogAsync(new LogActivityRequest
        {
            UserId = RequireUserId(),
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
