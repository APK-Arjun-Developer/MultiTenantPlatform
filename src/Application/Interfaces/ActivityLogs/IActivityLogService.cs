using Application.DTOs.ActivityLogs;
using Application.DTOs.Common;

namespace Application.Interfaces.ActivityLogs;

public interface IActivityLogService
{
    Task LogAsync(LogActivityRequest request, CancellationToken cancellationToken = default);

    Task<PagedResponse<ActivityLogResponse>> GetLogsAsync(ActivityLogQueryParams queryParams, CancellationToken cancellationToken = default);
}
