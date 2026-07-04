using Application.Common;
using Application.DTOs.ActivityLogs;
using Application.DTOs.Common;
using Application.DTOs.Roles;
using Application.Exceptions;
using Application.Interfaces.ActivityLogs;
using Application.Interfaces.Caching;
using Application.Interfaces.Roles;
using Application.Interfaces.Tenant;
using Infrastructure.Caching;
using Infrastructure.Common;
using Infrastructure.Identity;
using Infrastructure.Identity.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Roles;

public class RoleService : TenantScopedService, IRoleService
{
    private readonly ApplicationDbContext _context;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IActivityLogService _activityLogService;
    private readonly IRolePermissionLookup _rolePermissionLookup;
    private readonly IAppCache _cache;
    private readonly IIdentityRoleService _identityRoleService;

    public RoleService(
        ApplicationDbContext context,
        RoleManager<ApplicationRole> roleManager,
        ICurrentTenantService currentTenantService,
        IActivityLogService activityLogService,
        IRolePermissionLookup rolePermissionLookup,
        IAppCache cache,
        IIdentityRoleService identityRoleService)
        : base(currentTenantService)
    {
        _context = context;
        _roleManager = roleManager;
        _activityLogService = activityLogService;
        _rolePermissionLookup = rolePermissionLookup;
        _cache = cache;
        _identityRoleService = identityRoleService;
    }

    public async Task<PagedResponse<RoleResponse>> GetRolesAsync(
        int page, int pageSize,
        string? search = null,
        IReadOnlyList<Guid>? permissionIds = null)
    {
        (page, pageSize) = Pagination.Normalize(page, pageSize);

        var tenantId = RequireTenantId();
        IQueryable<ApplicationRole> query = _roleManager.Roles.Where(r => r.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(r => r.Name!.Contains(search));
        }

        if (permissionIds is { Count: > 0 })
        {
            query = query.Where(r => _context.RolePermissions
                .Any(rp => rp.RoleId == r.Id && permissionIds.Contains(rp.PermissionId)));
        }

        var totalCount = await query.CountAsync();

        var roles = await query
            .OrderBy(r => r.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = await BatchMapRolesAsync(roles);

        return new PagedResponse<RoleResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<RoleResponse> GetByNameAsync(string name)
    {
        var tenantId = RequireTenantId();
        var role = await _roleManager.Roles
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Name == name)
            ?? throw new NotFoundException($"Role '{name}' not found.");

        return await MapRoleAsync(role);
    }

    public async Task<RoleResponse> GetCurrentRoleAsync()
    {
        var roleId = CurrentTenantService.RoleId
            ?? throw new InvalidOperationException(
                "Role context is required. Ensure role_id is present in the JWT.");

        var role = IsSystemAdmin()
            ? await _roleManager.Roles.FirstOrDefaultAsync(r => r.Id == roleId)
            : await _roleManager.Roles.FirstOrDefaultAsync(r => r.Id == roleId && r.TenantId == RequireTenantId());

        if (role is null)
            throw new NotFoundException("Role not found.");

        return await MapRoleAsync(role);
    }

    public async Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request)
    {
        var tenantId = RequireTenantId();

        if (request.Name is RoleNames.SystemAdmin or RoleNames.TenantAdmin or RoleNames.TenantUser)
        {
            throw new ForbiddenException($"Cannot create built-in system role '{request.Name}'.");
        }

        if (await _identityRoleService.RoleExistsAsync(tenantId, request.Name))
        {
            throw new ConflictException($"Role '{request.Name}' already exists for this tenant.");
        }

        var role = await _identityRoleService.CreateRoleAsync(tenantId, request.Name, request.Description);

        await _identityRoleService.AssignPermissionsToRoleByIdsAsync(role.Id, request.Permissions);

        await _context.SaveChangesAsync();

        InvalidateRoleCaches(role);

        await LogActivityAsync(ActivityActions.Roles.Created, $"Created role '{role.Name}'.");

        return await MapRoleAsync(role);
    }

    public async Task<RoleResponse> UpdateRoleAsync(UpdateRoleRequest request)
    {
        var tenantId = RequireTenantId();
        var role = await _roleManager.Roles
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Name == request.Name)
            ?? throw new NotFoundException("Role not found.");

        if (role.Name is RoleNames.SystemAdmin or RoleNames.TenantAdmin or RoleNames.TenantUser)
        {
            throw new ForbiddenException($"Cannot modify built-in system role '{role.Name}'.");
        }

        var newName = request.NewName?.Trim();
        if (!string.IsNullOrEmpty(newName) && newName != role.Name)
        {
            if (newName is RoleNames.SystemAdmin or RoleNames.TenantAdmin or RoleNames.TenantUser)
            {
                throw new ForbiddenException($"Cannot rename a role to built-in system role '{newName}'.");
            }

            if (await _identityRoleService.RoleExistsAsync(tenantId, newName))
            {
                throw new ConflictException($"Role '{newName}' already exists for this tenant.");
            }

            role.Name = newName;
            role.NormalizedName = newName.ToUpperInvariant();
        }

        role.Description = request.Description;

        await _identityRoleService.SetRolePermissionsByIdsAsync(role.Id, request.Permissions);

        await _context.SaveChangesAsync();

        InvalidateRoleCaches(role);

        await LogActivityAsync(ActivityActions.Roles.Updated, $"Updated role '{role.Name}'.");

        return await MapRoleAsync(role);
    }

    public async Task DeleteRoleAsync(DeleteRoleRequest request)
    {
        var tenantId = RequireTenantId();
        var role = await _roleManager.Roles
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Name == request.Name)
            ?? throw new NotFoundException("Role not found.");

        if (role.Name is RoleNames.SystemAdmin or RoleNames.TenantAdmin or RoleNames.TenantUser)
        {
            throw new ForbiddenException($"Cannot delete the default '{role.Name}' role.");
        }

        var assignedUsers = await _context.Set<IdentityUserRole<Guid>>()
            .AnyAsync(ur => ur.RoleId == role.Id);

        if (assignedUsers)
        {
            throw new ConflictException("Cannot delete a role that is assigned to users.");
        }

        role.DeletedAt = DateTime.UtcNow;
        var result = await _roleManager.UpdateAsync(role);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        InvalidateRoleCaches(role);

        await LogActivityAsync(ActivityActions.Roles.Deleted, $"Deleted role '{role.Name}'.");
    }

    private void InvalidateRoleCaches(ApplicationRole role)
    {
        _rolePermissionLookup.Invalidate(role.Id);
    }

    private async Task LogActivityAsync(string action, string description)
    {
        await _activityLogService.LogAsync(new LogActivityRequest
        {
            UserId = RequireUserId(),
            Action = action,
            Module = ActivityModules.Roles,
            Description = description,
        });
    }

    // Batch-load permissions for all roles in one query - eliminates N+1.
    private async Task<List<RoleResponse>> BatchMapRolesAsync(List<ApplicationRole> roles)
    {
        if (roles.Count == 0)
        {
            return [];
        }

        var roleIds = roles.Select(r => r.Id).ToList();

        var permissionsByRole = await _context.RolePermissions
            .AsNoTracking()
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Join(_context.Permissions.AsNoTracking(),
                rp => rp.PermissionId,
                p => p.Id,
                (rp, p) => new { rp.RoleId, p.Id, p.Name })
            .ToListAsync();

        var grouped = permissionsByRole
            .GroupBy(x => x.RoleId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return roles.Select(role =>
        {
            var perms = grouped.GetValueOrDefault(role.Id, []);

            return new RoleResponse
            {
                Id = role.Id,
                Name = role.Name!,
                Description = role.Description,
                TenantId = role.TenantId,
                PermissionIds = perms.Select(p => p.Id).ToList(),
                PermissionNames = perms.Select(p => p.Name).ToList(),
            };
        }).ToList();
    }

    private async Task<RoleResponse> MapRoleAsync(ApplicationRole role)
    {
        var snapshot = await _rolePermissionLookup.GetAsync(role.Id);

        return new RoleResponse
        {
            Id = role.Id,
            Name = role.Name!,
            Description = role.Description,
            TenantId = role.TenantId,
            PermissionIds = snapshot.PermissionIds.ToList(),
            PermissionNames = snapshot.PermissionNames.ToList(),
        };
    }
}

