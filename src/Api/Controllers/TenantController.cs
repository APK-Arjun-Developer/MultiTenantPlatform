using Api.Attributes;
using Application.Common;
using Application.DTOs.Tenant;
using Application.Interfaces.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/tenants")]
[Authorize]
public class TenantController : ControllerBase
{
    private readonly ITenantService _tenantService;

    public TenantController(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    [HttpGet]
    [HasPermission(PermissionNames.TenantsView)]
    public async Task<IActionResult> GetAll()
    {
        var response = await _tenantService.GetAllAsync();

        return Ok(response);
    }

    [HttpGet("current")]
    [HasPermission(PermissionNames.TenantsView)]
    public async Task<IActionResult> GetCurrent()
    {
        var response = await _tenantService.GetCurrentAsync();

        return Ok(response);
    }

    [HttpPost]
    [HasPermission(PermissionNames.TenantsCreate)]
    public async Task<IActionResult> Onboard(OnboardTenantRequest request)
    {
        var response = await _tenantService.OnboardTenantAsync(request);

        return Ok(response);
    }

    [HttpPut]
    [HasPermission(PermissionNames.TenantsEdit)]
    public async Task<IActionResult> Update(UpdateTenantRequest request)
    {
        var response = await _tenantService.UpdateAsync(request);

        return Ok(response);
    }

    [HttpDelete]
    [HasPermission(PermissionNames.TenantsDelete)]
    public async Task<IActionResult> Delete(DeleteTenantRequest request)
    {
        await _tenantService.DeleteAsync(request);

        return NoContent();
    }
}
