using Domain.Enums;
using Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Persistence.Seed.Seeds;

public sealed class SuperAdminSeed : IDataSeed
{
    public const string Id = "20260603000003_SuperAdmin";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;

    public SuperAdminSeed(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _configuration = configuration;
    }

    public string SeedId => Id;

    public string Description => "System administrator account (admin@system.com).";

    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        var adminPassword = _configuration["Seeding:AdminPassword"];

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException(
                "Seeding:AdminPassword must be configured before applying SuperAdmin seed. " +
                "Set it via environment variable, user secrets, or a secrets manager.");
        }

        const string adminEmail = "admin@system.com";

        var existingUser = await _userManager.Users
            .FirstOrDefaultAsync(u =>
                u.NormalizedEmail == adminEmail.ToUpperInvariant() &&
                u.TenantId == Guid.Empty,
                cancellationToken);

        if (existingUser != null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Empty,
            SystemRole = SystemRole.SystemAdmin,
            FullName = "System Administrator",
            UserName = adminEmail,
            Email = adminEmail,
            NormalizedEmail = adminEmail.ToUpperInvariant(),
            NormalizedUserName = adminEmail.ToUpperInvariant(),
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        var result = await _userManager.CreateAsync(user, adminPassword);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Failed to create system admin user: " +
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
