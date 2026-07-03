using Application.Interfaces.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public class DashboardController : ApiControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Returns dashboard statistics scoped to the caller.
    /// SystemAdmin: total tenants, tenant admins, and tenant users platform-wide.
    /// TenantAdmin: tenant-user count for their own tenant only.
    /// </summary>
    [HttpGet("stats")]
    [Authorize(Policy = "TenantAdminOrAbove")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var response = await _dashboardService.GetStatsAsync(cancellationToken);

        return OkEnvelope(response, "Dashboard stats retrieved.");
    }
}
