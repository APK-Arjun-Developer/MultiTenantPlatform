using Application.Common;
using Domain.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Seed.Seeds;

public sealed class OnboardingPermissionsSeed : IDataSeed
{
    public const string Id = "20260603000003_OnboardingPermissions";

    private static readonly (string Name, string Module, string Description)[] Definitions =
    [
        (PermissionNames.OnboardingCreate,     "Onboarding", "Create users via direct onboarding flow"),
        (PermissionNames.OnboardingInvite,     "Onboarding", "Send invitation emails to new users"),
        (PermissionNames.OnboardingResend,     "Onboarding", "Resend onboarding or setup emails"),
        (PermissionNames.OnboardingRevoke,     "Onboarding", "Revoke pending invitations"),
        (PermissionNames.OnboardingActivate,   "Onboarding", "Activate user accounts"),
        (PermissionNames.OnboardingDeactivate, "Onboarding", "Deactivate user accounts"),
    ];

    private readonly ApplicationDbContext _context;

    public OnboardingPermissionsSeed(ApplicationDbContext context)
    {
        _context = context;
    }

    public string SeedId => Id;

    public string Description => "Onboarding & invitation RBAC permissions.";

    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _context.Permissions
            .Select(p => p.Name)
            .ToListAsync(cancellationToken);

        var existingSet = existing.ToHashSet(StringComparer.Ordinal);

        foreach (var (name, module, description) in Definitions)
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
