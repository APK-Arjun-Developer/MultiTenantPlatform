using Api.Attributes;
using Application.Common;
using Application.DTOs.Roles;
using Application.Interfaces.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/roles")]
[Authorize]
public class RolesController : ApiControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet]
    [HasPermission(PermissionNames.RolesView)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var response = await _roleService.GetRolesAsync(page, pageSize, search);

        return OkEnvelope(response, "Roles retrieved.");
    }

    [HttpGet("{name}")]
    [HasPermission(PermissionNames.RolesView)]
    public async Task<IActionResult> GetByName(string name)
    {
        var response = await _roleService.GetByNameAsync(name);

        return OkEnvelope(response, "Role retrieved.");
    }

    [HttpGet("current")]
    [HasPermission(PermissionNames.RolesView)]
    public async Task<IActionResult> GetCurrent()
    {
        var response = await _roleService.GetCurrentRoleAsync();

        return OkEnvelope(response, "Current role retrieved.");
    }

    [HttpPost]
    [HasPermission(PermissionNames.RolesCreate)]
    public async Task<IActionResult> Create(CreateRoleRequest request)
    {
        var response = await _roleService.CreateRoleAsync(request);

        return StatusCode(StatusCodes.Status201Created, new Api.Contracts.ApiEnvelope<RoleResponse>
        {
            Data = response,
            Message = "Role created.",
            TraceId = HttpContext.TraceIdentifier,
        });
    }

    [HttpPut]
    [HasPermission(PermissionNames.RolesEdit)]
    public async Task<IActionResult> Update(UpdateRoleRequest request)
    {
        var response = await _roleService.UpdateRoleAsync(request);

        return OkEnvelope(response, "Role updated.");
    }

    [HttpDelete("{name}")]
    [HasPermission(PermissionNames.RolesDelete)]
    public async Task<IActionResult> Delete(string name)
    {
        await _roleService.DeleteRoleAsync(new DeleteRoleRequest { Name = name });

        return OkEnvelope("Role deleted.");
    }
}
