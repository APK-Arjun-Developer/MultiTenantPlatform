using Application.Common;
using Application.DTOs.Permissions;
using Application.Interfaces.Permissions;
using Application.Interfaces.Tenant;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Permissions;

public class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;

    public PermissionService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService)
    {
        _context = context;
        _currentTenantService = currentTenantService;
    }

    public async Task<PermissionsCatalogResponse> GetCatalogAsync(bool groupByModule = false)
    {
        var query = _context.Permissions.AsNoTracking();

        if (!IsSystemAdmin())
        {
            var allowed = PermissionNames.TenantPermissions.ToHashSet(StringComparer.Ordinal);
            query = query.Where(p => allowed.Contains(p.Name));
        }

        var items = await query
            .OrderBy(p => p.Module)
            .ThenBy(p => p.Name)
            .Select(p => new PermissionResponse
            {
                Id = p.Id,
                Name = p.Name,
                Module = p.Module,
                Description = p.Description,
            })
            .ToListAsync();

        var response = new PermissionsCatalogResponse
        {
            Items = items,
        };

        if (groupByModule)
        {
            response.ByModule = items
                .GroupBy(p => p.Module)
                .Select(g => new PermissionModuleGroupResponse
                {
                    Module = g.Key,
                    Permissions = g.ToList(),
                })
                .ToList();
        }

        return response;
    }

    private bool IsSystemAdmin() =>
        (_currentTenantService.TenantId ?? Guid.Empty) == Guid.Empty;
}
