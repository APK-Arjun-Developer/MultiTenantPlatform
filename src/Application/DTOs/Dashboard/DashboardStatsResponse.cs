namespace Application.DTOs.Dashboard;

public class DashboardStatsResponse
{
    /// <summary>Total active (non-deleted) tenants. SystemAdmin only — null for tenant callers.</summary>
    public int? TotalTenants { get; set; }

    /// <summary>Total tenant admins across all tenants. SystemAdmin only — null for tenant callers.</summary>
    public int? TotalTenantAdmins { get; set; }

    /// <summary>Total tenant users. Platform-wide for SystemAdmin; scoped to own tenant for TenantAdmin.</summary>
    public int TotalTenantUsers { get; set; }

    /// <summary>Total custom roles in own tenant. TenantAdmin only — null for other callers.</summary>
    public int? TotalRoles { get; set; }

    /// <summary>Total pending user invitations in own tenant. TenantAdmin only — null for other callers.</summary>
    public int? TotalPendingInvitations { get; set; }

    // ── SystemAdmin chart data ──────────────────────────────────────────────

    /// <summary>Tenants on the Free plan. SystemAdmin only.</summary>
    public int? FreePlanTenants { get; set; }

    /// <summary>Tenants on the Pro plan. SystemAdmin only.</summary>
    public int? ProPlanTenants { get; set; }

    // ── TenantAdmin chart data ──────────────────────────────────────────────

    /// <summary>Accepted invitations in own tenant. TenantAdmin only.</summary>
    public int? AcceptedInvitations { get; set; }

    /// <summary>Expired invitations in own tenant. TenantAdmin only.</summary>
    public int? ExpiredInvitations { get; set; }

    /// <summary>Revoked invitations in own tenant. TenantAdmin only.</summary>
    public int? RevokedInvitations { get; set; }
}
