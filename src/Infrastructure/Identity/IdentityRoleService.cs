using Application.Common;
using Application.Exceptions;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Identity.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity;

public class IdentityRoleService : IIdentityRoleService
{
    private readonly ApplicationDbContext _context;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public IdentityRoleService(
        ApplicationDbContext context,
        RoleManager<ApplicationRole> roleManager)
    {
        _context = context;
        _roleManager = roleManager;
    }

    // Custom roles are capped at the TenantUser permission ceiling for every caller,
    // including SystemAdmin — roles exist to be assigned to tenant users and must
    // never escalate beyond what a tenant user may hold.
    private async Task RejectNonTenantUserPermissionsAsync(IEnumerable<Guid> permissionIds)
    {
        var ids = permissionIds.ToList();
        if (ids.Count == 0) return;

        var allowed = PermissionNames.TenantUserPermissions.ToHashSet(StringComparer.Ordinal);

        var disallowed = await _context.Permissions
            .Where(p => ids.Contains(p.Id) && !allowed.Contains(p.Name))
            .Select(p => p.Name)
            .ToListAsync();

        if (disallowed.Count > 0)
        {
            throw new ForbiddenException(
                "Custom roles may only include TenantUser-scoped permissions. " +
                $"Disallowed: {string.Join(", ", disallowed)}");
        }
    }

    public async Task<ApplicationRole?> FindRoleByNameAsync(Guid tenantId, string roleName)
    {
        return await _roleManager.Roles
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Name == roleName);
    }

    public async Task<bool> RoleExistsAsync(Guid tenantId, string roleName)
    {
        return await _context.Roles
            .AnyAsync(r => r.TenantId == tenantId && r.Name == roleName);
    }

    public async Task<ApplicationRole> CreateRoleAsync(
        Guid tenantId,
        string roleName,
        string? description = null)
    {
        var existing = await _context.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Name == roleName);

        if (existing != null)
        {
            if (existing.DeletedAt != null)
            {
                existing.DeletedAt = null;
                existing.Description = description;
            }

            return existing;
        }

        var role = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            Name = roleName,
            NormalizedName = roleName.ToUpperInvariant(),
            TenantId = tenantId,
            Scope = tenantId == Guid.Empty ? RoleScope.System : RoleScope.Tenant,
            Description = description,
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };

        _context.Roles.Add(role);

        return role;
    }

    public async Task<List<Guid>> ValidatePermissionIdsAsync(IEnumerable<Guid> permissionIds)
    {
        var ids = permissionIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            throw new InvalidOperationException("At least one permission is required.");
        }

        var existing = await _context.Permissions
            .Where(p => ids.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync();

        var missing = ids.Except(existing).ToList();

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Unknown permission ids: {string.Join(", ", missing)}");
        }

        return ids;
    }

    public async Task AssignPermissionsToRoleByIdsAsync(Guid roleId, IEnumerable<Guid> permissionIds)
    {
        var ids = await ValidatePermissionIdsAsync(permissionIds);

        await RejectNonTenantUserPermissionsAsync(ids);

        var existingSet = (await _context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionId)
            .ToListAsync()).ToHashSet();

        foreach (var permissionId in ids.Where(id => !existingSet.Contains(id)))
        {
            _context.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId
            });
        }
    }

    public async Task SetRolePermissionsByIdsAsync(Guid roleId, IEnumerable<Guid> permissionIds)
    {
        var ids = await ValidatePermissionIdsAsync(permissionIds);

        await RejectNonTenantUserPermissionsAsync(ids);

        var existing = await _context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync();

        _context.RolePermissions.RemoveRange(existing);

        foreach (var permissionId in ids)
        {
            _context.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId
            });
        }
    }

    public async Task AddUserToRoleAsync(Guid userId, Guid roleId)
    {
        var exists = await _context.Set<IdentityUserRole<Guid>>()
            .AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

        if (exists)
        {
            return;
        }

        _context.Set<IdentityUserRole<Guid>>().Add(new IdentityUserRole<Guid>
        {
            UserId = userId,
            RoleId = roleId
        });

        await _context.SaveChangesAsync();
    }

}
