using Domain.Enums;

namespace Application.DTOs.Onboarding;

public class InviteResponse
{
    public Guid InvitationId { get; set; }

    public InvitationType InvitationType { get; set; }

    public DateTime ExpiresAt { get; set; }
}
