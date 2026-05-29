namespace Application.DTOs.Permissions;

public class PermissionModuleGroupResponse
{
    public string Module { get; set; } = default!;

    public IReadOnlyList<PermissionResponse> Permissions { get; set; } = [];
}
