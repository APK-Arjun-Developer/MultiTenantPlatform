using Application.DTOs.Tenant;
using Application.Interfaces.Tenant;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/tenant-settings")]
[Authorize]
public class TenantSettingsController : ApiControllerBase
{
    private readonly ITenantService _tenantService;

    public TenantSettingsController(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if (!IsTenantAdmin())
            return Forbid();

        var response = await _tenantService.GetCurrentAsync();
        return OkEnvelope(response, "Tenant settings retrieved.");
    }

    [HttpPut]
    public async Task<IActionResult> Update(UpdateTenantSettingsRequest request)
    {
        if (!IsTenantAdmin())
            return Forbid();

        var response = await _tenantService.UpdateTenantSettingsAsync(request);
        return OkEnvelope(response, "Tenant settings updated.");
    }

    [HttpPost("logo")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadLogo(IFormFile file)
    {
        if (!IsTenantAdmin())
            return Forbid();

        var response = await _tenantService.UploadTenantLogoAsync(file);
        return OkEnvelope(response, "Company logo updated.");
    }

    [HttpDelete("logo")]
    public async Task<IActionResult> RemoveLogo()
    {
        if (!IsTenantAdmin())
            return Forbid();

        var response = await _tenantService.RemoveTenantLogoAsync();
        return OkEnvelope(response, "Company logo removed.");
    }

    private bool IsTenantAdmin()
    {
        var systemRoleClaim = User.FindFirstValue("system_role");
        return int.TryParse(systemRoleClaim, out var roleValue)
               && (SystemRole)roleValue == SystemRole.TenantAdmin;
    }
}
