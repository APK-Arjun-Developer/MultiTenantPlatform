using Application.DTOs.Onboarding;

namespace Application.Interfaces.Onboarding;

public interface IOnboardingService
{
    // ── System Admin ──────────────────────────────────────────────────────────

    Task<CreateTenantAdminResponse> CreateTenantAdminAsync(
        CreateTenantAdminRequest request,
        CancellationToken cancellationToken = default);

    Task ResendTenantAdminSetupEmailAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<UserStatusResponse> ActivateUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<UserStatusResponse> DeactivateUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    // ── Tenant Admin ──────────────────────────────────────────────────────────

    Task<CreateTenantUserResponse> CreateTenantUserAsync(
        CreateTenantUserRequest request,
        CancellationToken cancellationToken = default);

    Task ResendTenantUserSetupEmailAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
