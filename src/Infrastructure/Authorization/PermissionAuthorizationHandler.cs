using Application.Authorization;
using Application.Interfaces.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Infrastructure.Authorization;

public class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionRequirement>
{
    private readonly ICurrentUserPermissionService _permissionService;

    public PermissionAuthorizationHandler(
        ICurrentUserPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (await _permissionService.HasPermissionAsync(requirement.PermissionName))
        {
            context.Succeed(requirement);
        }
    }
}
