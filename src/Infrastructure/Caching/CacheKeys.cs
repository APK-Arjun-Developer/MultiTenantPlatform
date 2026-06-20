namespace Infrastructure.Caching;

public static class CacheKeys
{
    public const string PermissionCatalogSystem = "permissions:catalog:system";

    public const string PermissionCatalogTenant = "permissions:catalog:tenant";

    public const string PermissionNamesSystem = "permissions:names:system";

    public const string TenantCatalogAll = "tenants:catalog:all";

    public static string RolePermissions(Guid roleId) => $"role-permissions:{roleId:N}";

    public static string TenantDetail(Guid tenantId) => $"tenant:detail:{tenantId:N}";

    public static string Products(Guid tenantId) => $"products:tenant:{tenantId:N}";

    public static string ReportSummary(Guid tenantId) => $"reports:summary:{tenantId:N}";
}
