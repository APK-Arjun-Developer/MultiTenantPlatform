using Domain.Common;

namespace Domain.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; set; } = default!;

    public string Slug { get; set; } = default!;

    public bool IsActive { get; set; } = true;

    public Guid? ProfileFileId { get; set; }

    public FileEntity? ProfileFile { get; set; }
}