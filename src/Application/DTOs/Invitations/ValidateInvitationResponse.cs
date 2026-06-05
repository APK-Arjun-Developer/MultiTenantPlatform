using Domain.Enums;

namespace Application.DTOs.Invitations;

public class ValidateInvitationResponse
{
    public bool IsValid { get; set; }

    public string? Email { get; set; }

    public InvitationType? InvitationType { get; set; }

    public string? TenantName { get; set; }

    public string? ErrorMessage { get; set; }
}
