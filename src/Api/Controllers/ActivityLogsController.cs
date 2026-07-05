using Api.Attributes;
using Application.Common;
using Application.DTOs.ActivityLogs;
using Application.Interfaces.ActivityLogs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/activity-logs")]
[Authorize]
public class ActivityLogsController : ApiControllerBase
{
    private readonly IActivityLogService _activityLogService;

    public ActivityLogsController(IActivityLogService activityLogService)
    {
        _activityLogService = activityLogService;
    }

    [HttpGet]
    [HasPermission(PermissionNames.AuditLogsView)]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? module = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new ActivityLogQueryParams
        {
            Page = page,
            PageSize = pageSize,
            UserId = userId,
            Module = module,
            Action = action,
            DateFrom = dateFrom,
            DateTo = dateTo,
        };

        var response = await _activityLogService.GetLogsAsync(queryParams, cancellationToken);

        return OkEnvelope(response, "Activity logs retrieved.");
    }
}
