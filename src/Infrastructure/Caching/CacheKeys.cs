namespace Infrastructure.Caching;

public static class CacheKeys
{
    public const string PermissionCatalogSystem = "permissions:catalog:system";

    public const string PermissionCatalogTenant = "permissions:catalog:tenant";

    public const string PermissionCatalogTenantUser = "permissions:catalog:tenant-user";

    public const string PermissionNamesSystem = "permissions:names:system";

    public const string TenantCatalogAll = "tenants:catalog:all";

    public static string RolePermissions(Guid roleId) => $"role-permissions:{roleId:N}";

    public static string TenantDetail(Guid tenantId) => $"tenant:detail:{tenantId:N}";

    public static string UserStatus(Guid userId) => $"user:status:{userId:N}";

    public static string TenantStatus(Guid tenantId) => $"tenant:status:{tenantId:N}";

}
