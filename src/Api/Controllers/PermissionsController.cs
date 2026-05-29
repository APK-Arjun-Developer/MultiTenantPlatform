using Api.Attributes;
using Application.Common;
using Application.Interfaces.Permissions;
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

    [HttpGet]
    [HasPermission(PermissionNames.RolesView)]
    public async Task<IActionResult> GetAll([FromQuery] bool grouped = false)
    {
        var response = await _permissionService.GetCatalogAsync(grouped);

        return OkEnvelope(response, "Permissions retrieved.");
    }
}
