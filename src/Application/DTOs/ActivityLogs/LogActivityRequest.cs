namespace Application.DTOs.ActivityLogs;

public class LogActivityRequest
{
    public required Guid UserId { get; init; }

    public Guid? TenantId { get; init; }

    public required string Action { get; init; }

    public required string Module { get; init; }

    public required string Description { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}
