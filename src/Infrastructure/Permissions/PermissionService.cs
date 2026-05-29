using Application.Common;
using Application.DTOs.Permissions;
using Application.Interfaces.Caching;
using Application.Interfaces.Permissions;
using Application.Interfaces.Tenant;
using Infrastructure.Caching;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Permissions;

public class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IAppCache _cache;
    private readonly CacheOptions _cacheOptions;

    public PermissionService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        IAppCache cache,
        IOptions<CacheOptions> cacheOptions)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _cache = cache;
        _cacheOptions = cacheOptions.Value;
    }

    public async Task<PermissionsCatalogResponse> GetCatalogAsync(bool groupByModule = false)
    {
        var cacheKey = IsSystemAdmin()
            ? CacheKeys.PermissionCatalogSystem
            : CacheKeys.PermissionCatalogTenant;

        var response = await _cache.GetOrCreateAsync(
            cacheKey,
            async _ => await LoadCatalogAsync(),
            TimeSpan.FromMinutes(_cacheOptions.PermissionCatalogMinutes));

        return CloneForGrouping(response, groupByModule);
    }

    private async Task<PermissionsCatalogResponse> LoadCatalogAsync()
    {
        var query = _context.Permissions.AsNoTracking();

        if (!IsSystemAdmin())
        {
            var allowed = PermissionNames.TenantPermissions.ToHashSet(StringComparer.Ordinal);
            query = query.Where(p => allowed.Contains(p.Name));
        }

        var items = await query
            .OrderBy(p => p.Module)
            .ThenBy(p => p.Name)
            .Select(p => new PermissionResponse
            {
                Id = p.Id,
                Name = p.Name,
                Module = p.Module,
                Description = p.Description,
            })
            .ToListAsync();

        return new PermissionsCatalogResponse
        {
            Items = items,
        };
    }

    private static PermissionsCatalogResponse CloneForGrouping(
        PermissionsCatalogResponse source,
        bool groupByModule)
    {
        if (!groupByModule)
        {
            return new PermissionsCatalogResponse
            {
                Items = source.Items,
            };
        }

        return new PermissionsCatalogResponse
        {
            Items = source.Items,
            ByModule = source.Items
                .GroupBy(p => p.Module)
                .Select(g => new PermissionModuleGroupResponse
                {
                    Module = g.Key,
                    Permissions = g.ToList(),
                })
                .ToList(),
        };
    }

    private bool IsSystemAdmin() =>
        (_currentTenantService.TenantId ?? Guid.Empty) == Guid.Empty;
}
