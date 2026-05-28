namespace Application.DTOs.Roles;

public class RoleResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public Guid TenantId { get; set; }

    public IReadOnlyList<Guid> PermissionIds { get; set; } = [];

    public IReadOnlyList<string> PermissionNames { get; set; } = [];
}
