namespace Domain.Entities;

/// <summary>
/// Single-use hashed token for password reset flow.
/// Follows the same pattern as AccountSetupToken — raw token sent by email, hash stored in DB.
/// </summary>
public class PasswordResetToken
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
