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

    public const string TenantsCreate = "Tenants.Create";
    public const string TenantsView = "Tenants.View";
    public const string TenantsEdit = "Tenants.Edit";
    public const string TenantsDelete = "Tenants.Delete";

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
        TenantsCreate,
        TenantsView,
        TenantsEdit,
        TenantsDelete
    ];

    public static readonly IReadOnlyList<string> TenantPermissions =
        All.Where(p => !p.StartsWith("Tenants.", StringComparison.Ordinal)).ToList();
}
