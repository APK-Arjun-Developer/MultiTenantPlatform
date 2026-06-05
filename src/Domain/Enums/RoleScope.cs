namespace Domain.Enums;

public enum RoleScope
{
    /// <summary>Platform-wide role (TenantId = Guid.Empty). Only SuperAdmin is system-scoped.</summary>
    System = 1,

    /// <summary>Belongs to a specific tenant. Tenant Admins and all custom roles.</summary>
    Tenant = 2,
}
