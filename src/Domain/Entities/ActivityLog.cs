using Domain.Common;

namespace Domain.Entities;

public class ActivityLog : BaseEntity
{
    public Guid UserId { get; set; }

    public string Action { get; set; } = default!;

    public string Module { get; set; } = default!;

    public string Description { get; set; } = default!;

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }
}