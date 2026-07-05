namespace Application.DTOs.ActivityLogs;

public class ActivityLogQueryParams
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public Guid? UserId { get; set; }

    public string? Module { get; set; }

    public string? Action { get; set; }

    public DateTime? DateFrom { get; set; }

    public DateTime? DateTo { get; set; }
}
