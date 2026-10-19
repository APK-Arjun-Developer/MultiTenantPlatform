using Application.DTOs.Tenant;
using Application.DTOs.TenantConfig;
using Application.Interfaces.Tenant;
using Application.Interfaces.TenantConfig;
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
    private readonly ITenantConfigService _tenantConfigService;

    public TenantSettingsController(
        ITenantService tenantService,
        ITenantConfigService tenantConfigService)
    {
        _tenantService = tenantService;
        _tenantConfigService = tenantConfigService;
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

    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(CancellationToken cancellationToken)
    {
        if (!IsTenantAdmin())
            return Forbid();

        var response = await _tenantConfigService.GetCurrentAsync(cancellationToken);
        return OkEnvelope(response, "Tenant config retrieved.");
    }

    [HttpPut("config")]
    public async Task<IActionResult> UpdateConfig(
        UpdateCurrentTenantConfigRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsTenantAdmin())
            return Forbid();

        var response = await _tenantConfigService.UpdateCurrentAsync(request, cancellationToken);
        return OkEnvelope(response, "Tenant config updated.");
    }

    private bool IsTenantAdmin()
    {
        var systemRoleClaim = User.FindFirstValue("system_role");
        return int.TryParse(systemRoleClaim, out var roleValue)
               && (SystemRole)roleValue == SystemRole.TenantAdmin;
    }
}
