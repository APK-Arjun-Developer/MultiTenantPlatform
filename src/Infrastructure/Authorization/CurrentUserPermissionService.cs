using Application.Common;
using Application.Interfaces.Authorization;
using Application.Interfaces.Caching;
using Application.Interfaces.Tenant;
using Domain.Enums;
using Infrastructure.Caching;
using Infrastructure.Identity.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Authorization;

public class CurrentUserPermissionService : ICurrentUserPermissionService
{
    private const string PermissionsCacheKey = "__UserPermissions";

    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAppCache _cache;
    private readonly IRolePermissionLookup _rolePermissionLookup;
    private readonly CacheOptions _cacheOptions;

    public CurrentUserPermissionService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        IHttpContextAccessor httpContextAccessor,
        IAppCache cache,
        IRolePermissionLookup rolePermissionLookup,
        IOptions<CacheOptions> cacheOptions)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _httpContextAccessor = httpContextAccessor;
        _cache = cache;
        _rolePermissionLookup = rolePermissionLookup;
        _cacheOptions = cacheOptions.Value;
    }

    public async Task<bool> HasPermissionAsync(string permission)
    {
        var permissions = await GetPermissionsAsync();

        return permissions.Contains(permission, StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext?.Items.TryGetValue(PermissionsCacheKey, out var cached) == true &&
            cached is IReadOnlyList<string> cachedPermissions)
        {
            return cachedPermissions;
        }

        if (!_currentTenantService.UserId.HasValue)
        {
            return [];
        }

        var userId = _currentTenantService.UserId.Value;

        // Resolve system role from JWT claim — no DB lookup required.
        var systemRoleClaim = httpContext?.User.FindFirst("system_role")?.Value;
        var userSystemRole = int.TryParse(systemRoleClaim, out var srInt)
            ? (SystemRole)srInt
            : SystemRole.TenantUser;

        if (userSystemRole == SystemRole.SystemAdmin)
        {
            var allPermissions = await _cache.GetOrCreateAsync(
                CacheKeys.PermissionNamesSystem,
                async _ => await _context.Permissions
                    .AsNoTracking()
                    .Select(p => p.Name)
                    .ToListAsync(),
                TimeSpan.FromMinutes(_cacheOptions.PermissionCatalogMinutes));

            CachePermissions(httpContext, allPermissions);
            return allPermissions;
        }

        if (userSystemRole == SystemRole.TenantAdmin)
        {
            // TenantAdmin always gets the full tenant-level permission set.
            // Permissions come from SystemRole, not from role table lookups.
            IReadOnlyList<string> tenantPerms = PermissionNames.TenantPermissions
                .OrderBy(p => p)
                .ToList();

            CachePermissions(httpContext, tenantPerms);
            return tenantPerms;
        }

        // TenantUser: permissions come from assigned custom roles only.
        var tenantId = _currentTenantService.TenantId ?? Guid.Empty;

        var roleIds = await _context.Set<IdentityUserRole<Guid>>()
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Join(
                _context.Roles.AsNoTracking().Where(r => r.TenantId == tenantId),
                ur => ur.RoleId,
                r => r.Id,
                (_, r) => r.Id)
            .ToListAsync();

        if (roleIds.Count == 0)
        {
            CachePermissions(httpContext, []);
            return [];
        }

        var permissionSet = new HashSet<string>(StringComparer.Ordinal);

        foreach (var roleId in roleIds)
        {
            var snapshot = await _rolePermissionLookup.GetAsync(roleId);

            foreach (var name in snapshot.PermissionNames)
            {
                permissionSet.Add(name);
            }
        }

        // Permission Ceiling: strip any permission requiring a higher system role.
        permissionSet.RemoveWhere(p =>
            PermissionNames.Scopes.TryGetValue(p, out var required) &&
            required < userSystemRole);

        var permissions = permissionSet.OrderBy(p => p).ToList();

        CachePermissions(httpContext, permissions);

        return permissions;
    }

    private static void CachePermissions(
        HttpContext? httpContext,
        IReadOnlyList<string> permissions)
    {
        if (httpContext != null)
        {
            httpContext.Items[PermissionsCacheKey] = permissions;
        }
    }
}
