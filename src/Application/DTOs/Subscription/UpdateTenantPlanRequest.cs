using Domain.Enums;

namespace Application.DTOs.Subscription;

public class UpdateTenantPlanRequest
{
    public Guid TenantId { get; set; }

    public PlanType PlanType { get; set; }
}
