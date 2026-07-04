namespace Application.DTOs.Onboarding;

public class InviteTenantAdminRequest
{
    public Guid TenantId { get; set; }

    public string Email { get; set; } = default!;
}
