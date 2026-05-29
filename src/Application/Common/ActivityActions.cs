namespace Application.Common;

public static class ActivityActions
{
    public static class Auth
    {
        public const string Login = "Login";
        public const string Logout = "Logout";
        public const string Refresh = "Refresh";
    }

    public static class Users
    {
        public const string Created = "Created";
        public const string Updated = "Updated";
        public const string Deleted = "Deleted";
    }

    public static class Roles
    {
        public const string Created = "Created";
        public const string Updated = "Updated";
        public const string Deleted = "Deleted";
    }

    public static class Tenants
    {
        public const string Onboarded = "Onboarded";
        public const string Updated = "Updated";
        public const string Deleted = "Deleted";
    }

    public static class Products
    {
        public const string Created = "Created";
        public const string Updated = "Updated";
        public const string Deleted = "Deleted";
    }

    public static class Files
    {
        public const string Uploaded = "Uploaded";
        public const string Deleted = "Deleted";
    }
}
