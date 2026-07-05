namespace Application.DTOs.Subscription;

public class SubscriptionPlanDto
{
    public string PlanType { get; set; } = default!;

    public string Name { get; set; } = default!;

    public PlanFeatureSummary Features { get; set; } = default!;
}

public class PlanFeatureSummary
{
    public int MaxUsers { get; set; }

    public int MaxStorageMb { get; set; }

    public bool CanAccessReports { get; set; }

    public bool CanAccessAdvancedRoles { get; set; }
}
