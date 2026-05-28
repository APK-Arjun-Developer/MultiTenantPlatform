using Application.DTOs.Tenant;
using Application.Interfaces.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/tenants")]
[Authorize(Roles = "SuperAdmin")]
public class TenantController : ControllerBase
{
    private readonly ITenantService _tenantService;

    public TenantController(
        ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTenantRequest request)
    {
        var response = await _tenantService.CreateAsync(
                request);

        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var response = await _tenantService.GetAllAsync();

        return Ok(response);
    }

    [HttpPost("{tenantId}/admin")]
    public async Task<IActionResult> CreateAdmin(
        Guid tenantId,
        CreateTenantAdminRequest request)
    {
        await _tenantService.CreateTenantAdminAsync(tenantId, request);

        return Ok(new
        {
            Message = "Tenant admin created successfully."
        });
    }
}