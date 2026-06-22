namespace Application.DTOs.Onboarding;

public class InviteTenantAdminRequest
{
    /// <summary>Existing tenant slug to invite the admin for.</summary>
    public string TenantSlug { get; set; } = default!;

    public string Email { get; set; } = default!;
}
