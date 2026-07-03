namespace Application.DTOs.Dashboard;

public class DashboardStatsResponse
{
    /// <summary>Total active (non-deleted) tenants. SystemAdmin only — null for tenant callers.</summary>
    public int? TotalTenants { get; set; }

    /// <summary>Total tenant admins across all tenants. SystemAdmin only — null for tenant callers.</summary>
    public int? TotalTenantAdmins { get; set; }

    /// <summary>Total tenant users. Platform-wide for SystemAdmin; scoped to own tenant for TenantAdmin.</summary>
    public int TotalTenantUsers { get; set; }
}
