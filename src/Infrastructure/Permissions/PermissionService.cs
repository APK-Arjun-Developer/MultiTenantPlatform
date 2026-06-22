using Application.Common;
using Application.DTOs.Permissions;
using Application.Interfaces.Caching;
using Application.Interfaces.Permissions;
using Application.Interfaces.Tenant;
using Domain.Enums;
using Infrastructure.Caching;
using Infrastructure.Common;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Permissions;

public class PermissionService : TenantScopedService, IPermissionService
{
    private readonly ApplicationDbContext _context;
    private readonly IAppCache _cache;
    private readonly CacheOptions _cacheOptions;

    public PermissionService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        IAppCache cache,
        IOptions<CacheOptions> cacheOptions)
        : base(currentTenantService)
    {
        _context = context;
        _cache = cache;
        _cacheOptions = cacheOptions.Value;
    }

    public async Task<PermissionsCatalogResponse> GetCatalogAsync(SystemRole? scopeFilter = null, bool groupByModule = false)
    {
        var cacheKey = IsSystemAdmin()
            ? CacheKeys.PermissionCatalogSystem
            : CacheKeys.PermissionCatalogTenant;

        // Load the full catalog for this caller type; scope filtering happens in memory.
        var catalog = await _cache.GetOrCreateAsync(
            cacheKey,
            async _ => await LoadCatalogAsync(),
            TimeSpan.FromMinutes(_cacheOptions.PermissionCatalogMinutes));

        // Tenant Admin callers cannot request SystemAdmin-scoped permissions.
        var effectiveFilter = (!IsSystemAdmin() && scopeFilter == SystemRole.SystemAdmin)
            ? (SystemRole?)null
            : scopeFilter;

        var items = effectiveFilter.HasValue
            ? catalog.Items.Where(p => p.Scope == effectiveFilter.Value.ToString()).ToList()
            : (IReadOnlyList<PermissionResponse>)catalog.Items;

        return BuildResponse(items, groupByModule);
    }

    private async Task<PermissionsCatalogResponse> LoadCatalogAsync()
    {
        var query = _context.Permissions.AsNoTracking();

        if (!IsSystemAdmin())
        {
            var allowed = PermissionNames.TenantUserPermissions.ToHashSet(StringComparer.Ordinal);
            query = query.Where(p => allowed.Contains(p.Name));
        }

        var rows = await query
            .OrderBy(p => p.Module)
            .ThenBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.Module, p.Description, p.RequiredSystemRole })
            .ToListAsync();

        var items = rows.Select(p => new PermissionResponse
        {
            Id = p.Id,
            Name = p.Name,
            Module = p.Module,
            Description = p.Description,
            Scope = p.RequiredSystemRole.ToString(),
        }).ToList();

        return new PermissionsCatalogResponse { Items = items };
    }

    private static PermissionsCatalogResponse BuildResponse(
        IReadOnlyList<PermissionResponse> items,
        bool groupByModule)
    {
        if (!groupByModule)
        {
            return new PermissionsCatalogResponse { Items = items };
        }

        return new PermissionsCatalogResponse
        {
            Items = items,
            ByModule = items
                .GroupBy(p => p.Module)
                .Select(g => new PermissionModuleGroupResponse
                {
                    Module = g.Key,
                    Permissions = g.ToList(),
                })
                .ToList(),
        };
    }
}
