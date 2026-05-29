using Application.Interfaces.Caching;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Caching;

public class RolePermissionLookup : IRolePermissionLookup
{
    private readonly ApplicationDbContext _context;
    private readonly IAppCache _cache;
    private readonly CacheOptions _options;

    public RolePermissionLookup(
        ApplicationDbContext context,
        IAppCache cache,
        IOptions<CacheOptions> options)
    {
        _context = context;
        _cache = cache;
        _options = options.Value;
    }

    public Task<RolePermissionSnapshot> GetAsync(Guid roleId) =>
        _cache.GetOrCreateAsync(
            CacheKeys.RolePermissions(roleId),
            ct => LoadAsync(roleId, ct),
            TimeSpan.FromMinutes(_options.RolePermissionsMinutes));

    public void Invalidate(Guid roleId) =>
        _cache.InvalidateRole(roleId);

    private async Task<RolePermissionSnapshot> LoadAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var permissionRows = await _context.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Join(
                _context.Permissions.AsNoTracking(),
                rp => rp.PermissionId,
                p => p.Id,
                (_, p) => new { p.Id, p.Name })
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return new RolePermissionSnapshot
        {
            PermissionIds = permissionRows.Select(p => p.Id).ToList(),
            PermissionNames = permissionRows.Select(p => p.Name).ToList(),
        };
    }
}
