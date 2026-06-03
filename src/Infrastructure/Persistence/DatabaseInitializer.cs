using Infrastructure.Persistence.Contexts;
using Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task ApplyMigrationsAndSeedAsync(
        IServiceProvider services,
        IConfiguration configuration,
        ILogger logger)
    {
        var applyMigrations = configuration.GetValue("ApplyMigrationsOnStartup", true);
        var applySeeds = configuration.GetValue("ApplySeedsOnStartup", true);

        if (!applyMigrations && !applySeeds)
        {
            return;
        }

        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var dbContext = sp.GetRequiredService<ApplicationDbContext>();

        if (applyMigrations)
        {
            var pending = await dbContext.Database.GetPendingMigrationsAsync();

            if (pending.Any())
            {
                logger.LogInformation(
                    "Applying {Count} pending migration(s): {Migrations}",
                    pending.Count(),
                    string.Join(", ", pending));

                await dbContext.Database.MigrateAsync();
            }
            else
            {
                logger.LogInformation("Database schema is up to date; no pending migrations.");
            }
        }

        if (!applySeeds)
        {
            return;
        }

        var seedRunner = sp.GetRequiredService<SeedRunner>();
        await seedRunner.ApplyPendingSeedsAsync();
    }
}
