namespace Application.DTOs.Tenant;

public class OnboardTenantRequest
{
    public OnboardUserDetails User { get; set; } = default!;

    public OnboardTenantDetails Tenant { get; set; } = default!;

    public List<OnboardRoleDetails> Roles { get; set; } = [];
}

public class OnboardUserDetails
{
    public string FullName { get; set; } = default!;

    public string Email { get; set; } = default!;
}

public class OnboardTenantDetails
{
    public string Name { get; set; } = default!;

    public string Slug { get; set; } = default!;
}

public class OnboardRoleDetails
{
    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public List<Guid> Permissions { get; set; } = [];
}
