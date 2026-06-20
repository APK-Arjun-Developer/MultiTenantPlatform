using Application.Interfaces.Tenant;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.MultiTenancy;

public class CurrentTenantService : ICurrentTenantService
{
    public bool IsSystemAdmin { get; }

    public Guid? TenantId { get; }

    public Guid? UserId { get; }

    public Guid? RoleId { get; }

    public IReadOnlyList<Guid> RoleIds { get; }

    public string? Email { get; }

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var user = httpContext?.User;

        if (user == null)
        {
            RoleIds = [];
            return;
        }

        var tenantIdClaim = user.FindFirst("tenant_id")?.Value;

        if (Guid.TryParse(tenantIdClaim, out var jwtTenantId))
        {
            if (jwtTenantId == Guid.Empty)
            {
                // SystemAdmin: identity is platform-level; effective tenant comes from header.
                IsSystemAdmin = true;

                var headerValue = httpContext?.Request.Headers["X-Tenant-Id"].FirstOrDefault();

                if (Guid.TryParse(headerValue, out var headerTenantId) && headerTenantId != Guid.Empty)
                {
                    TenantId = headerTenantId;
                }
                // No header → TenantId remains null (cross-tenant / platform-wide context).
            }
            else
            {
                TenantId = jwtTenantId;
            }
        }

        var userIdClaim =
            user.FindFirst("user_id")?.Value ??
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            UserId = userId;
        }

        RoleIds = user.FindAll("role_ids")
            .Select(c => Guid.TryParse(c.Value, out var id) ? (Guid?)id : null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        RoleId = RoleIds.Count > 0 ? RoleIds[0] : null;

        Email = user.FindFirst(ClaimTypes.Email)?.Value;
    }
}
