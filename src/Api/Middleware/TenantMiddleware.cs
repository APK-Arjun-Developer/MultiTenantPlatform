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
                context.Response.StatusCode = 401;

                await context.Response.WriteAsync("Tenant claim missing.");

                return;
            }
        }

        await _next(context);
    }
}