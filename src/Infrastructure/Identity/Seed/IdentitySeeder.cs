using Application.Common;
using Infrastructure.Identity.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Identity.Seed;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        IIdentityRoleService identityRoleService,
        IConfiguration configuration)
    {
        var adminPassword = configuration["Seeding:AdminPassword"];

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException(
                "Seeding:AdminPassword must be configured before seeding. " +
                "Set it via environment variable, user secrets, or a secrets manager. " +
                "Never hardcode credentials in source files.");
        }

        const string adminRole = RoleNames.SuperAdmin;
        var platformTenantId = Guid.Empty;

        var superAdminRole = await identityRoleService.FindRoleByNameAsync(platformTenantId, adminRole);

        if (superAdminRole == null)
        {
            superAdminRole = await identityRoleService.CreateRoleAsync(
                platformTenantId,
                adminRole,
                "System Super Administrator");
        }

        await identityRoleService.AssignPermissionsToRoleAsync(superAdminRole.Id, PermissionNames.All);

        await context.SaveChangesAsync();

        const string adminEmail = "admin@system.com";

        var existingUser = await userManager.Users
            .FirstOrDefaultAsync(u =>
                u.NormalizedEmail == adminEmail.ToUpperInvariant() &&
                u.TenantId == platformTenantId);

        if (existingUser == null)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                TenantId = platformTenantId,
                FullName = "System Administrator",
                UserName = adminEmail,
                Email = adminEmail,
                NormalizedEmail = adminEmail.ToUpperInvariant(),
                NormalizedUserName = adminEmail.ToUpperInvariant(),
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(user, adminPassword);

            if (result.Succeeded)
            {
                await identityRoleService.AddUserToRoleAsync(user.Id, superAdminRole.Id);
            }
            else
            {
                throw new InvalidOperationException(
                    "Failed to create system admin user: " +
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
