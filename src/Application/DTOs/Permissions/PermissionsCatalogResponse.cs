namespace Application.DTOs.Permissions;

public class PermissionsCatalogResponse
{
    public IReadOnlyList<PermissionResponse> Items { get; set; } = [];

    public IReadOnlyList<PermissionModuleGroupResponse>? ByModule { get; set; }
}
