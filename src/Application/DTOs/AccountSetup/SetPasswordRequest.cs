using Application.DTOs.Common;

namespace Application.DTOs.AccountSetup;

public class SetPasswordRequest
{
    public string Token { get; set; } = default!;

    public string Password { get; set; } = default!;

    public string ConfirmPassword { get; set; } = default!;

    /// <summary>If provided, update the user's display name during account setup.</summary>
    public string? FullName { get; set; }

    /// <summary>Optional address to save when activating the account.</summary>
    public AddressRequest? Address { get; set; }
}
