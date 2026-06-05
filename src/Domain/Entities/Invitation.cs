using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Invitation record for tenant-admin or tenant-user onboarding via email link.
/// Not subject to global query filters — queried via IgnoreQueryFilters on public endpoints.
/// </summary>
public class Invitation
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Email { get; set; } = default!;

    public InvitationType InvitationType { get; set; }

    /// <summary>JSON-serialized list of role GUIDs to assign on acceptance.</summary>
    public string RoleIdsJson { get; set; } = "[]";

    /// <summary>SHA-256 hash of the raw invitation token.</summary>
    public string TokenHash { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public Guid InvitedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    public bool IsAccepted => AcceptedAt.HasValue;

    public bool IsRevoked => RevokedAt.HasValue;
}
