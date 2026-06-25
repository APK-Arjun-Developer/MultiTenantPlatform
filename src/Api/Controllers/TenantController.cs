using Api.Attributes;
using Application.Common;
using Application.DTOs.Tenant;
using System.Security.Claims;
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
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null)
    {
        var response = await _tenantService.GetTenantsAsync(page, pageSize, search, sortBy, sortOrder);

        return OkEnvelope(response, "Tenants retrieved.");
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionNames.TenantsView)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await _tenantService.GetByIdAsync(id);

        return OkEnvelope(response, "Tenant retrieved.");
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

    [HttpPut("current/address")]
    [Authorize]
    public async Task<IActionResult> UpdateCurrentAddress(UpdateCurrentTenantAddressRequest request)
    {
        var systemRole = User.FindFirstValue("system_role");
        if (systemRole != "2") // 2 = TenantAdmin
            return Forbid();

        var response = await _tenantService.UpdateCurrentTenantAddressAsync(request);

        return OkEnvelope(response, "Company address updated.");
    }

    [HttpDelete]
    [HasPermission(PermissionNames.TenantsDelete)]
    public async Task<IActionResult> Delete(DeleteTenantRequest request)
    {
        await _tenantService.DeleteAsync(request);

        return OkEnvelope("Tenant deleted.");
    }
}
