namespace Application.DTOs.AccountSetup;

public class SetPasswordRequest
{
    public string Token { get; set; } = default!;

    public string Password { get; set; } = default!;

    public string ConfirmPassword { get; set; } = default!;
}
