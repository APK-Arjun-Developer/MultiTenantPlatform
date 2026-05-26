using Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity.Seed;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager)
    {
        const string adminRole = "SuperAdmin";

        if (!await roleManager.RoleExistsAsync(adminRole))
        {
            await roleManager.CreateAsync(new ApplicationRole
            {
                Id = Guid.NewGuid(),
                Name = adminRole,
                NormalizedName = adminRole.ToUpper(),
                Description = "System Super Administrator",
                TenantId = Guid.Empty
            });
        }

        const string adminEmail = "admin@system.com";

        var existingUser =
            await userManager.FindByEmailAsync(adminEmail);

        if (existingUser == null)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.Empty,
                FullName = "System Administrator",
                UserName = adminEmail,
                Email = adminEmail,
                NormalizedEmail = adminEmail.ToUpper(),
                NormalizedUserName = adminEmail.ToUpper(),
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(
                user,
                "Admin123!");

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(
                    user,
                    adminRole);
            }
        }
    }
}