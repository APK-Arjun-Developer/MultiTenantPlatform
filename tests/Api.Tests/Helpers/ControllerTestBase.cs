using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Tests.Helpers;

public abstract class ControllerTestBase
{
    protected static ControllerContext BuildContext(
        string? userId = null,
        string? tenantId = null,
        string systemRole = "3",
        string email = "user@example.com")
    {
        var claims = new List<Claim>
        {
            new("user_id", userId ?? Guid.NewGuid().ToString()),
            new("tenant_id", tenantId ?? Guid.NewGuid().ToString()),
            new("system_role", systemRole),
            new(ClaimTypes.Email, email),
            new("full_name", "Test User"),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        return new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } };
    }

    protected static ControllerContext SystemAdminContext(string? userId = null)
        => BuildContext(userId, Guid.Empty.ToString(), "1", "admin@system.com");

    protected static ControllerContext TenantAdminContext(string? tenantId = null)
        => BuildContext(null, tenantId ?? Guid.NewGuid().ToString(), "2", "admin@tenant.com");

    protected static ControllerContext TenantUserContext(string? tenantId = null)
        => BuildContext(null, tenantId ?? Guid.NewGuid().ToString(), "3");

    protected static ControllerContext BuildContextWithCookie(
        string cookieName,
        string cookieValue,
        string systemRole = "3",
        string? tenantId = null)
    {
        var claims = new List<Claim>
        {
            new("user_id", Guid.NewGuid().ToString()),
            new("tenant_id", tenantId ?? Guid.NewGuid().ToString()),
            new("system_role", systemRole),
            new(ClaimTypes.Email, "user@example.com"),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.Request.Headers["Cookie"] = $"{cookieName}={cookieValue}";
        return new ControllerContext { HttpContext = httpContext };
    }

    protected static ControllerContext EmptyClaimsContext()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        return new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } };
    }
}
