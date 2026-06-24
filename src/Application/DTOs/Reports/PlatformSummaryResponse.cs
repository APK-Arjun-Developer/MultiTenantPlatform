namespace Application.DTOs.Reports;

public class PlatformSummaryResponse
{
    public int TenantCount { get; set; }

    public int TotalUserCount { get; set; }

    public int TotalProductCount { get; set; }

    public int TotalActivityLogCount { get; set; }

    public DateTime GeneratedAtUtc { get; set; }
}
