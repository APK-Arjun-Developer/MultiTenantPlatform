namespace Application.DTOs.Onboarding;

public class CreateTenantAdminRequest
{
    /// <summary>Existing tenant slug to create the admin under.</summary>
    public string TenantSlug { get; set; } = default!;

    public string FullName { get; set; } = default!;

    public string Email { get; set; } = default!;
}
