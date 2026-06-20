using Application.Common;
using Application.Interfaces.Tenant;
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
    private readonly ICurrentTenantService _currentTenantService;

    public IdentityRoleService(
        ApplicationDbContext context,
        RoleManager<ApplicationRole> roleManager,
        ICurrentTenantService currentTenantService)
    {
        _context = context;
        _roleManager = roleManager;
        _currentTenantService = currentTenantService;
    }

    private bool IsSystemAdmin() =>
        !_currentTenantService.TenantId.HasValue ||          // null = no HTTP context (seed/background)
        _currentTenantService.TenantId.Value == Guid.Empty;  // Guid.Empty = System Admin JWT

    private static void RejectPlatformPermissions(IEnumerable<Guid> permissionIds, IEnumerable<Permission> permissions)
    {
        var tenantSafeNames = PermissionNames.TenantPermissions.ToHashSet(StringComparer.Ordinal);
        var platformIds = permissions
            .Where(p => !tenantSafeNames.Contains(p.Name))
            .Select(p => p.Id)
            .ToHashSet();

        var violating = permissionIds.Where(id => platformIds.Contains(id)).ToList();
        if (violating.Count > 0)
        {
            throw new InvalidOperationException(
                "Tenant roles cannot be assigned platform-only permissions " +
                $"({string.Join(", ", violating)}).");
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

    public async Task<List<string>> ValidatePermissionNamesAsync(IEnumerable<string> permissionNames)
    {
        var names = permissionNames.Distinct(StringComparer.Ordinal).ToList();

        if (names.Count == 0)
        {
            throw new InvalidOperationException("At least one permission is required.");
        }

        var existing = await _context.Permissions
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

        if (!IsSystemAdmin())
        {
            var permissions = await _context.Permissions
                .Where(p => ids.Contains(p.Id))
                .ToListAsync();
            RejectPlatformPermissions(ids, permissions);
        }

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

        if (!IsSystemAdmin())
        {
            var permissions = await _context.Permissions
                .Where(p => ids.Contains(p.Id))
                .ToListAsync();
            RejectPlatformPermissions(ids, permissions);
        }

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

    public async Task AssignPermissionsToRoleAsync(Guid roleId, IEnumerable<string> permissionNames)
    {
        var names = await ValidatePermissionNamesAsync(permissionNames);

        var permissions = await _context.Permissions
            .Where(p => names.Contains(p.Name))
            .ToListAsync();

        if (!IsSystemAdmin())
        {
            RejectPlatformPermissions(permissions.Select(p => p.Id), permissions);
        }

        var existingSet = (await _context.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionId)
            .ToListAsync()).ToHashSet();

        foreach (var permission in permissions.Where(p => !existingSet.Contains(p.Id)))
        {
            _context.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permission.Id
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

    public async Task<ApplicationRole> EnsureTenantAdminRoleAsync(Guid tenantId, CancellationToken ct = default)
    {
        var role = await CreateRoleAsync(
            tenantId,
            RoleNames.TenantAdmin,
            "Tenant administrator — full access to all tenant resources.");

        await _context.SaveChangesAsync(ct);

        await AssignPermissionsToRoleAsync(role.Id, PermissionNames.TenantPermissions);

        await _context.SaveChangesAsync(ct);

        return role;
    }

    public async Task<ApplicationRole> EnsureTenantUserRoleAsync(Guid tenantId, CancellationToken ct = default)
    {
        var role = await CreateRoleAsync(
            tenantId,
            RoleNames.TenantUser,
            "Standard tenant user — basic access to products, reports, and files.");

        await _context.SaveChangesAsync(ct);

        await AssignPermissionsToRoleAsync(role.Id, PermissionNames.TenantUserPermissions);

        await _context.SaveChangesAsync(ct);

        return role;
    }
}
