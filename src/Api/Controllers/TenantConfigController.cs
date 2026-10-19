using Application.DTOs.TenantConfig;
using Application.Interfaces.TenantConfig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/tenants")]
[Authorize(Policy = "SystemAdminOnly")]
public class TenantConfigController : ApiControllerBase
{
    private readonly ITenantConfigService _tenantConfigService;

    public TenantConfigController(ITenantConfigService tenantConfigService)
    {
        _tenantConfigService = tenantConfigService;
    }

    [HttpGet("{tenantId:guid}/config")]
    public async Task<IActionResult> Get(Guid tenantId, CancellationToken cancellationToken)
    {
        var response = await _tenantConfigService.GetByTenantIdAsync(tenantId, cancellationToken);
        return OkEnvelope(response, "Tenant config retrieved.");
    }

    [HttpPut("{tenantId:guid}/config")]
    public async Task<IActionResult> Update(
        Guid tenantId,
        UpdateTenantConfigRequest request,
        CancellationToken cancellationToken)
    {
        request.TenantId = tenantId;
        var response = await _tenantConfigService.UpdateByTenantIdAsync(request, cancellationToken);
        return OkEnvelope(response, "Tenant config updated.");
    }
}
