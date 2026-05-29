using Api.Attributes;
using Application.Common;
using Application.Interfaces.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/files")]
[Authorize]
public class FilesController : ApiControllerBase
{
    private readonly IFileService _fileService;

    public FilesController(IFileService fileService)
    {
        _fileService = fileService;
    }

    [HttpGet]
    [HasPermission(PermissionNames.FilesView)]
    public async Task<IActionResult> GetAll()
    {
        var response = await _fileService.GetAllAsync();

        return OkEnvelope(response, "Files retrieved.");
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionNames.FilesView)]
    public async Task<IActionResult> GetMetadata(Guid id)
    {
        var response = await _fileService.GetMetadataAsync(id);

        return OkEnvelope(response, "File metadata retrieved.");
    }

    [HttpGet("{id:guid}/download")]
    [HasPermission(PermissionNames.FilesView)]
    public async Task<IActionResult> Download(Guid id)
    {
        var (stream, contentType, fileName) = await _fileService.DownloadAsync(id);

        return File(stream, contentType, fileName);
    }

    [HttpPost]
    [HasPermission(PermissionNames.FilesUpload)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        var response = await _fileService.UploadAsync(file);

        return OkEnvelope(response, "File uploaded.");
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionNames.FilesDelete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _fileService.DeleteAsync(id);

        return OkEnvelope("File deleted.");
    }
}
