using Application.Interfaces.Caching;
using Infrastructure.Identity;
using Infrastructure.Identity.Entities;
using Infrastructure.Identity.Seed;
using Infrastructure.Persistence.Contexts;
using Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Identity;
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
        var seedOnStartup = configuration.GetValue("SeedOnStartup", false);

        if (!applyMigrations && !seedOnStartup)
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

        if (!seedOnStartup)
        {
            return;
        }

        logger.LogInformation("Running database seeders.");

        await DbSeeder.SeedAsync(dbContext);
        await PermissionSeeder.SeedAsync(dbContext);

        sp.GetRequiredService<IAppCache>().InvalidatePermissionCatalogs();

        var roleManager = sp.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var identityRoleService = sp.GetRequiredService<IIdentityRoleService>();

        await IdentitySeeder.SeedAsync(
            roleManager,
            userManager,
            dbContext,
            identityRoleService,
            configuration);

        logger.LogInformation("Database seeding completed.");
    }
}
