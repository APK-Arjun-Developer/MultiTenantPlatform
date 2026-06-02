using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Jobs;

/// <summary>
/// Deletes refresh tokens that have been expired or revoked for more than 30 days.
/// Runs once every 24 hours to keep the RefreshTokens table bounded.
/// </summary>
public sealed class ExpiredTokenCleanupJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ExpiredTokenCleanupJob> _logger;
    private static readonly TimeSpan RunInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

    public ExpiredTokenCleanupJob(
        IServiceScopeFactory scopeFactory,
        ILogger<ExpiredTokenCleanupJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExpiredTokenCleanupJob started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error during expired token cleanup.");
            }

            await Task.Delay(RunInterval, stoppingToken);
        }

        _logger.LogInformation("ExpiredTokenCleanupJob stopped.");
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow - RetentionPeriod;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var deleted = await context.RefreshTokens
            .Where(t =>
                (t.ExpiresAt < cutoff) ||
                (t.RevokedAt != null && t.RevokedAt < cutoff))
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            _logger.LogInformation(
                "Cleaned up {Count} expired/revoked refresh tokens (cutoff: {Cutoff:O}).",
                deleted,
                cutoff);
        }
    }
}
