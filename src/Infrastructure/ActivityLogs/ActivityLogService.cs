using Application.DTOs.ActivityLogs;
using Application.Interfaces.ActivityLogs;
using Application.Interfaces.Tenant;
using Domain.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Infrastructure.ActivityLogs;

public class ActivityLogService : IActivityLogService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ActivityLogService> _logger;

    public ActivityLogService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ActivityLogService> logger)
    {
        _context = context;
        _currentTenantService = currentTenantService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogAsync(LogActivityRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = request.TenantId
                ?? _currentTenantService.TenantId
                ?? Guid.Empty;

            var userAgent = request.UserAgent
                ?? _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();

            _context.ActivityLogs.Add(new ActivityLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = request.UserId,
                Action = request.Action,
                Module = request.Module,
                Description = request.Description,
                IpAddress = request.IpAddress,
                UserAgent = userAgent,
                CreatedAt = DateTime.UtcNow,
            });

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to persist activity log {Module}/{Action} for user {UserId}.",
                request.Module,
                request.Action,
                request.UserId);
        }
    }
}
