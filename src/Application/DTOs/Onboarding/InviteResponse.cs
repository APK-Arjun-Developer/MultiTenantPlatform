using Domain.Enums;

namespace Application.DTOs.Onboarding;

public class InviteResponse
{
    public Guid InvitationId { get; set; }

    public string Email { get; set; } = default!;

    public InvitationType InvitationType { get; set; }

    public DateTime ExpiresAt { get; set; }

    /// <summary>The invitation URL (shown in response; also sent via email).</summary>
    public string InvitationUrl { get; set; } = default!;
}
