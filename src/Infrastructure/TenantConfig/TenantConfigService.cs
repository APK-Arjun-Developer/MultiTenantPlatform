using Application.Common;
using Application.DTOs.ActivityLogs;
using Application.DTOs.TenantConfig;
using Application.Exceptions;
using Application.Interfaces.ActivityLogs;
using Application.Interfaces.Authentication;
using Application.Interfaces.Tenant;
using Application.Interfaces.TenantConfig;
using Infrastructure.Common;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.TenantConfig;

public class TenantConfigService : TenantScopedService, ITenantConfigService
{
    private readonly ApplicationDbContext _context;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IActivityLogService _activityLogService;

    public TenantConfigService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        IRefreshTokenService refreshTokenService,
        IActivityLogService activityLogService)
        : base(currentTenantService)
    {
        _context = context;
        _refreshTokenService = refreshTokenService;
        _activityLogService = activityLogService;
    }

    public async Task<TenantConfigResponse> GetByTenantIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!IsSystemAdmin())
            throw new ForbiddenException("Only system administrators can access tenant config by ID.");

        var config = await GetOrCreateAsync(tenantId, cancellationToken);
        return MapToResponse(config);
    }

    public async Task<TenantConfigResponse> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var config = await GetOrCreateAsync(tenantId, cancellationToken);
        return MapToResponse(config);
    }

    public async Task<TenantConfigResponse> UpdateByTenantIdAsync(
        UpdateTenantConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsSystemAdmin())
            throw new ForbiddenException("Only system administrators can update tenant config by ID.");

        var config = await GetOrCreateAsync(request.TenantId, cancellationToken);
        var loginWasDisabled = config.LoginEnabled && !request.LoginEnabled;

        Apply(config, request.LoginEnabled, request.WelcomeEmailEnabled,
            request.MaintenanceMode, request.MaintenanceMessage);

        await _context.SaveChangesAsync(cancellationToken);

        if (loginWasDisabled)
            await _refreshTokenService.RevokeAllForTenantAsync(request.TenantId);

        await LogActivityAsync(request.TenantId,
            $"System admin updated config for tenant {request.TenantId}.");

        return MapToResponse(config);
    }

    public async Task<TenantConfigResponse> UpdateCurrentAsync(
        UpdateCurrentTenantConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var config = await GetOrCreateAsync(tenantId, cancellationToken);
        var loginWasDisabled = config.LoginEnabled && !request.LoginEnabled;

        // Maintenance fields are SystemAdmin-only — preserve existing values here.
        Apply(config, request.LoginEnabled, request.WelcomeEmailEnabled,
            config.MaintenanceMode, config.MaintenanceMessage);

        await _context.SaveChangesAsync(cancellationToken);

        if (loginWasDisabled)
            await _refreshTokenService.RevokeAllForTenantAsync(tenantId);

        await LogActivityAsync(tenantId, "Tenant admin updated tenant config.");

        return MapToResponse(config);
    }

    public async Task<Domain.Entities.TenantConfig> GetOrCreateAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var config = await _context.TenantConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken);

        if (config != null)
            return config;

        config = new Domain.Entities.TenantConfig
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LoginEnabled = true,
            WelcomeEmailEnabled = true,
            MaintenanceMode = false,
        };

        _context.TenantConfigs.Add(config);
        await _context.SaveChangesAsync(cancellationToken);

        return config;
    }

    private static void Apply(
        Domain.Entities.TenantConfig config,
        bool loginEnabled,
        bool welcomeEmailEnabled,
        bool maintenanceMode,
        string? maintenanceMessage)
    {
        config.LoginEnabled = loginEnabled;
        config.WelcomeEmailEnabled = welcomeEmailEnabled;
        config.MaintenanceMode = maintenanceMode;
        config.MaintenanceMessage = maintenanceMessage;
        config.UpdatedAt = DateTime.UtcNow;
    }

    private static TenantConfigResponse MapToResponse(Domain.Entities.TenantConfig config) =>
        new()
        {
            TenantId = config.TenantId,
            LoginEnabled = config.LoginEnabled,
            WelcomeEmailEnabled = config.WelcomeEmailEnabled,
            MaintenanceMode = config.MaintenanceMode,
            MaintenanceMessage = config.MaintenanceMessage,
        };

    private async Task LogActivityAsync(Guid tenantId, string description)
    {
        await _activityLogService.LogAsync(new LogActivityRequest
        {
            UserId = RequireUserId(),
            TenantId = tenantId,
            Action = ActivityActions.TenantConfig.Updated,
            Module = ActivityModules.TenantConfig,
            Description = description,
        });
    }
}
