namespace Application.DTOs.Auth;

public class VerifyEmailOtpRequest
{
    public string Email { get; set; } = default!;

    public string? TenantSlug { get; set; }

    public string Otp { get; set; } = default!;
}
