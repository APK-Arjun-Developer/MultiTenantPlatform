using Application.Interfaces.Caching;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Caching;

public sealed class AppCache : IAppCache
{
    private readonly IMemoryCache _cache;

    public AppCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan absoluteExpiration,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            return cached;
        }

        var value = await factory(cancellationToken);

        _cache.Set(
            key,
            value,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = absoluteExpiration,
            });

        return value;
    }

    public void Remove(string key) => _cache.Remove(key);

    public void InvalidatePermissionCatalogs()
    {
        Remove(CacheKeys.PermissionCatalogSystem);
        Remove(CacheKeys.PermissionCatalogTenant);
    }

    public void InvalidateRole(Guid roleId) =>
        Remove(CacheKeys.RolePermissions(roleId));

    public void InvalidateTenantCatalog() =>
        Remove(CacheKeys.TenantCatalogAll);

    public void InvalidateTenant(Guid tenantId)
    {
        Remove(CacheKeys.TenantDetail(tenantId));
        InvalidateTenantDashboard(tenantId);
    }

    public void InvalidateProducts(Guid tenantId) =>
        Remove(CacheKeys.Products(tenantId));

    public void InvalidateReportSummary(Guid tenantId) =>
        Remove(CacheKeys.ReportSummary(tenantId));

    public void InvalidateTenantDashboard(Guid tenantId)
    {
        InvalidateProducts(tenantId);
        InvalidateReportSummary(tenantId);
    }
}
