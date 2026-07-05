using Application.DTOs.Subscription;

namespace Application.Interfaces.Subscription;

public interface ISubscriptionService
{
    IReadOnlyList<SubscriptionPlanDto> GetPlans();

    Task<TenantPlanResponse> UpdateTenantPlanAsync(UpdateTenantPlanRequest request, CancellationToken cancellationToken = default);
}
