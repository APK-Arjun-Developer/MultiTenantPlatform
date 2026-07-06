using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Jobs;

/// <summary>
/// Runs every 24 hours and removes tokens that are well past their useful life:
///   • Refresh tokens    — expired or revoked > 30 days ago
///   • AccountSetupTokens — used or expired > 30 days ago
///   • PasswordResetTokens — used or expired > 30 days ago
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

        var refreshDeleted = await context.RefreshTokens
            .Where(t =>
                t.ExpiresAt < cutoff ||
                (t.RevokedAt != null && t.RevokedAt < cutoff))
            .ExecuteDeleteAsync(cancellationToken);

        var setupDeleted = await context.AccountSetupTokens
            .Where(t =>
                (t.UsedAt != null && t.UsedAt < cutoff) ||
                (t.UsedAt == null && t.ExpiresAt < cutoff))
            .ExecuteDeleteAsync(cancellationToken);

        var resetDeleted = await context.PasswordResetTokens
            .Where(t =>
                (t.UsedAt != null && t.UsedAt < cutoff) ||
                (t.UsedAt == null && t.ExpiresAt < cutoff))
            .ExecuteDeleteAsync(cancellationToken);

        var totalDeleted = refreshDeleted + setupDeleted + resetDeleted;

        if (totalDeleted > 0)
        {
            _logger.LogInformation(
                "Token cleanup complete. Deleted: {Refresh} refresh, {Setup} setup, {Reset} password-reset (cutoff: {Cutoff:O}).",
                refreshDeleted, setupDeleted, resetDeleted, cutoff);
        }
    }
}
