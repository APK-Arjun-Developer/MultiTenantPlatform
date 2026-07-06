namespace Application.DTOs.ActivityLogs;

public class ActivityLogResponse
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string? TenantName { get; set; }

    public Guid UserId { get; set; }

    public string UserDisplayName { get; set; } = default!;

    public string UserEmail { get; set; } = default!;

    public string Action { get; set; } = default!;

    public string Module { get; set; } = default!;

    public string Description { get; set; } = default!;

    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; }
}
