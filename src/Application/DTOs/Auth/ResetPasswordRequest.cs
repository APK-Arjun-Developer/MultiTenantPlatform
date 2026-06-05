namespace Application.DTOs.Auth;

public class ResetPasswordRequest
{
    public string Token { get; set; } = default!;

    public string NewPassword { get; set; } = default!;

    public string ConfirmPassword { get; set; } = default!;
}
