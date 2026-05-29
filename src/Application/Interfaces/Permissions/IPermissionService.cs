using Application.DTOs.Permissions;

namespace Application.Interfaces.Permissions;

public interface IPermissionService
{
    Task<PermissionsCatalogResponse> GetCatalogAsync(bool groupByModule = false);
}
