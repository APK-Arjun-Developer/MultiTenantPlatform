namespace Application.DTOs.Onboarding;

public class CreateTenantUserResponse
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = default!;

    public string Email { get; set; } = default!;

    public Guid TenantId { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = [];

    public bool IsActive { get; set; }

    public string SetupUrl { get; set; } = default!;
}
