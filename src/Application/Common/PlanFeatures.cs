using Domain.Enums;

namespace Application.Common;

public record PlanFeatureSet(
    int MaxUsers,
    int MaxStorageMb,
    bool CanAccessReports,
    bool CanAccessAdvancedRoles);

public static class PlanFeatures
{
    public static readonly PlanFeatureSet Free = new(
        MaxUsers: 10,
        MaxStorageMb: 500,
        CanAccessReports: false,
        CanAccessAdvancedRoles: false);

    public static readonly PlanFeatureSet Pro = new(
        MaxUsers: -1,
        MaxStorageMb: 10240,
        CanAccessReports: true,
        CanAccessAdvancedRoles: true);

    public static PlanFeatureSet Get(PlanType plan) => plan switch
    {
        PlanType.Pro => Pro,
        _ => Free,
    };

    public static string GetName(PlanType plan) => plan switch
    {
        PlanType.Pro => "Pro",
        _ => "Free",
    };
}
