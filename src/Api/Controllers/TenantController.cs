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
public class TenantController : ApiControllerBase
{
    private readonly ITenantService _tenantService;

    public TenantController(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    [HttpGet]
    [HasPermission(PermissionNames.TenantsView)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var response = await _tenantService.GetTenantsAsync(page, pageSize);

        return OkEnvelope(response, "Tenants retrieved.");
    }

    [HttpGet("current")]
    [HasPermission(PermissionNames.TenantsView)]
    public async Task<IActionResult> GetCurrent()
    {
        var response = await _tenantService.GetCurrentAsync();

        return OkEnvelope(response, "Current tenant retrieved.");
    }

    [HttpPost]
    [HasPermission(PermissionNames.TenantsCreate)]
    public async Task<IActionResult> Onboard(OnboardTenantRequest request)
    {
        var response = await _tenantService.OnboardTenantAsync(request);

        return OkEnvelope(response, "Tenant onboarded.");
    }

    [HttpPut]
    [HasPermission(PermissionNames.TenantsEdit)]
    public async Task<IActionResult> Update(UpdateTenantRequest request)
    {
        var response = await _tenantService.UpdateAsync(request);

        return OkEnvelope(response, "Tenant updated.");
    }

    [HttpDelete]
    [HasPermission(PermissionNames.TenantsDelete)]
    public async Task<IActionResult> Delete(DeleteTenantRequest request)
    {
        await _tenantService.DeleteAsync(request);

        return OkEnvelope("Tenant deleted.");
    }
}
