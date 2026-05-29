using Application.Common;
using Application.DTOs.Roles;
using Application.Interfaces.Roles;
using Application.Interfaces.Tenant;
using Infrastructure.Identity;
using Infrastructure.Identity.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Roles;

public class RoleService : IRoleService
{
    private readonly ApplicationDbContext _context;

    private readonly RoleManager<ApplicationRole> _roleManager;

    private readonly ICurrentTenantService _currentTenantService;

    public RoleService(
        ApplicationDbContext context,
        RoleManager<ApplicationRole> roleManager,
        ICurrentTenantService currentTenantService)
    {
        _context = context;
        _roleManager = roleManager;
        _currentTenantService = currentTenantService;
    }

    public async Task<IReadOnlyList<RoleResponse>> GetRolesAsync()
    {
        var tenantId = RequireTenantId();

        return await MapRolesAsync(
            _roleManager.Roles.Where(r => r.TenantId == tenantId));
    }

    public async Task<RoleResponse> GetCurrentRoleAsync()
    {
        var roleId = RequireRoleId();
        var tenantId = RequireTenantId();

        var role = await _roleManager.Roles
            .FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == tenantId);

        if (role == null)
        {
            throw new InvalidOperationException("Role not found.");
        }

        return await MapRoleAsync(role);
    }

    public async Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request)
    {
        var tenantId = RequireTenantId();

        if (request.Name == RoleNames.SuperAdmin)
        {
            throw new InvalidOperationException("Cannot create the SuperAdmin role.");
        }

        var exists = await IdentityRoleHelper.RoleExistsAsync(
            _context,
            tenantId,
            request.Name);

        if (exists)
        {
            throw new InvalidOperationException(
                $"Role '{request.Name}' already exists for this tenant.");
        }

        var role = await IdentityRoleHelper.CreateRoleAsync(
            _context,
            tenantId,
            request.Name,
            request.Description);

        await IdentityRoleHelper.AssignPermissionsToRoleByIdsAsync(
            _context,
            role.Id,
            request.Permissions);

        await _context.SaveChangesAsync();

        return await MapRoleAsync(role);
    }

    public async Task<RoleResponse> UpdateRoleAsync(UpdateRoleRequest request)
    {
        var tenantId = RequireTenantId();

        var role = await _roleManager.Roles
            .FirstOrDefaultAsync(r =>
                r.TenantId == tenantId &&
                r.Name == request.Name);

        if (role == null)
        {
            throw new InvalidOperationException("Role not found.");
        }

        if (role.Name == RoleNames.SuperAdmin)
        {
            throw new InvalidOperationException("Cannot modify the SuperAdmin role.");
        }

        role.Description = request.Description;

        await IdentityRoleHelper.SetRolePermissionsByIdsAsync(
            _context,
            role.Id,
            request.Permissions);

        await _context.SaveChangesAsync();

        return await MapRoleAsync(role);
    }

    public async Task DeleteRoleAsync(DeleteRoleRequest request)
    {
        var tenantId = RequireTenantId();

        var role = await _roleManager.Roles
            .FirstOrDefaultAsync(r =>
                r.TenantId == tenantId &&
                r.Name == request.Name);

        if (role == null)
        {
            throw new InvalidOperationException("Role not found.");
        }

        if (role.Name == RoleNames.SuperAdmin)
        {
            throw new InvalidOperationException("Cannot delete the SuperAdmin role.");
        }

        var assignedUsers = await _context.Set<IdentityUserRole<Guid>>()
            .AnyAsync(ur => ur.RoleId == role.Id);

        if (assignedUsers)
        {
            throw new InvalidOperationException(
                "Cannot delete a role that is assigned to users.");
        }

        role.DeletedAt = DateTime.UtcNow;
        var result = await _roleManager.UpdateAsync(role);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    private Guid RequireTenantId()
    {
        var tenantId = _currentTenantService.TenantId ?? Guid.Empty;

        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Tenant context is required. Ensure tenant_id is present in the JWT.");
        }

        return tenantId;
    }

    private Guid RequireRoleId()
    {
        if (!_currentTenantService.RoleId.HasValue)
        {
            throw new InvalidOperationException(
                "Role context is required. Ensure role_id is present in the JWT.");
        }

        return _currentTenantService.RoleId.Value;
    }

    private async Task<IReadOnlyList<RoleResponse>> MapRolesAsync(
        IQueryable<ApplicationRole> query)
    {
        var roles = await query.OrderBy(r => r.Name).ToListAsync();
        var results = new List<RoleResponse>();

        foreach (var role in roles)
        {
            results.Add(await MapRoleAsync(role));
        }

        return results;
    }

    private async Task<RoleResponse> MapRoleAsync(ApplicationRole role)
    {
        var permissionRows = await _context.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .Join(
                _context.Permissions,
                rp => rp.PermissionId,
                p => p.Id,
                (_, p) => new { p.Id, p.Name })
            .OrderBy(p => p.Name)
            .ToListAsync();

        return new RoleResponse
        {
            Id = role.Id,
            Name = role.Name!,
            Description = role.Description,
            TenantId = role.TenantId,
            PermissionIds = permissionRows.Select(p => p.Id).ToList(),
            PermissionNames = permissionRows.Select(p => p.Name).ToList()
        };
    }
}
