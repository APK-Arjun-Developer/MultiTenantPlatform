using Application.Common;
using Application.DTOs.ActivityLogs;
using Application.DTOs.Common;
using Application.Interfaces.ActivityLogs;
using Application.Interfaces.Tenant;
using Domain.Entities;
using Infrastructure.Common;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.ActivityLogs;

public class ActivityLogService : TenantScopedService, IActivityLogService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ActivityLogService> _logger;

    public ActivityLogService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ActivityLogService> logger)
        : base(currentTenantService)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogAsync(LogActivityRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var tenantId = request.TenantId
                ?? CurrentTenantService.TenantId
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

    public async Task<PagedResponse<ActivityLogResponse>> GetLogsAsync(
        ActivityLogQueryParams queryParams,
        CancellationToken cancellationToken = default)
    {
        (var page, var pageSize) = Pagination.Normalize(queryParams.Page, queryParams.PageSize);

        IQueryable<ActivityLog> query;

        if (IsSystemAdmin())
        {
            // SystemAdmin: IgnoreQueryFilters — tenant scoped by X-Tenant-Id if provided,
            // otherwise all tenants.
            query = CurrentTenantService.TenantId.HasValue
                ? _context.ActivityLogs
                    .IgnoreQueryFilters()
                    .Where(l => l.TenantId == CurrentTenantService.TenantId && l.DeletedAt == null)
                : _context.ActivityLogs
                    .IgnoreQueryFilters()
                    .Where(l => l.DeletedAt == null);
        }
        else
        {
            // TenantAdmin/TenantUser: EF global filter scopes to own tenant automatically.
            query = _context.ActivityLogs.AsQueryable();
        }

        if (queryParams.UserId.HasValue)
            query = query.Where(l => l.UserId == queryParams.UserId.Value);

        if (!string.IsNullOrWhiteSpace(queryParams.Module))
            query = query.Where(l => l.Module == queryParams.Module);

        if (!string.IsNullOrWhiteSpace(queryParams.Action))
            query = query.Where(l => l.Action == queryParams.Action);

        if (queryParams.DateFrom.HasValue)
            query = query.Where(l => l.CreatedAt >= queryParams.DateFrom.Value);

        if (queryParams.DateTo.HasValue)
            query = query.Where(l => l.CreatedAt <= queryParams.DateTo.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        query = (queryParams.SortBy?.ToLowerInvariant(), queryParams.SortOrder?.ToLowerInvariant()) switch
        {
            ("createdat", "asc") => query.OrderBy(l => l.CreatedAt),
            _ => query.OrderByDescending(l => l.CreatedAt),
        };

        var logs = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var userIds = logs.Select(l => l.UserId).Distinct().ToList();
        var tenantIds = logs.Select(l => l.TenantId).Distinct().ToList();

        var userMap = await _context.Users
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var tenantMap = await _context.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Name })
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        var items = logs.Select(l =>
        {
            userMap.TryGetValue(l.UserId, out var user);
            tenantMap.TryGetValue(l.TenantId, out var tenant);
            return new ActivityLogResponse
            {
                Id = l.Id,
                TenantId = l.TenantId,
                TenantName = tenant?.Name,
                UserId = l.UserId,
                UserDisplayName = user?.FullName ?? "Unknown",
                UserEmail = user?.Email ?? string.Empty,
                Action = l.Action,
                Module = l.Module,
                Description = l.Description,
                IpAddress = l.IpAddress,
                CreatedAt = l.CreatedAt,
            };
        }).ToList();

        return new PagedResponse<ActivityLogResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }
}
