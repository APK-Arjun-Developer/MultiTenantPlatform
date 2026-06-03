namespace Application.DTOs.Onboarding;

public class InviteTenantAdminRequest
{
    /// <summary>Existing tenant slug to invite the admin for.</summary>
    public string TenantSlug { get; set; } = default!;

    public string Email { get; set; } = default!;

    /// <summary>Optional predefined role IDs to pre-assign after acceptance.</summary>
    public List<Guid> RoleIds { get; set; } = [];
}
