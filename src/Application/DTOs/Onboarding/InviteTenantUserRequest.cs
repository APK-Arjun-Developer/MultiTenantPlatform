namespace Application.DTOs.Onboarding;

public class InviteTenantUserRequest
{
    public string Email { get; set; } = default!;

    public List<Guid> RoleIds { get; set; } = [];
}
