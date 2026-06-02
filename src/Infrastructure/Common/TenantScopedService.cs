using Application.Interfaces.Tenant;

namespace Infrastructure.Common;

public abstract class TenantScopedService
{
    protected readonly ICurrentTenantService CurrentTenantService;

    protected TenantScopedService(ICurrentTenantService currentTenantService)
    {
        CurrentTenantService = currentTenantService;
    }

    protected bool IsSystemAdmin() =>
        CurrentTenantService.TenantId.HasValue &&
        CurrentTenantService.TenantId.Value == Guid.Empty;

    protected Guid RequireTenantId()
    {
        var tenantId = CurrentTenantService.TenantId ?? Guid.Empty;

        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Tenant context is required. Ensure tenant_id is present in the JWT.");
        }

        return tenantId;
    }

    protected Guid RequireUserId()
    {
        return CurrentTenantService.UserId
            ?? throw new InvalidOperationException(
                "User context is required. Ensure user_id is present in the JWT.");
    }
}
