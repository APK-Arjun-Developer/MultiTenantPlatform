namespace Application.Interfaces.Caching;

/// <summary>
/// Tenant-aware in-memory cache with explicit invalidation on writes.
/// Prefer caching reference/read-heavy data; invalidate in the same service that mutates data.
/// </summary>
public interface IAppCache
{
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan absoluteExpiration,
        CancellationToken cancellationToken = default);

    void Remove(string key);

    void InvalidatePermissionCatalogs();

    void InvalidateRole(Guid roleId);

    void InvalidateTenantCatalog();

    void InvalidateTenant(Guid tenantId);

    void InvalidateUserStatus(Guid userId);

    void InvalidateTenantStatus(Guid tenantId);

}
