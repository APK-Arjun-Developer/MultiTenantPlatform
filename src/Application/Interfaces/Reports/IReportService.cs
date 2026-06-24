using Application.DTOs.Reports;

namespace Application.Interfaces.Reports;

public interface IReportService
{
    Task<ReportSummaryResponse> GetSummaryAsync();

    Task<PlatformSummaryResponse> GetPlatformSummaryAsync();

    Task<byte[]> ExportSummaryCsvAsync();

    Task<byte[]> ExportPlatformCsvAsync();
}
