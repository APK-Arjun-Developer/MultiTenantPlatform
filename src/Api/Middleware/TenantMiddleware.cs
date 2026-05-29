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
            var tenantId = context.User.FindFirst("tenant_id")?.Value;

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new UnauthorizedAccessException("Tenant claim missing.");
            }
        }

        await _next(context);
    }
}