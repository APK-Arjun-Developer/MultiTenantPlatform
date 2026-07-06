using Infrastructure.Identity.Entities;

namespace Infrastructure.Identity;

public interface IIdentityRoleService
{
    Task<ApplicationRole?> FindRoleByNameAsync(Guid tenantId, string roleName);

    Task<bool> RoleExistsAsync(Guid tenantId, string roleName);

    Task<ApplicationRole> CreateRoleAsync(Guid tenantId, string roleName, string? description = null);

    Task<List<Guid>> ValidatePermissionIdsAsync(IEnumerable<Guid> permissionIds);

    Task AssignPermissionsToRoleByIdsAsync(Guid roleId, IEnumerable<Guid> permissionIds);

    Task SetRolePermissionsByIdsAsync(Guid roleId, IEnumerable<Guid> permissionIds);

    Task AddUserToRoleAsync(Guid userId, Guid roleId);
}
