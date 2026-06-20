namespace Application.DTOs.Auth;

public class ResendEmailOtpRequest
{
    public string Email { get; set; } = default!;

    public string? TenantSlug { get; set; }
}
