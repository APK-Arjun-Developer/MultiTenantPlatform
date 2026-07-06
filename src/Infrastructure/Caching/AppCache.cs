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
        // IMemoryCache.GetOrCreateAsync has internal locking to prevent cache stampede.
        var result = await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = absoluteExpiration;
            return await factory(cancellationToken);
        });

        return result!;
    }

    public void Remove(string key) => _cache.Remove(key);

    public void InvalidatePermissionCatalogs()
    {
        Remove(CacheKeys.PermissionCatalogSystem);
        Remove(CacheKeys.PermissionCatalogTenant);
        Remove(CacheKeys.PermissionNamesSystem);
    }

    public void InvalidateRole(Guid roleId) =>
        Remove(CacheKeys.RolePermissions(roleId));

    public void InvalidateTenantCatalog() =>
        Remove(CacheKeys.TenantCatalogAll);

    public void InvalidateTenant(Guid tenantId)
    {
        Remove(CacheKeys.TenantDetail(tenantId));
        InvalidateTenantCatalog();
    }

    public void InvalidateUserStatus(Guid userId) =>
        Remove(CacheKeys.UserStatus(userId));

    public void InvalidateTenantStatus(Guid tenantId) =>
        Remove(CacheKeys.TenantStatus(tenantId));

}
