using Application.Common;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Seed.Seeds;

public sealed class SubscriptionAndAuditPermissionsSeed : IDataSeed
{
    public const string Id = "20260705000001_SubscriptionAndAuditPermissions";

    private static readonly (string Name, string Module, string Description, SystemRole RequiredSystemRole)[] Definitions =
    [
        (PermissionNames.AuditLogsView,      "AuditLogs",     "View activity audit logs",      SystemRole.TenantAdmin),
        (PermissionNames.SubscriptionsView,  "Subscriptions", "View subscription plans",       SystemRole.SystemAdmin),
        (PermissionNames.SubscriptionsEdit,  "Subscriptions", "Change tenant subscription plan", SystemRole.SystemAdmin),
    ];

    private readonly ApplicationDbContext _context;

    public SubscriptionAndAuditPermissionsSeed(ApplicationDbContext context)
    {
        _context = context;
    }

    public string SeedId => Id;

    public string Description => "Subscription and audit log permissions.";

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
