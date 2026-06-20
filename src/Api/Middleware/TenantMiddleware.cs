namespace Api.Middleware;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantIdValue = context.User.FindFirst("tenant_id")?.Value;

            if (string.IsNullOrWhiteSpace(tenantIdValue))
            {
                throw new UnauthorizedAccessException("Tenant claim missing.");
            }

            if (!Guid.TryParse(tenantIdValue, out _))
            {
                throw new UnauthorizedAccessException("Tenant claim is invalid.");
            }

            // SystemAdmin may optionally scope a request to a specific tenant via this header.
            var xTenantId = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(xTenantId) && !Guid.TryParse(xTenantId, out _))
            {
                throw new UnauthorizedAccessException("X-Tenant-Id header value is not a valid GUID.");
            }
        }

        await _next(context);
    }
}
