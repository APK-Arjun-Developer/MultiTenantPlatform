using Domain.Common;

namespace Domain.Entities;

public class FileEntity : BaseEntity
{
    public string OriginalName { get; set; } = default!;

    public string StoredName { get; set; } = default!;

    public string RelativePath { get; set; } = default!;

    public string StorageType { get; set; } = default!;

    public string ContentType { get; set; } = default!;

    public string Extension { get; set; } = default!;

    public long Size { get; set; }
}