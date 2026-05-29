using Application.DTOs.Files;
using Microsoft.AspNetCore.Http;

namespace Application.Interfaces.Files;

public interface IFileService
{
    Task<IReadOnlyList<FileResponse>> GetAllAsync();

    Task<FileResponse> GetMetadataAsync(Guid id);

    Task<(Stream Stream, string ContentType, string FileName)> DownloadAsync(Guid id);

    Task<FileResponse> UploadAsync(IFormFile file);

    Task DeleteAsync(Guid id);
}
