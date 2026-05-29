namespace Application.Interfaces.Files;

public interface IFileStorageService
{
    Task<StoredFileResult> SaveAsync(
        Guid tenantId,
        Stream content,
        string originalFileName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken = default);

    Task DeletePhysicalAsync(string relativePath, CancellationToken cancellationToken = default);
}

public sealed class StoredFileResult
{
    public required string StoredName { get; init; }

    public required string RelativePath { get; init; }

    public required string StorageType { get; init; }

    public required string Extension { get; init; }

    public required long Size { get; init; }
}
