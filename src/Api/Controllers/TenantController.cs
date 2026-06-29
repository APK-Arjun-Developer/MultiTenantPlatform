using Api.Attributes;
using Application.Common;
using Application.DTOs.Onboarding;
using Application.DTOs.Tenant;
using Domain.Enums;
using System.Security.Claims;
using Application.Interfaces.Invitations;
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
    private readonly IInvitationService _invitationService;

    public TenantController(
        ITenantService tenantService,
        IInvitationService invitationService)
    {
        _tenantService = tenantService;
        _invitationService = invitationService;
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
        var systemRoleClaim = User.FindFirstValue("system_role");
        if (!int.TryParse(systemRoleClaim, out var roleValue) || (SystemRole)roleValue != SystemRole.TenantAdmin)
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

    [HttpGet("invitations")]
    [HasPermission(PermissionNames.TenantsView)]
    public async Task<IActionResult> GetInvitations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _invitationService.GetTenantCreationInvitationsAsync(
            page, pageSize, status, cancellationToken);

        return OkEnvelope(response, "Tenant creation invitations retrieved.");
    }

    [HttpPost("invite")]
    [HasPermission(PermissionNames.TenantsCreate)]
    public async Task<IActionResult> InviteTenant(
        InviteTenantRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _invitationService.InviteTenantAsync(request, cancellationToken);

        return OkEnvelope(response, "Tenant creation invitation sent.");
    }

    [HttpPost("invitations/{invitationId:guid}/revoke")]
    [HasPermission(PermissionNames.TenantsCreate)]
    public async Task<IActionResult> RevokeInvitation(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        await _invitationService.RevokeInvitationAsync(invitationId, cancellationToken);

        return OkEnvelope("Invitation revoked.");
    }

    [HttpPost("invitations/{invitationId:guid}/resend")]
    [HasPermission(PermissionNames.TenantsCreate)]
    public async Task<IActionResult> ResendInvitation(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        await _invitationService.ResendInvitationAsync(invitationId, cancellationToken);

        return OkEnvelope("Invitation resent.");
    }
}
