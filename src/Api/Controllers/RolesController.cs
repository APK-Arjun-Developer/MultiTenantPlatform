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
    public async Task<IActionResult> GetAll()
    {
        var response = await _roleService.GetRolesAsync();

        return OkEnvelope(response, "Roles retrieved.");
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

        return OkEnvelope(response, "Role created.");
    }

    [HttpPut]
    [HasPermission(PermissionNames.RolesEdit)]
    public async Task<IActionResult> Update(UpdateRoleRequest request)
    {
        var response = await _roleService.UpdateRoleAsync(request);

        return OkEnvelope(response, "Role updated.");
    }

    [HttpDelete]
    [HasPermission(PermissionNames.RolesDelete)]
    public async Task<IActionResult> Delete(DeleteRoleRequest request)
    {
        await _roleService.DeleteRoleAsync(request);

        return OkEnvelope("Role deleted.");
    }
}
