using Application.DTOs.ActivityLogs;

namespace Application.Interfaces.ActivityLogs;

public interface IActivityLogService
{
    Task LogAsync(LogActivityRequest request, CancellationToken cancellationToken = default);
}
