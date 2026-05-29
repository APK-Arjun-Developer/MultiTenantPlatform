namespace Application.DTOs.Files;

public class FileResponse
{
    public Guid Id { get; set; }

    public string OriginalName { get; set; } = default!;

    public string ContentType { get; set; } = default!;

    public string Extension { get; set; } = default!;

    public long Size { get; set; }

    public string StorageType { get; set; } = default!;

    public DateTime CreatedAt { get; set; }
}
