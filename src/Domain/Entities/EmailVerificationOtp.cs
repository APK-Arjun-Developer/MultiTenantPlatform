namespace Domain.Entities;

public class EmailVerificationOtp
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>SHA-256 hash of the 6-digit OTP sent to the user.</summary>
    public string OtpHash { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    public bool IsUsed => UsedAt.HasValue;
}
