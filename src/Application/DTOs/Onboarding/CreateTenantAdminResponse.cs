namespace Application.DTOs.Onboarding;

public class CreateTenantAdminResponse
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = default!;

    public string Email { get; set; } = default!;

    public Guid TenantId { get; set; }

    public string TenantSlug { get; set; } = default!;

    public IReadOnlyList<string> Roles { get; set; } = [];

    public bool IsActive { get; set; }

    public string SetupUrl { get; set; } = default!;
}
