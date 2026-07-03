using Application.DTOs.Dashboard;
using Application.Interfaces.Dashboard;
using Application.Interfaces.Tenant;
using Domain.Enums;
using Infrastructure.Common;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Dashboard;

public class DashboardService : TenantScopedService, IDashboardService
{
    private readonly ApplicationDbContext _context;

    public DashboardService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService)
        : base(currentTenantService)
    {
        _context = context;
    }

    public async Task<DashboardStatsResponse> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        if (IsSystemAdmin())
        {
            // Platform-wide counts. Users has no global tenant filter for SystemAdmin,
            // but soft-deleted rows must be excluded explicitly on IgnoreQueryFilters-free paths.
            var totalTenants = await _context.Tenants
                .IgnoreQueryFilters()
                .CountAsync(t => t.DeletedAt == null, cancellationToken);

            var totalTenantAdmins = await _context.Users
                .IgnoreQueryFilters()
                .CountAsync(
                    u => u.SystemRole == SystemRole.TenantAdmin && u.DeletedAt == null,
                    cancellationToken);

            var totalTenantUsers = await _context.Users
                .IgnoreQueryFilters()
                .CountAsync(
                    u => u.SystemRole == SystemRole.TenantUser && u.DeletedAt == null,
                    cancellationToken);

            return new DashboardStatsResponse
            {
                TotalTenants = totalTenants,
                TotalTenantAdmins = totalTenantAdmins,
                TotalTenantUsers = totalTenantUsers,
            };
        }

        var tenantId = RequireTenantId();

        var tenantUserCount = await _context.Users
            .IgnoreQueryFilters()
            .CountAsync(
                u => u.TenantId == tenantId
                     && u.SystemRole == SystemRole.TenantUser
                     && u.DeletedAt == null,
                cancellationToken);

        return new DashboardStatsResponse
        {
            TotalTenants = null,
            TotalTenantAdmins = null,
            TotalTenantUsers = tenantUserCount,
        };
    }
}
