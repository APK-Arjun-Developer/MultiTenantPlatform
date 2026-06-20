using Application.Interfaces.Tenant;

namespace Infrastructure.Common;

public abstract class TenantScopedService
{
    protected readonly ICurrentTenantService CurrentTenantService;

    protected TenantScopedService(ICurrentTenantService currentTenantService)
    {
        CurrentTenantService = currentTenantService;
    }

    protected bool IsSystemAdmin() => CurrentTenantService.IsSystemAdmin;

    protected Guid RequireTenantId()
    {
        if (!CurrentTenantService.TenantId.HasValue || CurrentTenantService.TenantId.Value == Guid.Empty)
        {
            var message = CurrentTenantService.IsSystemAdmin
                ? "Tenant context is required. Provide the X-Tenant-Id request header."
                : "Tenant context is required. Ensure tenant_id is present in the JWT.";

            throw new InvalidOperationException(message);
        }

        return CurrentTenantService.TenantId.Value;
    }

    protected Guid RequireUserId()
    {
        return CurrentTenantService.UserId
            ?? throw new InvalidOperationException(
                "User context is required. Ensure user_id is present in the JWT.");
    }
}
