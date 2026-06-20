using Application.DTOs.Permissions;
using Domain.Enums;

namespace Application.Interfaces.Permissions;

public interface IPermissionService
{
    Task<PermissionsCatalogResponse> GetCatalogAsync(SystemRole? scopeFilter = null, bool groupByModule = false);
}
