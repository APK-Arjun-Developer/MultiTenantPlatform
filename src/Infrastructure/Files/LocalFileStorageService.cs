using Application.Interfaces.Files;
using Microsoft.Extensions.Options;

namespace Infrastructure.Files;

public class LocalFileStorageService : IFileStorageService
{
    public const string StorageTypeName = "Local";

    private readonly FileStorageSettings _settings;
    private readonly string _rootPath;

    public LocalFileStorageService(IOptions<FileStorageSettings> settings)
    {
        _settings = settings.Value;
        _rootPath = Path.IsPathRooted(_settings.BasePath)
            ? _settings.BasePath
            : Path.Combine(Directory.GetCurrentDirectory(), _settings.BasePath);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<StoredFileResult> SaveAsync(
        Guid tenantId,
        Stream content,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(originalFileName);
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var relativePath = Path.Combine(tenantId.ToString("N"), storedName);
        var absolutePath = GetAbsolutePath(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using var fileStream = new FileStream(
            absolutePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);

        await content.CopyToAsync(fileStream, cancellationToken);

        var size = fileStream.Length;

        return new StoredFileResult
        {
            StoredName = storedName,
            RelativePath = NormalizeRelativePath(relativePath),
            StorageType = StorageTypeName,
            Extension = extension,
            Size = size,
        };
    }

    public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var absolutePath = GetAbsolutePath(relativePath);

        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("Stored file was not found on disk.", absolutePath);
        }

        Stream stream = new FileStream(
            absolutePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        return Task.FromResult(stream);
    }

    public Task DeletePhysicalAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var absolutePath = GetAbsolutePath(relativePath);

        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        return Task.CompletedTask;
    }

    private string GetAbsolutePath(string relativePath)
    {
        var combined = Path.GetFullPath(Path.Combine(_rootPath, relativePath));

        if (!combined.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid file path.");
        }

        return combined;
    }

    private static string NormalizeRelativePath(string relativePath) =>
        relativePath.Replace('\\', '/');
}
