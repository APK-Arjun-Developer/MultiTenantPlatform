namespace Application.DTOs.Subscription;

public class TenantPlanResponse
{
    public Guid TenantId { get; set; }

    public string TenantName { get; set; } = default!;

    public string PlanType { get; set; } = default!;

    public string PlanName { get; set; } = default!;

    public PlanFeatureSummary Features { get; set; } = default!;
}
