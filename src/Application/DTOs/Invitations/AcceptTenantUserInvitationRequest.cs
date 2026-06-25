using Application.DTOs.Common;

namespace Application.DTOs.Invitations;

/// <summary>
/// Completes a TenantUser invitation. Invited user supplies basic profile +
/// a password.
/// </summary>
public class AcceptTenantUserInvitationRequest
{
    public string Token { get; set; } = default!;

    public string FullName { get; set; } = default!;

    public string? Phone { get; set; }

    public string Password { get; set; } = default!;

    public string ConfirmPassword { get; set; } = default!;

    /// <summary>Optional personal address to save when accepting the invitation.</summary>
    public AddressRequest? Address { get; set; }
}
