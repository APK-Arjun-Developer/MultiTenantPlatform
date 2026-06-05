using Domain.Enums;

namespace Application.DTOs.Invitations;

public class InvitationListItemResponse
{
    public Guid Id { get; set; }

    public string Email { get; set; } = default!;

    public InvitationType InvitationType { get; set; }

    public Guid TenantId { get; set; }

    public string? TenantName { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public bool IsExpired { get; set; }

    public bool IsAccepted { get; set; }

    public bool IsRevoked { get; set; }

    public string Status { get; set; } = default!;

    public DateTime CreatedAt { get; set; }

    public Guid InvitedByUserId { get; set; }
}
