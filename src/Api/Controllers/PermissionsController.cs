using Api.Attributes;
using Application.Common;
using Application.Interfaces.Permissions;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/permissions")]
[Authorize]
public class PermissionsController : ApiControllerBase
{
    private readonly IPermissionService _permissionService;

    public PermissionsController(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    /// <summary>
    /// Returns the permission catalog visible to the caller.
    /// Use <c>scope</c> to narrow results: TenantUser, TenantAdmin, or System.
    /// System Admin sees all scopes; Tenant Admin sees TenantAdmin + TenantUser.
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionNames.RolesView)]
    public async Task<IActionResult> GetAll(
        [FromQuery] SystemRole? scope = null,
        [FromQuery] bool grouped = false)
    {
        var response = await _permissionService.GetCatalogAsync(scope, grouped);

        return OkEnvelope(response, "Permissions retrieved.");
    }
}
