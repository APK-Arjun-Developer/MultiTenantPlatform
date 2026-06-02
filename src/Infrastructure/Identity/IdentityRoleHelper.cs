using Infrastructure.Identity.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity;

/// <summary>
/// Replaced by <see cref="IIdentityRoleService"/> / <see cref="IdentityRoleService"/>.
/// Retained for reference only — do not add new usages.
/// </summary>
[Obsolete("Use IIdentityRoleService instead. This static class will be removed in a future version.")]
public static class IdentityRoleHelper
{
    public static async Task<ApplicationRole?> FindRoleByNameAsync(
        RoleManager<ApplicationRole> roleManager,
        Guid tenantId,
        string roleName)
    {
        return await roleManager.Roles
            .FirstOrDefaultAsync(r =>
                r.TenantId == tenantId &&
                r.Name == roleName);
    }

    public static async Task<bool> RoleExistsAsync(
        RoleManager<ApplicationRole> roleManager,
        Guid tenantId,
        string roleName)
    {
        return await FindRoleByNameAsync(roleManager, tenantId, roleName) != null;
    }

    public static async Task<bool> RoleExistsAsync(
        ApplicationDbContext context,
        Guid tenantId,
        string roleName)
    {
        return await context.Roles
            .AnyAsync(r => r.TenantId == tenantId && r.Name == roleName);
    }

    public static async Task<ApplicationRole> CreateRoleAsync(
        ApplicationDbContext context,
        Guid tenantId,
        string roleName,
        string? description = null)
    {
        var existing = await context.Roles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r =>
                r.TenantId == tenantId &&
                r.Name == roleName);

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
            Description = description,
            ConcurrencyStamp = Guid.NewGuid().ToString()
        };

        context.Roles.Add(role);

        return role;
    }

    public static async Task<List<string>> ValidatePermissionNamesAsync(
        ApplicationDbContext context,
        IEnumerable<string> permissionNames)
    {
        var names = permissionNames.Distinct(StringComparer.Ordinal).ToList();

        if (names.Count == 0)
        {
            throw new InvalidOperationException("At least one permission is required.");
        }

        var existing = await context.Permissions
            .Where(p => names.Contains(p.Name))
            .Select(p => p.Name)
            .ToListAsync();

        var missing = names.Except(existing, StringComparer.Ordinal).ToList();

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Unknown permissions: {string.Join(", ", missing)}");
        }

        return names;
    }

    public static async Task<List<Guid>> ValidatePermissionIdsAsync(
        ApplicationDbContext context,
        IEnumerable<Guid> permissionIds)
    {
        var ids = permissionIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            throw new InvalidOperationException("At least one permission is required.");
        }

        var existing = await context.Permissions
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

    public static async Task AssignPermissionsToRoleByIdsAsync(
        ApplicationDbContext context,
        Guid roleId,
        IEnumerable<Guid> permissionIds)
    {
        var ids = await ValidatePermissionIdsAsync(context, permissionIds);

        var existingPermissionIds = await context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionId)
            .ToListAsync();

        var existingSet = existingPermissionIds.ToHashSet();

        foreach (var permissionId in ids)
        {
            if (existingSet.Contains(permissionId))
            {
                continue;
            }

            context.RolePermissions.Add(new Domain.Entities.RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId
            });
        }
    }

    public static async Task SetRolePermissionsByIdsAsync(
        ApplicationDbContext context,
        Guid roleId,
        IEnumerable<Guid> permissionIds)
    {
        var ids = await ValidatePermissionIdsAsync(context, permissionIds);

        var existing = await context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync();

        context.RolePermissions.RemoveRange(existing);

        foreach (var permissionId in ids)
        {
            context.RolePermissions.Add(new Domain.Entities.RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId
            });
        }
    }

    public static async Task AssignPermissionsToRoleAsync(
        ApplicationDbContext context,
        Guid roleId,
        IEnumerable<string> permissionNames)
    {
        var names = await ValidatePermissionNamesAsync(context, permissionNames);

        var permissions = await context.Permissions
            .Where(p => names.Contains(p.Name))
            .ToListAsync();

        var existingPermissionIds = await context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionId)
            .ToListAsync();

        var existingSet = existingPermissionIds.ToHashSet();

        foreach (var permission in permissions)
        {
            if (existingSet.Contains(permission.Id))
            {
                continue;
            }

            context.RolePermissions.Add(new Domain.Entities.RolePermission
            {
                RoleId = roleId,
                PermissionId = permission.Id
            });
        }
    }

    public static async Task SetRolePermissionsAsync(
        ApplicationDbContext context,
        Guid roleId,
        IEnumerable<string> permissionNames)
    {
        var names = await ValidatePermissionNamesAsync(context, permissionNames);

        var permissions = await context.Permissions
            .Where(p => names.Contains(p.Name))
            .ToListAsync();

        var existing = await context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync();

        context.RolePermissions.RemoveRange(existing);

        foreach (var permission in permissions)
        {
            context.RolePermissions.Add(new Domain.Entities.RolePermission
            {
                RoleId = roleId,
                PermissionId = permission.Id
            });
        }
    }

    public static async Task AddUserToRoleAsync(
        ApplicationDbContext context,
        Guid userId,
        Guid roleId)
    {
        var exists = await context.Set<IdentityUserRole<Guid>>()
            .AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

        if (exists)
        {
            return;
        }

        context.Set<IdentityUserRole<Guid>>().Add(new IdentityUserRole<Guid>
        {
            UserId = userId,
            RoleId = roleId
        });

        await context.SaveChangesAsync();
    }
}
