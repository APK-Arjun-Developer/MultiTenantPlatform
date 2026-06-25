using Application.DTOs.Common;

namespace Application.DTOs.Invitations;

/// <summary>
/// Completes a TenantAdmin invitation. Invited user supplies full profile +
/// company information and chooses a password.
/// </summary>
public class AcceptTenantAdminInvitationRequest
{
    public string Token { get; set; } = default!;

    public string FullName { get; set; } = default!;

    public string? Phone { get; set; }

    public string Password { get; set; } = default!;

    public string ConfirmPassword { get; set; } = default!;

    /// <summary>Optional company / tenant profile information.</summary>
    public CompanyInfo? Company { get; set; }

    /// <summary>Optional personal address to save when accepting the invitation.</summary>
    public AddressRequest? Address { get; set; }
}

public class CompanyInfo
{
    public string? Name { get; set; }

    public string? Website { get; set; }

    public string? Industry { get; set; }
}
