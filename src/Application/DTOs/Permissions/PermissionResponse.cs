namespace Application.DTOs.Permissions;

public class PermissionResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string Module { get; set; } = default!;

    public string Description { get; set; } = default!;
}
