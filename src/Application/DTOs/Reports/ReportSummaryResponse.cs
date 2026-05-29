namespace Application.DTOs.Reports;

public class ReportSummaryResponse
{
    public int UserCount { get; set; }

    public int RoleCount { get; set; }

    public int ProductCount { get; set; }

    public int ActivityLogCount { get; set; }

    public DateTime GeneratedAtUtc { get; set; }
}
