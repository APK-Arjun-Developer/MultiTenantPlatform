using Application.DTOs.Common;
using Application.DTOs.Invitations;
using Application.DTOs.Onboarding;

namespace Application.Interfaces.Invitations;

public interface IInvitationService
{
    // ── System Admin ──────────────────────────────────────────────────────────

    Task<PagedResponse<InvitationListItemResponse>> GetTenantAdminInvitationsAsync(
        int page, int pageSize, string? status = null,
        CancellationToken cancellationToken = default);

    Task<InviteResponse> InviteTenantAdminAsync(
        InviteTenantAdminRequest request,
        CancellationToken cancellationToken = default);

    // ── Tenant Admin ──────────────────────────────────────────────────────────

    Task<PagedResponse<InvitationListItemResponse>> GetUserInvitationsAsync(
        int page, int pageSize, string? status = null,
        CancellationToken cancellationToken = default);

    Task<InviteResponse> InviteTenantUserAsync(
        InviteTenantUserRequest request,
        CancellationToken cancellationToken = default);

    Task RevokeInvitationAsync(
        Guid invitationId,
        CancellationToken cancellationToken = default);

    // ── Public ────────────────────────────────────────────────────────────────

    Task<ValidateInvitationResponse> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task<AcceptInvitationResponse> AcceptTenantAdminInvitationAsync(
        AcceptTenantAdminInvitationRequest request,
        CancellationToken cancellationToken = default);

    Task<AcceptInvitationResponse> AcceptTenantUserInvitationAsync(
        AcceptTenantUserInvitationRequest request,
        CancellationToken cancellationToken = default);
}
