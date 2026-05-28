using Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Api.Attributes;

public class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
    {
        Policy = $"{PermissionPolicyProvider.PolicyPrefix}{permission}";
    }
}
