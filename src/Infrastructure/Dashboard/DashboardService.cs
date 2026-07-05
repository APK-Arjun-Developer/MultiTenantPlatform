using Application.DTOs.Dashboard;
using Application.Interfaces.Dashboard;
using Application.Interfaces.Tenant;
using Domain.Enums;
using Infrastructure.Common;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Domain.Entities;

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

            var freePlanTenants = await _context.Tenants
                .IgnoreQueryFilters()
                .CountAsync(t => t.DeletedAt == null && t.PlanType == PlanType.Free, cancellationToken);

            var proPlanTenants = await _context.Tenants
                .IgnoreQueryFilters()
                .CountAsync(t => t.DeletedAt == null && t.PlanType == PlanType.Pro, cancellationToken);

            return new DashboardStatsResponse
            {
                TotalTenants = totalTenants,
                TotalTenantAdmins = totalTenantAdmins,
                TotalTenantUsers = totalTenantUsers,
                FreePlanTenants = freePlanTenants,
                ProPlanTenants = proPlanTenants,
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

        var roleCount = await _context.Roles
            .IgnoreQueryFilters()
            .CountAsync(r => r.TenantId == tenantId && r.DeletedAt == null, cancellationToken);

        var now = DateTime.UtcNow;

        var pendingInvitationCount = await _context.Invitations
            .CountAsync(
                i => i.TenantId == tenantId
                     && i.AcceptedAt == null
                     && i.RevokedAt == null
                     && i.ExpiresAt > now,
                cancellationToken);

        var acceptedInvitationCount = await _context.Invitations
            .CountAsync(i => i.TenantId == tenantId && i.AcceptedAt != null, cancellationToken);

        var expiredInvitationCount = await _context.Invitations
            .CountAsync(
                i => i.TenantId == tenantId
                     && i.AcceptedAt == null
                     && i.RevokedAt == null
                     && i.ExpiresAt <= now,
                cancellationToken);

        var revokedInvitationCount = await _context.Invitations
            .CountAsync(i => i.TenantId == tenantId && i.RevokedAt != null, cancellationToken);

        return new DashboardStatsResponse
        {
            TotalTenants = null,
            TotalTenantAdmins = null,
            TotalTenantUsers = tenantUserCount,
            TotalRoles = roleCount,
            TotalPendingInvitations = pendingInvitationCount,
            AcceptedInvitations = acceptedInvitationCount,
            ExpiredInvitations = expiredInvitationCount,
            RevokedInvitations = revokedInvitationCount,
        };
    }
}
