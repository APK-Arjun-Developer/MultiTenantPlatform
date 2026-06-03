namespace Domain.Entities;

/// <summary>
/// Single-use hashed token that lets a newly created (inactive) user set their password.
/// Not subject to global tenant/soft-delete query filters — queried via IgnoreQueryFilters on
/// public endpoints where no JWT context exists.
/// </summary>
public class AccountSetupToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>SHA-256 hash of the raw token sent to the user.</summary>
    public string TokenHash { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    public bool IsUsed => UsedAt.HasValue;
}
