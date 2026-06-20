namespace Application.DTOs.AccountSetup;

public class SetPasswordResponse
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = default!;

    public string? TenantSlug { get; set; }

    public bool IsActive { get; set; }
}
