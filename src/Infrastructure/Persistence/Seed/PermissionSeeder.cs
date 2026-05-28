using Application.Common;
using Domain.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Seed;

public static class PermissionSeeder
{
    private static readonly (string Name, string Module, string Description)[] PermissionDefinitions =
    [
        (PermissionNames.UsersCreate, "Users", "Create users"),
        (PermissionNames.UsersView, "Users", "View users"),
        (PermissionNames.UsersEdit, "Users", "Edit users"),
        (PermissionNames.UsersDelete, "Users", "Delete users"),
        (PermissionNames.RolesCreate, "Roles", "Create roles"),
        (PermissionNames.RolesView, "Roles", "View roles"),
        (PermissionNames.RolesEdit, "Roles", "Edit roles"),
        (PermissionNames.RolesDelete, "Roles", "Delete roles"),
        (PermissionNames.ProductsCreate, "Products", "Create products"),
        (PermissionNames.ProductsView, "Products", "View products"),
        (PermissionNames.ProductsEdit, "Products", "Edit products"),
        (PermissionNames.ProductsDelete, "Products", "Delete products"),
        (PermissionNames.ReportsView, "Reports", "View reports"),
        (PermissionNames.ReportsExport, "Reports", "Export reports"),
        (PermissionNames.TenantsCreate, "Tenants", "Create tenants"),
        (PermissionNames.TenantsView, "Tenants", "View tenants"),
        (PermissionNames.TenantsEdit, "Tenants", "Edit tenants"),
        (PermissionNames.TenantsDelete, "Tenants", "Delete tenants")
    ];

    public static async Task SeedAsync(ApplicationDbContext context)
    {
        var existingNames = await context.Permissions
            .Select(p => p.Name)
            .ToListAsync();

        var existingSet = existingNames.ToHashSet(StringComparer.Ordinal);

        foreach (var (name, module, description) in PermissionDefinitions)
        {
            if (existingSet.Contains(name))
            {
                continue;
            }

            context.Permissions.Add(new Permission
            {
                Id = Guid.NewGuid(),
                Name = name,
                Module = module,
                Description = description,
                CreatedAt = DateTime.UtcNow
            });
        }

        await context.SaveChangesAsync();
    }
}
