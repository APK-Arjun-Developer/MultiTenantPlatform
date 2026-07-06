namespace Application.DTOs.Tenant;

public class OnboardTenantResponse
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = default!;

    public Guid AdminUserId { get; set; }

    public string AdminEmail { get; set; } = default!;

    public IReadOnlyList<CreatedRoleSummary> Roles { get; set; } = [];
}

public class CreatedRoleSummary
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;
}
