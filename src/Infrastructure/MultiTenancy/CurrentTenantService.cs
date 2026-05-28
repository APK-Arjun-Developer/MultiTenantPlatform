using Application.Interfaces.Tenant;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.MultiTenancy;

public class CurrentTenantService : ICurrentTenantService
{
    public Guid? TenantId { get; }

    public Guid? UserId { get; }

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

        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(userIdClaim, out var userId))
        {
            UserId = userId;
        }

        Email = user.FindFirst(ClaimTypes.Email)?.Value;
    }
}