using Application.Common;
using Domain.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Seed.Seeds;

public sealed class PermissionsSeed : IDataSeed
{
    public const string Id = "20260603000002_Permissions";

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
        (PermissionNames.FilesView, "Files", "View files"),
        (PermissionNames.FilesUpload, "Files", "Upload files"),
        (PermissionNames.FilesDelete, "Files", "Delete files"),
        (PermissionNames.TenantsCreate, "Tenants", "Create tenants"),
        (PermissionNames.TenantsView, "Tenants", "View tenants"),
        (PermissionNames.TenantsEdit, "Tenants", "Edit tenants"),
        (PermissionNames.TenantsDelete, "Tenants", "Delete tenants"),
    ];

    private readonly ApplicationDbContext _context;

    public PermissionsSeed(ApplicationDbContext context)
    {
        _context = context;
    }

    public string SeedId => Id;

    public string Description => "RBAC permission catalog.";

    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        var existingNames = await _context.Permissions
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);

        var existingSet = existingNames.ToHashSet(StringComparer.Ordinal);

        foreach (var (name, module, description) in PermissionDefinitions)
        {
            if (existingSet.Contains(name))
            {
                continue;
            }

            _context.Permissions.Add(new Permission
            {
                Id = Guid.NewGuid(),
                Name = name,
                Module = module,
                Description = description,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
