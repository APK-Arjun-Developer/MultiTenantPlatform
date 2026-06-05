namespace Application.DTOs.Auth;

public class ForgotPasswordRequest
{
    public string Email { get; set; } = default!;

    /// <summary>Required for tenant users. Omit for SuperAdmin.</summary>
    public string? TenantSlug { get; set; }
}
