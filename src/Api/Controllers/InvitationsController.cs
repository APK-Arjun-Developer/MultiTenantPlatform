using Application.DTOs.Invitations;
using Application.Interfaces.Invitations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Controllers;

/// <summary>
/// Public endpoints for the invitation acceptance flow.
/// No authentication required — access is controlled by the invitation token.
/// </summary>
[ApiController]
[Route("api/v1/invitations")]
[AllowAnonymous]
public class InvitationsController : ApiControllerBase
{
    private readonly IInvitationService _invitationService;

    public InvitationsController(IInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    /// <summary>
    /// Validate an invitation token before presenting the registration form.
    /// Returns the invited email, invitation type, and tenant name.
    /// </summary>
    [HttpGet("validate")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Validate(
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        var response = await _invitationService.ValidateTokenAsync(token, cancellationToken);

        return OkEnvelope(response, response.IsValid
            ? "Invitation is valid."
            : "Invitation validation failed.");
    }

    /// <summary>
    /// Accept a TenantAdmin invitation.
    /// The invited user provides their profile and a new password.
    /// The invitation is marked accepted and cannot be reused.
    /// </summary>
    [HttpPost("accept/tenant-admin")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> AcceptTenantAdmin(
        AcceptTenantAdminInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _invitationService.AcceptTenantAdminInvitationAsync(
            request, cancellationToken);

        return OkEnvelope(response, "Invitation accepted. Your account is ready.");
    }

    /// <summary>
    /// Accept a TenantUser invitation.
    /// The invited user provides their profile and a new password.
    /// The invitation is marked accepted and cannot be reused.
    /// </summary>
    [HttpPost("accept/user")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> AcceptTenantUser(
        AcceptTenantUserInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _invitationService.AcceptTenantUserInvitationAsync(
            request, cancellationToken);

        return OkEnvelope(response, "Invitation accepted. Your account is ready.");
    }

    /// <summary>
    /// Accept a new-tenant creation invitation.
    /// The invited user provides tenant details, their profile, and a new password.
    /// A new tenant and tenant admin account are created atomically.
    /// </summary>
    [HttpPost("accept/new-tenant")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> AcceptNewTenant(
        AcceptTenantCreationInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _invitationService.AcceptTenantCreationInvitationAsync(
            request, cancellationToken);

        return OkEnvelope(response, "Invitation accepted. Your tenant and account are ready.");
    }
}
