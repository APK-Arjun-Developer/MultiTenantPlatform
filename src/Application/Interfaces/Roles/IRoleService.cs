using Application.DTOs.Roles;

namespace Application.Interfaces.Roles;

public interface IRoleService
{
    Task<IReadOnlyList<RoleResponse>> GetRolesAsync();

    Task<RoleResponse> GetCurrentRoleAsync();

    Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request);

    Task<RoleResponse> UpdateRoleAsync(UpdateRoleRequest request);

    Task DeleteRoleAsync(DeleteRoleRequest request);
}
