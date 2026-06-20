using Domain.Enums;

namespace Application.Common;

public static class PermissionNames
{
    public const string UsersCreate = "Users.Create";
    public const string UsersView = "Users.View";
    public const string UsersEdit = "Users.Edit";
    public const string UsersDelete = "Users.Delete";

    public const string RolesCreate = "Roles.Create";
    public const string RolesView = "Roles.View";
    public const string RolesEdit = "Roles.Edit";
    public const string RolesDelete = "Roles.Delete";

    public const string ProductsCreate = "Products.Create";
    public const string ProductsView = "Products.View";
    public const string ProductsEdit = "Products.Edit";
    public const string ProductsDelete = "Products.Delete";

    public const string ReportsView = "Reports.View";
    public const string ReportsExport = "Reports.Export";

    public const string FilesView = "Files.View";
    public const string FilesUpload = "Files.Upload";
    public const string FilesDelete = "Files.Delete";

    public const string TenantsCreate = "Tenants.Create";
    public const string TenantsView = "Tenants.View";
    public const string TenantsEdit = "Tenants.Edit";
    public const string TenantsDelete = "Tenants.Delete";

    public const string OnboardingCreate = "Onboarding.Create";
    public const string OnboardingInvite = "Onboarding.Invite";
    public const string OnboardingResend = "Onboarding.Resend";
    public const string OnboardingRevoke = "Onboarding.Revoke";
    public const string OnboardingActivate = "Onboarding.Activate";
    public const string OnboardingDeactivate = "Onboarding.Deactivate";

    public static readonly IReadOnlyList<string> All =
    [
        UsersCreate,
        UsersView,
        UsersEdit,
        UsersDelete,
        RolesCreate,
        RolesView,
        RolesEdit,
        RolesDelete,
        ProductsCreate,
        ProductsView,
        ProductsEdit,
        ProductsDelete,
        ReportsView,
        ReportsExport,
        FilesView,
        FilesUpload,
        FilesDelete,
        TenantsCreate,
        TenantsView,
        TenantsEdit,
        TenantsDelete,
        OnboardingCreate,
        OnboardingInvite,
        OnboardingResend,
        OnboardingRevoke,
        OnboardingActivate,
        OnboardingDeactivate,
    ];

    // Maps each permission to the minimum SystemRole that can be assigned it.
    // SystemAdmin=1 (highest), TenantAdmin=2, TenantUser=3 (lowest).
    public static readonly IReadOnlyDictionary<string, SystemRole> Scopes =
        new Dictionary<string, SystemRole>
        {
            // TenantUser — basic operational permissions
            [ProductsCreate] = SystemRole.TenantUser,
            [ProductsView]   = SystemRole.TenantUser,
            [ProductsEdit]   = SystemRole.TenantUser,
            [ProductsDelete] = SystemRole.TenantUser,
            [ReportsView]    = SystemRole.TenantUser,
            [ReportsExport]  = SystemRole.TenantUser,
            [FilesView]      = SystemRole.TenantUser,
            [FilesUpload]    = SystemRole.TenantUser,

            // TenantAdmin — tenant management permissions
            [UsersCreate]          = SystemRole.TenantAdmin,
            [UsersView]            = SystemRole.TenantAdmin,
            [UsersEdit]            = SystemRole.TenantAdmin,
            [UsersDelete]          = SystemRole.TenantAdmin,
            [RolesCreate]          = SystemRole.TenantAdmin,
            [RolesView]            = SystemRole.TenantAdmin,
            [RolesEdit]            = SystemRole.TenantAdmin,
            [RolesDelete]          = SystemRole.TenantAdmin,
            [FilesDelete]          = SystemRole.TenantAdmin,
            [OnboardingCreate]     = SystemRole.TenantAdmin,
            [OnboardingInvite]     = SystemRole.TenantAdmin,
            [OnboardingResend]     = SystemRole.TenantAdmin,
            [OnboardingRevoke]     = SystemRole.TenantAdmin,
            [OnboardingActivate]   = SystemRole.TenantAdmin,
            [OnboardingDeactivate] = SystemRole.TenantAdmin,

            // SystemAdmin — platform-level permissions
            [TenantsCreate] = SystemRole.SystemAdmin,
            [TenantsView]   = SystemRole.SystemAdmin,
            [TenantsEdit]   = SystemRole.SystemAdmin,
            [TenantsDelete] = SystemRole.SystemAdmin,
        };

    // All permissions a Tenant Admin can see or assign (TenantAdmin + TenantUser scopes).
    // SystemRole values: SystemAdmin=1 < TenantAdmin=2 < TenantUser=3, so >= TenantAdmin excludes System-only.
    public static readonly IReadOnlyList<string> TenantPermissions =
        All.Where(p => Scopes.TryGetValue(p, out var s) && s >= SystemRole.TenantAdmin).ToList();

    // Subset assignable to Tenant User roles only.
    public static readonly IReadOnlyList<string> TenantUserPermissions =
        All.Where(p => Scopes.TryGetValue(p, out var s) && s == SystemRole.TenantUser).ToList();
}
