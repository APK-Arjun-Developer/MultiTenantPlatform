namespace Application.Interfaces.Authorization;

public interface ICurrentUserPermissionService
{
    Task<bool> HasPermissionAsync(string permission);

    Task<IReadOnlyList<string>> GetPermissionsAsync();
}
