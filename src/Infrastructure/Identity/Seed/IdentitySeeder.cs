using Application.Common;
using Domain.Entities;
using Infrastructure.Identity.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity.Seed;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context)
    {
        const string adminRole = RoleNames.SuperAdmin;
        var platformTenantId = Guid.Empty;

        var superAdminRole = await IdentityRoleHelper.FindRoleByNameAsync(
            roleManager,
            platformTenantId,
            adminRole);

        if (superAdminRole == null)
        {
            superAdminRole = await IdentityRoleHelper.CreateRoleAsync(
                context,
                platformTenantId,
                adminRole,
                "System Super Administrator");
        }

        await IdentityRoleHelper.AssignPermissionsToRoleAsync(
            context,
            superAdminRole.Id,
            PermissionNames.All);

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

            var result = await userManager.CreateAsync(
                user,
                "Admin123!");

            if (result.Succeeded)
            {
                await IdentityRoleHelper.AddUserToRoleAsync(
                    context,
                    user.Id,
                    superAdminRole.Id);
            }
        }
    }
}
