using Application.Common;
using Application.Interfaces.Authorization;
using Application.Interfaces.Tenant;
using Infrastructure.Identity.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Authorization;

public class CurrentUserPermissionService : ICurrentUserPermissionService
{
    private const string PermissionsCacheKey = "__UserPermissions";

    private readonly ApplicationDbContext _context;

    private readonly ICurrentTenantService _currentTenantService;

    private readonly UserManager<ApplicationUser> _userManager;

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserPermissionService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
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

        if (httpContext?.User.IsInRole(RoleNames.SuperAdmin) == true)
        {
            var allPermissions = await _context.Permissions
                .Select(p => p.Name)
                .ToListAsync();

            CachePermissions(httpContext, allPermissions);

            return allPermissions;
        }

        var tenantId = _currentTenantService.TenantId ?? Guid.Empty;

        var roleIds = await _context.Set<IdentityUserRole<Guid>>()
            .Where(ur => ur.UserId == userId)
            .Join(
                _context.Roles.Where(r => r.TenantId == tenantId),
                ur => ur.RoleId,
                r => r.Id,
                (_, r) => r.Id)
            .ToListAsync();

        if (roleIds.Count == 0)
        {
            CachePermissions(httpContext, []);

            return [];
        }

        var permissions = await _context.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Join(
                _context.Permissions,
                rp => rp.PermissionId,
                p => p.Id,
                (_, p) => p.Name)
            .Distinct()
            .ToListAsync();

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
