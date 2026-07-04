using Application.Common;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Seed.Seeds;

public sealed class AddListPermissionsSeed : IDataSeed
{
    public const string Id = "20260704000001_AddListPermissions";

    private static readonly (string Name, string Module, string Description, SystemRole RequiredSystemRole)[] Definitions =
    [
        (PermissionNames.UsersList,   "Users",   "List all users",   SystemRole.TenantAdmin),
        (PermissionNames.RolesList,   "Roles",   "List all roles",   SystemRole.TenantAdmin),
        (PermissionNames.TenantsList, "Tenants", "List all tenants", SystemRole.SystemAdmin),
    ];

    private readonly ApplicationDbContext _context;

    public AddListPermissionsSeed(ApplicationDbContext context)
    {
        _context = context;
    }

    public string SeedId => Id;

    public string Description => "Adds granular List permissions separate from View (detail) permissions.";

    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _context.Permissions
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);

        var existingSet = existing.ToHashSet(StringComparer.Ordinal);

        foreach (var (name, module, description, requiredSystemRole) in Definitions)
        {
            if (existingSet.Contains(name))
                continue;

            _context.Permissions.Add(new Permission
            {
                Id = Guid.NewGuid(),
                Name = name,
                Module = module,
                Description = description,
                RequiredSystemRole = requiredSystemRole,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
