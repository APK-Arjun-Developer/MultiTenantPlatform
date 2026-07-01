using Infrastructure.Caching;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace Api.Middleware;

/// <summary>
/// Rejects authenticated requests where the user or their tenant is inactive/deleted.
/// Runs after UseAuthentication() so HttpContext.User is fully populated before any
/// scoped service (ApplicationDbContext → CurrentTenantService) is constructed.
/// </summary>
public sealed class UserStatusMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;

    public UserStatusMiddleware(RequestDelegate next, IMemoryCache cache)
    {
        _next = next;
        _cache = cache;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var userIdClaim = context.User.FindFirst("user_id")?.Value;
        var tenantIdClaim = context.User.FindFirst("tenant_id")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            await _next(context);
            return;
        }

        // ── User active check ─────────────────────────────────────────────────
        if (!_cache.TryGetValue(CacheKeys.UserStatus(userId), out bool isUserActive))
        {
            var status = await db.Users
                .IgnoreQueryFilters()
                .Where(u => u.Id == userId)
                .Select(u => new { u.IsActive, u.DeletedAt })
                .FirstOrDefaultAsync(context.RequestAborted);

            isUserActive = status != null && status.IsActive && status.DeletedAt == null;
            _cache.Set(CacheKeys.UserStatus(userId), isUserActive, TimeSpan.FromMinutes(5));
        }

        if (!isUserActive)
        {
            await WriteInactiveResponseAsync(
                context,
                "Your account has been deactivated. Please contact your administrator.",
                "user_inactive");
            return;
        }

        // ── Tenant active check (skip for SystemAdmin) ────────────────────────
        if (Guid.TryParse(tenantIdClaim, out var tenantId) && tenantId != Guid.Empty)
        {
            if (!_cache.TryGetValue(CacheKeys.TenantStatus(tenantId), out bool isTenantActive))
            {
                var tenantStatus = await db.Tenants
                    .IgnoreQueryFilters()
                    .Where(t => t.Id == tenantId)
                    .Select(t => new { t.IsActive, t.DeletedAt })
                    .FirstOrDefaultAsync(context.RequestAborted);

                isTenantActive = tenantStatus != null
                    && tenantStatus.IsActive
                    && tenantStatus.DeletedAt == null;

                _cache.Set(CacheKeys.TenantStatus(tenantId), isTenantActive, TimeSpan.FromMinutes(5));
            }

            if (!isTenantActive)
            {
                await WriteInactiveResponseAsync(
                    context,
                    "Your organization account has been deactivated. Please contact support.",
                    "tenant_inactive");
                return;
            }
        }

        await _next(context);
    }

    private static Task WriteInactiveResponseAsync(HttpContext context, string message, string code)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";

        return context.Response.WriteAsync(
            JsonSerializer.Serialize(new
            {
                data = (object?)null,
                message,
                errors = new { code },
                traceId = context.TraceIdentifier,
            }),
            context.RequestAborted);
    }
}
