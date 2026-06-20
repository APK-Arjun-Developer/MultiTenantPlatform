using Api.Attributes;
using Application.Common;
using Application.DTOs.Onboarding;
using Application.DTOs.Users;
using Application.Interfaces.Invitations;
using Application.Interfaces.Onboarding;
using Application.Interfaces.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// System Admin endpoints for tenant admin management (CRUD + onboarding + invitation).
/// All routes require SystemAdmin authentication.
/// </summary>
[ApiController]
[Route("api/v1/tenant-admins")]
[Authorize]
public class TenantAdminsController : ApiControllerBase
{
    private readonly IOnboardingService _onboardingService;
    private readonly IInvitationService _invitationService;
    private readonly IUserManagementService _userManagementService;

    public TenantAdminsController(
        IOnboardingService onboardingService,
        IInvitationService invitationService,
        IUserManagementService userManagementService)
    {
        _onboardingService = onboardingService;
        _invitationService = invitationService;
        _userManagementService = userManagementService;
    }

    [HttpGet]
    [HasPermission(PermissionNames.TenantsView)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] Guid? tenantId = null)
    {
        var response = await _userManagementService.GetTenantAdminsAsync(page, pageSize, search, tenantId);

        return OkEnvelope(response, "Tenant admins retrieved.");
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionNames.TenantsView)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await _userManagementService.GetTenantAdminByIdAsync(id);

        return OkEnvelope(response, "Tenant admin retrieved.");
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PermissionNames.TenantsEdit)]
    public async Task<IActionResult> Update(Guid id, UpdateTenantAdminRequest request)
    {
        request.UserId = id;
        var response = await _userManagementService.UpdateTenantAdminAsync(request);

        return OkEnvelope(response, "Tenant admin updated.");
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionNames.TenantsDelete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _userManagementService.DeleteTenantAdminAsync(id);

        return OkEnvelope("Tenant admin deleted.");
    }

    /// <summary>
    /// Directly create a tenant admin. Generates an account-setup token and sends a
    /// setup email — the account remains inactive until the user sets their password.
    /// </summary>
    [HttpPost]
    [HasPermission(PermissionNames.OnboardingCreate)]
    public async Task<IActionResult> CreateTenantAdmin(
        CreateTenantAdminRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _onboardingService.CreateTenantAdminAsync(request, cancellationToken);

        return OkEnvelope(response, "Tenant admin created. Setup email sent.");
    }

    /// <summary>
    /// Send an invitation email to a prospective tenant admin.
    /// The invited user self-registers via the link in the email.
    /// </summary>
    [HttpPost("invite")]
    [HasPermission(PermissionNames.OnboardingInvite)]
    public async Task<IActionResult> InviteTenantAdmin(
        InviteTenantAdminRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _invitationService.InviteTenantAdminAsync(request, cancellationToken);

        return OkEnvelope(response, "Tenant admin invitation sent.");
    }

    /// <summary>
    /// Resend the account-setup email for an inactive tenant admin.
    /// Issues a fresh token, invalidating the previous one.
    /// </summary>
    [HttpPost("{userId:guid}/resend")]
    [HasPermission(PermissionNames.OnboardingResend)]
    public async Task<IActionResult> ResendSetupEmail(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _onboardingService.ResendTenantAdminSetupEmailAsync(userId, cancellationToken);

        return OkEnvelope("Setup email resent.");
    }

    [HttpGet("invitations")]
    [HasPermission(PermissionNames.TenantsView)]
    public async Task<IActionResult> GetInvitations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _invitationService.GetTenantAdminInvitationsAsync(page, pageSize, status, cancellationToken);

        return OkEnvelope(response, "Tenant admin invitations retrieved.");
    }

    /// <summary>Revoke a pending tenant admin invitation.</summary>
    [HttpPost("invitations/{invitationId:guid}/revoke")]
    [HasPermission(PermissionNames.OnboardingRevoke)]
    public async Task<IActionResult> RevokeInvitation(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        await _invitationService.RevokeInvitationAsync(invitationId, cancellationToken);

        return OkEnvelope("Invitation revoked.");
    }

    /// <summary>Activate a tenant admin account.</summary>
    [HttpPost("{userId:guid}/activate")]
    [HasPermission(PermissionNames.OnboardingActivate)]
    public async Task<IActionResult> Activate(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var response = await _onboardingService.ActivateUserAsync(userId, cancellationToken);

        return OkEnvelope(response, "User activated.");
    }

    /// <summary>Deactivate a tenant admin account.</summary>
    [HttpPost("{userId:guid}/deactivate")]
    [HasPermission(PermissionNames.OnboardingDeactivate)]
    public async Task<IActionResult> Deactivate(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var response = await _onboardingService.DeactivateUserAsync(userId, cancellationToken);

        return OkEnvelope(response, "User deactivated.");
    }
}
