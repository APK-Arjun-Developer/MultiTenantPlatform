using Application.Common;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Seed.Seeds;

public sealed class PermissionsSeed : IDataSeed
{
    public const string Id = "20260706000001_Permissions";

    private static readonly (string Name, string Module, string Description, SystemRole RequiredSystemRole)[] Definitions =
    [
        // TenantUser — basic operational permissions
        (PermissionNames.ProfileView,    "Profile",   "View own profile",                       SystemRole.TenantUser),
        (PermissionNames.ProfileEdit,    "Profile",   "Edit own profile and change password",   SystemRole.TenantUser),
        (PermissionNames.FilesView,      "Files",     "View files",                             SystemRole.TenantUser),
        (PermissionNames.FilesUpload,    "Files",     "Upload files",                           SystemRole.TenantUser),

        // TenantAdmin — tenant management permissions
        (PermissionNames.UsersCreate,          "Users",      "Create users",                            SystemRole.TenantAdmin),
        (PermissionNames.UsersList,            "Users",      "List all users",                          SystemRole.TenantAdmin),
        (PermissionNames.UsersView,            "Users",      "View user details",                       SystemRole.TenantAdmin),
        (PermissionNames.UsersEdit,            "Users",      "Edit users",                              SystemRole.TenantAdmin),
        (PermissionNames.UsersDelete,          "Users",      "Delete users",                            SystemRole.TenantAdmin),
        (PermissionNames.RolesCreate,          "Roles",      "Create roles",                            SystemRole.TenantAdmin),
        (PermissionNames.RolesList,            "Roles",      "List all roles",                          SystemRole.TenantAdmin),
        (PermissionNames.RolesView,            "Roles",      "View role details",                       SystemRole.TenantAdmin),
        (PermissionNames.RolesEdit,            "Roles",      "Edit roles",                              SystemRole.TenantAdmin),
        (PermissionNames.RolesDelete,          "Roles",      "Delete roles",                            SystemRole.TenantAdmin),
        (PermissionNames.FilesDelete,          "Files",      "Delete files",                            SystemRole.TenantAdmin),
        (PermissionNames.OnboardingCreate,     "Onboarding", "Create users via direct onboarding flow", SystemRole.TenantAdmin),
        (PermissionNames.OnboardingInvite,     "Onboarding", "Send invitation emails to new users",     SystemRole.TenantAdmin),
        (PermissionNames.OnboardingResend,     "Onboarding", "Resend onboarding or setup emails",       SystemRole.TenantAdmin),
        (PermissionNames.OnboardingRevoke,     "Onboarding", "Revoke pending invitations",              SystemRole.TenantAdmin),
        (PermissionNames.OnboardingActivate,   "Onboarding", "Activate user accounts",                  SystemRole.TenantAdmin),
        (PermissionNames.OnboardingDeactivate, "Onboarding", "Deactivate user accounts",                SystemRole.TenantAdmin),

        // SystemAdmin — platform-level permissions
        (PermissionNames.TenantsCreate,     "Tenants",       "Create tenants",                   SystemRole.SystemAdmin),
        (PermissionNames.TenantsList,       "Tenants",       "List all tenants",                 SystemRole.SystemAdmin),
        (PermissionNames.TenantsView,       "Tenants",       "View tenant details",              SystemRole.SystemAdmin),
        (PermissionNames.TenantsEdit,       "Tenants",       "Edit tenants",                     SystemRole.SystemAdmin),
        (PermissionNames.TenantsDelete,     "Tenants",       "Delete tenants",                   SystemRole.SystemAdmin),
        (PermissionNames.SubscriptionsView, "Subscriptions", "View subscription plans",          SystemRole.SystemAdmin),
        (PermissionNames.SubscriptionsEdit, "Subscriptions", "Change tenant subscription plan",  SystemRole.SystemAdmin),
    ];

    private readonly ApplicationDbContext _context;

    public PermissionsSeed(ApplicationDbContext context)
    {
        _context = context;
    }

    public string SeedId => Id;

    public string Description => "Full RBAC permission catalog.";

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
