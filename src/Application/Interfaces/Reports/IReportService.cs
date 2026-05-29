using Application.DTOs.Reports;

namespace Application.Interfaces.Reports;

public interface IReportService
{
    Task<ReportSummaryResponse> GetSummaryAsync();

    Task<byte[]> ExportSummaryCsvAsync();
}
