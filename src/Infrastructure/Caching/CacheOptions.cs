namespace Infrastructure.Caching;

public class CacheOptions
{
    public const string SectionName = "Caching";

    /// <summary>Permission catalog for onboarding / role UI.</summary>
    public int PermissionCatalogMinutes { get; set; } = 10;

    /// <summary>Role id → permission names (authorization + role API).</summary>
    public int RolePermissionsMinutes { get; set; } = 5;

    /// <summary>System-admin tenant list.</summary>
    public int TenantCatalogMinutes { get; set; } = 5;

    /// <summary>Single tenant metadata (current tenant, admin tenant lookup).</summary>
    public int TenantDetailMinutes { get; set; } = 5;

    /// <summary>Tenant product catalog list.</summary>
    public int ProductListMinutes { get; set; } = 3;

    /// <summary>Aggregated report counts (short TTL; invalidated on writes).</summary>
    public int ReportSummaryMinutes { get; set; } = 2;
}
