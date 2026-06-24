using Api.Attributes;
using Application.Common;
using Application.Interfaces.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
[Authorize]
public class ReportsController : ApiControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("summary")]
    [HasPermission(PermissionNames.ReportsView)]
    public async Task<IActionResult> GetSummary()
    {
        var response = await _reportService.GetSummaryAsync();

        return OkEnvelope(response, "Report summary generated.");
    }

    [HttpGet("platform-summary")]
    [HasPermission(PermissionNames.TenantsView)]
    public async Task<IActionResult> GetPlatformSummary()
    {
        var response = await _reportService.GetPlatformSummaryAsync();

        return OkEnvelope(response, "Platform summary generated.");
    }

    [HttpGet("export")]
    [HasPermission(PermissionNames.ReportsExport)]
    public async Task<IActionResult> Export()
    {
        var csv = await _reportService.ExportSummaryCsvAsync();

        return File(
            csv,
            "text/csv",
            $"tenant-report-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }

    [HttpGet("platform-export")]
    [HasPermission(PermissionNames.TenantsView)]
    public async Task<IActionResult> ExportPlatform()
    {
        var csv = await _reportService.ExportPlatformCsvAsync();

        return File(
            csv,
            "text/csv",
            $"platform-report-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
    }
}
