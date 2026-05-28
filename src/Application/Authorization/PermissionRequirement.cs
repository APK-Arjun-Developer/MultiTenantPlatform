using Microsoft.AspNetCore.Authorization;

namespace Application.Authorization;

public class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permissionName)
    {
        PermissionName = permissionName;
    }

    public string PermissionName { get; }
}
