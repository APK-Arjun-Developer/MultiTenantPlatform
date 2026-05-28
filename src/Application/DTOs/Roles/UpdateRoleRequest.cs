namespace Application.DTOs.Roles;

public class UpdateRoleRequest
{
    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public List<Guid> Permissions { get; set; } = [];
}
