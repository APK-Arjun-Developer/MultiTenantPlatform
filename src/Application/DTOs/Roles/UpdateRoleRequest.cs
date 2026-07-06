namespace Application.DTOs.Roles;

public class UpdateRoleRequest
{
    public string Name { get; set; } = default!;

    /// <summary>Optional new name — when set and different from <see cref="Name"/>, the role is renamed.</summary>
    public string? NewName { get; set; }

    public string? Description { get; set; }

    public List<Guid> Permissions { get; set; } = [];
}
