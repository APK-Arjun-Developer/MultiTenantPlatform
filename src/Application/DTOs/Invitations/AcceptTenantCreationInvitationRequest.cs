using Application.DTOs.Common;

namespace Application.DTOs.Invitations;

public class AcceptTenantCreationInvitationRequest
{
    public string Token { get; set; } = default!;

    public string FullName { get; set; } = default!;

    public string? Phone { get; set; }

    public string Password { get; set; } = default!;

    public string ConfirmPassword { get; set; } = default!;

    public string TenantName { get; set; } = default!;

    public string TenantSlug { get; set; } = default!;

    public AddressRequest? TenantAddress { get; set; }

    public AddressRequest? UserAddress { get; set; }
}
