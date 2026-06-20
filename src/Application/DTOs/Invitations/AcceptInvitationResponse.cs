using Domain.Enums;

namespace Application.DTOs.Invitations;

public class AcceptInvitationResponse
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = default!;

    public string FullName { get; set; } = default!;

    public Guid TenantId { get; set; }

    public string? TenantSlug { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = [];

    public InvitationType InvitationType { get; set; }

    public bool IsActive { get; set; }
}
