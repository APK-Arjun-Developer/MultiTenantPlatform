using Infrastructure.Identity.Entities;

namespace Infrastructure.Identity;

public interface IIdentityRoleService
{
    Task<ApplicationRole?> FindRoleByNameAsync(Guid tenantId, string roleName);

    Task<bool> RoleExistsAsync(Guid tenantId, string roleName);

    Task<ApplicationRole> CreateRoleAsync(Guid tenantId, string roleName, string? description = null);

    Task<List<Guid>> ValidatePermissionIdsAsync(IEnumerable<Guid> permissionIds);

    Task<List<string>> ValidatePermissionNamesAsync(IEnumerable<string> permissionNames);

    Task AssignPermissionsToRoleByIdsAsync(Guid roleId, IEnumerable<Guid> permissionIds);

    Task SetRolePermissionsByIdsAsync(Guid roleId, IEnumerable<Guid> permissionIds);

    Task AssignPermissionsToRoleAsync(Guid roleId, IEnumerable<string> permissionNames);

    Task AddUserToRoleAsync(Guid userId, Guid roleId);

    Task<ApplicationRole> EnsureTenantAdminRoleAsync(Guid tenantId, CancellationToken ct = default);

    Task<ApplicationRole> EnsureTenantUserRoleAsync(Guid tenantId, CancellationToken ct = default);
}
