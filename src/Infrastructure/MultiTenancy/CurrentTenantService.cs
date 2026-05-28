using Application.Interfaces.Tenant;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.MultiTenancy;

public class CurrentTenantService : ICurrentTenantService
{
    public Guid? TenantId { get; }

    public Guid? UserId { get; }

    public Guid? RoleId { get; }

    public string? Email { get; }

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;

        if (user == null)
        {
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

        var roleIdClaim = user.FindFirst("role_id")?.Value;

        if (Guid.TryParse(roleIdClaim, out var roleId))
        {
            RoleId = roleId;
        }

        Email = user.FindFirst(ClaimTypes.Email)?.Value;
    }
}