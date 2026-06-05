using Application.DTOs.Common;
using Application.DTOs.Roles;

namespace Application.Interfaces.Roles;

public interface IRoleService
{
    Task<PagedResponse<RoleResponse>> GetRolesAsync(int page, int pageSize, string? search = null);

    Task<RoleResponse> GetByNameAsync(string name);

    Task<RoleResponse> GetCurrentRoleAsync();

    Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request);

    Task<RoleResponse> UpdateRoleAsync(UpdateRoleRequest request);

    Task DeleteRoleAsync(DeleteRoleRequest request);
}
