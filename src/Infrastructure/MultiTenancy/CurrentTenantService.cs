using Application.Interfaces.Tenant;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.MultiTenancy;

public class CurrentTenantService : ICurrentTenantService
{
    public Guid? TenantId { get; }

    public Guid? UserId { get; }

    public Guid? RoleId { get; }

    public IReadOnlyList<Guid> RoleIds { get; }

    public string? Email { get; }

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;

        if (user == null)
        {
            RoleIds = [];
            return;
        }

        var tenantIdClaim = user.FindFirst("tenant_id")?.Value;

        if (Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            TenantId = tenantId;
        }

        var userIdClaim =
            user.FindFirst("user_id")?.Value ??
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            UserId = userId;
        }

        // Read all role_ids claims (one emitted per role at login/refresh).
        RoleIds = user.FindAll("role_ids")
            .Select(c => Guid.TryParse(c.Value, out var id) ? (Guid?)id : null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        RoleId = RoleIds.Count > 0 ? RoleIds[0] : null;

        Email = user.FindFirst(ClaimTypes.Email)?.Value;
    }
}