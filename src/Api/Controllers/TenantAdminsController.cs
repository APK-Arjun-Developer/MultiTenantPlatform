using Api.Attributes;
using Application.Common;
using Application.DTOs.Onboarding;
using Application.Interfaces.Invitations;
using Application.Interfaces.Onboarding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// System Admin endpoints for tenant admin onboarding (direct creation + invitation).
/// All routes require SuperAdmin authentication and TenantsCreate / Onboarding.* permissions.
/// </summary>
[ApiController]
[Route("api/v1/tenant-admins")]
[Authorize]
public class TenantAdminsController : ApiControllerBase
{
    private readonly IOnboardingService _onboardingService;
    private readonly IInvitationService _invitationService;

    public TenantAdminsController(
        IOnboardingService onboardingService,
        IInvitationService invitationService)
    {
        _onboardingService = onboardingService;
        _invitationService = invitationService;
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
