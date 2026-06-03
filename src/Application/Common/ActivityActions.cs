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

    public static class Onboarding
    {
        public const string TenantAdminCreated = "TenantAdminCreated";
        public const string TenantAdminInvited = "TenantAdminInvited";
        public const string TenantUserCreated = "TenantUserCreated";
        public const string TenantUserInvited = "TenantUserInvited";
        public const string AccountSetupCompleted = "AccountSetupCompleted";
        public const string InvitationAccepted = "InvitationAccepted";
        public const string InvitationRevoked = "InvitationRevoked";
        public const string OnboardingEmailResent = "OnboardingEmailResent";
        public const string UserActivated = "UserActivated";
        public const string UserDeactivated = "UserDeactivated";
    }
}
