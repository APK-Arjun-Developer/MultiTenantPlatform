using Application.Interfaces.Caching;
using Domain.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence.Seed;

public sealed class SeedRunner
{
    private readonly ApplicationDbContext _context;
    private readonly IEnumerable<IDataSeed> _seeds;
    private readonly IAppCache _cache;
    private readonly ILogger<SeedRunner> _logger;

    public SeedRunner(
        ApplicationDbContext context,
        IEnumerable<IDataSeed> seeds,
        IAppCache cache,
        ILogger<SeedRunner> logger)
    {
        _context = context;
        _seeds = seeds.OrderBy(s => s.SeedId, StringComparer.Ordinal);
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> GetPendingSeedIdsAsync(
        CancellationToken cancellationToken = default)
    {
        var applied = await _context.SeedHistory
            .AsNoTracking()
            .Select(x => x.SeedId)
            .ToListAsync(cancellationToken);

        var appliedSet = applied.ToHashSet(StringComparer.Ordinal);

        return _seeds
            .Where(s => !appliedSet.Contains(s.SeedId))
            .Select(s => s.SeedId)
            .ToList();
    }

    public async Task ApplyPendingSeedsAsync(CancellationToken cancellationToken = default)
    {
        var pending = await GetPendingSeedIdsAsync(cancellationToken);

        if (pending.Count == 0)
        {
            _logger.LogInformation("Database seeds are up to date; no pending seeds.");
            return;
        }

        _logger.LogInformation(
            "Applying {Count} pending seed(s): {Seeds}",
            pending.Count,
            string.Join(", ", pending));

        var pendingSet = pending.ToHashSet(StringComparer.Ordinal);
        var permissionCatalogChanged = false;

        foreach (var seed in _seeds.Where(s => pendingSet.Contains(s.SeedId)))
        {
            _logger.LogInformation("Applying seed '{SeedId}' ...", seed.SeedId);

            await seed.ApplyAsync(cancellationToken);

            _context.SeedHistory.Add(new SeedHistory
            {
                SeedId = seed.SeedId,
                AppliedAt = DateTime.UtcNow,
                Description = seed.Description,
            });

            await _context.SaveChangesAsync(cancellationToken);

            if (seed.SeedId.Contains("Permissions", StringComparison.OrdinalIgnoreCase))
            {
                permissionCatalogChanged = true;
            }

            _logger.LogInformation("Applied seed '{SeedId}'.", seed.SeedId);
        }

        if (permissionCatalogChanged)
        {
            _cache.InvalidatePermissionCatalogs();
        }

        _logger.LogInformation("Database seeding completed.");
    }
}
