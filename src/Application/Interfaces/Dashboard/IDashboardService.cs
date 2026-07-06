using Application.DTOs.Dashboard;

namespace Application.Interfaces.Dashboard;

public interface IDashboardService
{
    Task<DashboardStatsResponse> GetStatsAsync(CancellationToken cancellationToken = default);
}
