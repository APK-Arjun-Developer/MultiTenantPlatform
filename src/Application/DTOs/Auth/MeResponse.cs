namespace Application.DTOs.Auth;

public class MeResponse
{
    public Guid Id { get; set; }

    public string Email { get; set; } = default!;

    public string FullName { get; set; } = default!;

    public IList<string> Roles { get; set; } = [];

    public string? TenantSlug { get; set; }
}
