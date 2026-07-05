using Application.Common;
using Application.DTOs.ActivityLogs;
using Application.DTOs.Subscription;
using Application.Exceptions;
using Application.Interfaces.ActivityLogs;
using Application.Interfaces.Subscription;
using Application.Interfaces.Tenant;
using Domain.Enums;
using Infrastructure.Common;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Subscription;

public class SubscriptionService : TenantScopedService, ISubscriptionService
{
    private readonly ApplicationDbContext _context;
    private readonly IActivityLogService _activityLogService;

    public SubscriptionService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        IActivityLogService activityLogService)
        : base(currentTenantService)
    {
        _context = context;
        _activityLogService = activityLogService;
    }

    public IReadOnlyList<SubscriptionPlanDto> GetPlans()
    {
        return
        [
            MapPlan(PlanType.Free),
            MapPlan(PlanType.Pro),
        ];
    }

    public async Task<TenantPlanResponse> UpdateTenantPlanAsync(
        UpdateTenantPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == request.TenantId && t.DeletedAt == null, cancellationToken)
            ?? throw new NotFoundException($"Tenant {request.TenantId} not found.");

        var previousPlan = tenant.PlanType;
        tenant.PlanType = request.PlanType;
        tenant.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(new LogActivityRequest
        {
            UserId = RequireUserId(),
            TenantId = tenant.Id,
            Action = ActivityActions.Subscriptions.PlanChanged,
            Module = ActivityModules.Subscriptions,
            Description = $"Tenant '{tenant.Name}' plan changed from {PlanFeatures.GetName(previousPlan)} to {PlanFeatures.GetName(request.PlanType)}.",
        }, cancellationToken);

        var features = PlanFeatures.Get(request.PlanType);

        return new TenantPlanResponse
        {
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            PlanType = request.PlanType.ToString(),
            PlanName = PlanFeatures.GetName(request.PlanType),
            Features = new PlanFeatureSummary
            {
                MaxUsers = features.MaxUsers,
                MaxStorageMb = features.MaxStorageMb,
                CanAccessReports = features.CanAccessReports,
                CanAccessAdvancedRoles = features.CanAccessAdvancedRoles,
            },
        };
    }

    private static SubscriptionPlanDto MapPlan(PlanType planType)
    {
        var features = PlanFeatures.Get(planType);
        return new SubscriptionPlanDto
        {
            PlanType = planType.ToString(),
            Name = PlanFeatures.GetName(planType),
            Features = new PlanFeatureSummary
            {
                MaxUsers = features.MaxUsers,
                MaxStorageMb = features.MaxStorageMb,
                CanAccessReports = features.CanAccessReports,
                CanAccessAdvancedRoles = features.CanAccessAdvancedRoles,
            },
        };
    }
}
