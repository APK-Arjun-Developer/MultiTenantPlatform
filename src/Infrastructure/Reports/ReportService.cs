using System.Globalization;
using System.Text;
using Application.DTOs.Reports;
using Application.Interfaces.Caching;
using Application.Interfaces.Reports;
using Application.Interfaces.Tenant;
using Infrastructure.Caching;
using Infrastructure.Identity.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Reports;

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IAppCache _cache;
    private readonly CacheOptions _cacheOptions;

    public ReportService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ICurrentTenantService currentTenantService,
        IAppCache cache,
        IOptions<CacheOptions> cacheOptions)
    {
        _context = context;
        _userManager = userManager;
        _currentTenantService = currentTenantService;
        _cache = cache;
        _cacheOptions = cacheOptions.Value;
    }

    public Task<ReportSummaryResponse> GetSummaryAsync()
    {
        var tenantId = RequireTenantId();

        return _cache.GetOrCreateAsync(
            CacheKeys.ReportSummary(tenantId),
            _ => LoadSummaryAsync(tenantId),
            TimeSpan.FromMinutes(_cacheOptions.ReportSummaryMinutes));
    }

    public async Task<byte[]> ExportSummaryCsvAsync()
    {
        var summary = await GetSummaryAsync();

        var builder = new StringBuilder();
        builder.AppendLine("Metric,Value");
        builder.AppendLine($"Users,{summary.UserCount}");
        builder.AppendLine($"Roles,{summary.RoleCount}");
        builder.AppendLine($"Products,{summary.ProductCount}");
        builder.AppendLine($"ActivityLogs,{summary.ActivityLogCount}");
        builder.AppendLine(
            $"GeneratedAtUtc,{summary.GeneratedAtUtc.ToString("O", CultureInfo.InvariantCulture)}");

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private async Task<ReportSummaryResponse> LoadSummaryAsync(Guid tenantId)
    {
        return new ReportSummaryResponse
        {
            UserCount = await _userManager.Users.CountAsync(u => u.TenantId == tenantId),
            RoleCount = await _context.Roles.CountAsync(r => r.TenantId == tenantId),
            ProductCount = await _context.Products.CountAsync(),
            ActivityLogCount = await _context.ActivityLogs.CountAsync(),
            GeneratedAtUtc = DateTime.UtcNow,
        };
    }

    private Guid RequireTenantId()
    {
        var tenantId = _currentTenantService.TenantId ?? Guid.Empty;

        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Reports are available within a tenant context only.");
        }

        return tenantId;
    }
}
