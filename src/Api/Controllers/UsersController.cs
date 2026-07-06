using Api.Attributes;
using Application.Common;
using Application.DTOs.Onboarding;
using Application.DTOs.Users;
using Application.Interfaces.Invitations;
using Application.Interfaces.Onboarding;
using Application.Interfaces.Users;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ApiControllerBase
{
    private readonly IUserManagementService _userManagementService;
    private readonly IOnboardingService _onboardingService;
    private readonly IInvitationService _invitationService;

    public UsersController(
        IUserManagementService userManagementService,
        IOnboardingService onboardingService,
        IInvitationService invitationService)
    {
        _userManagementService = userManagementService;
        _onboardingService = onboardingService;
        _invitationService = invitationService;
    }

    [HttpGet]
    [HasPermission(PermissionNames.UsersList)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] CreatedVia? createdVia = null)
    {
        var response = await _userManagementService.GetUsersAsync(page, pageSize, search, sortBy, sortOrder, isActive, createdVia);

        return OkEnvelope(response, "Users retrieved.");
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionNames.UsersView)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await _userManagementService.GetByIdAsync(id);

        return OkEnvelope(response, "User retrieved.");
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent()
    {
        var response = await _userManagementService.GetCurrentUserAsync();

        return OkEnvelope(response, "Current user retrieved.");
    }

    [HttpPost]
    [HasPermission(PermissionNames.UsersCreate)]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        var response = await _userManagementService.CreateUserAsync(request);

        return OkEnvelope(response, "User created.");
    }

    [HttpPost("current/change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        await _userManagementService.ChangePasswordAsync(request);

        return OkEnvelope("Password changed successfully.");
    }

    [HttpPut("current")]
    public async Task<IActionResult> UpdateCurrent(UpdateCurrentUserRequest request)
    {
        var response = await _userManagementService.UpdateCurrentUserAsync(request);

        return OkEnvelope(response, "Profile updated.");
    }

    [HttpPost("current/avatar")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadAvatar(IFormFile avatar)
    {
        var response = await _userManagementService.UploadCurrentUserAvatarAsync(avatar);

        return OkEnvelope(response, "Profile picture uploaded.");
    }

    [HttpDelete("current/avatar")]
    public async Task<IActionResult> RemoveAvatar()
    {
        var response = await _userManagementService.RemoveCurrentUserAvatarAsync();

        return OkEnvelope(response, "Profile picture removed.");
    }

    [HttpGet("{id:guid}/avatar")]
    public async Task<IActionResult> GetAvatar(Guid id)
    {
        var result = await _userManagementService.GetUserAvatarAsync(id);

        if (result == null)
            return NotFound();

        var (stream, contentType, fileName) = result.Value;

        return File(stream, contentType, fileName);
    }

    [HttpPut]
    [HasPermission(PermissionNames.UsersEdit)]
    public async Task<IActionResult> Update(UpdateUserRequest request)
    {
        var response = await _userManagementService.UpdateUserAsync(request);

        return OkEnvelope(response, "User updated.");
    }

    [HttpDelete]
    [HasPermission(PermissionNames.UsersDelete)]
    public async Task<IActionResult> Delete(DeleteUserRequest request)
    {
        await _userManagementService.DeleteUserAsync(request);

        return OkEnvelope("User deleted.");
    }

    // ── Onboarding: Tenant Admin creates Tenant User ──────────────────────────

    /// <summary>
    /// Directly create a tenant user. Generates an account-setup token and sends a
    /// setup email — the account remains inactive until the user sets their password.
    /// </summary>
    [HttpPost("direct-create")]
    [HasPermission(PermissionNames.OnboardingCreate)]
    public async Task<IActionResult> DirectCreate(
        CreateTenantUserRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _onboardingService.CreateTenantUserAsync(request, cancellationToken);

        return OkEnvelope(response, "User created. Setup email sent.");
    }

    /// <summary>
    /// Send an invitation email to a prospective tenant user.
    /// The invited user self-registers via the link in the email.
    /// </summary>
    [HttpPost("invite")]
    [HasPermission(PermissionNames.OnboardingInvite)]
    public async Task<IActionResult> Invite(
        InviteTenantUserRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _invitationService.InviteTenantUserAsync(request, cancellationToken);

        return OkEnvelope(response, "User invitation sent.");
    }

    /// <summary>
    /// Resend the account-setup email for an inactive tenant user.
    /// Issues a fresh token, invalidating the previous one.
    /// </summary>
    [HttpPost("{userId:guid}/resend")]
    [HasPermission(PermissionNames.OnboardingResend)]
    public async Task<IActionResult> ResendSetupEmail(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _onboardingService.ResendTenantUserSetupEmailAsync(userId, cancellationToken);

        return OkEnvelope("Setup email resent.");
    }

    [HttpGet("invitations")]
    [HasPermission(PermissionNames.OnboardingInvite)]
    public async Task<IActionResult> GetInvitations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var response = await _invitationService.GetUserInvitationsAsync(page, pageSize, status, cancellationToken);

        return OkEnvelope(response, "Invitations retrieved.");
    }

    /// <summary>Revoke a pending tenant user invitation.</summary>
    [HttpPost("invitations/{invitationId:guid}/revoke")]
    [HasPermission(PermissionNames.OnboardingRevoke)]
    public async Task<IActionResult> RevokeInvitation(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        await _invitationService.RevokeInvitationAsync(invitationId, cancellationToken);

        return OkEnvelope("Invitation revoked.");
    }

    /// <summary>Resend a pending tenant user invitation with a fresh token.</summary>
    [HttpPost("invitations/{invitationId:guid}/resend")]
    [HasPermission(PermissionNames.OnboardingResend)]
    public async Task<IActionResult> ResendInvitation(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        await _invitationService.ResendInvitationAsync(invitationId, cancellationToken);

        return OkEnvelope("Invitation resent.");
    }

    /// <summary>Activate a tenant user account.</summary>
    [HttpPost("{userId:guid}/activate")]
    [HasPermission(PermissionNames.OnboardingActivate)]
    public async Task<IActionResult> Activate(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var response = await _onboardingService.ActivateUserAsync(userId, cancellationToken);

        return OkEnvelope(response, "User activated.");
    }

    /// <summary>Deactivate a tenant user account.</summary>
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
