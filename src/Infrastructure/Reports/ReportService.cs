using System.Globalization;
using System.Text;
using Application.DTOs.Reports;
using Application.Interfaces.Reports;
using Application.Interfaces.Tenant;
using Infrastructure.Identity.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Reports;

public class ReportService : IReportService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentTenantService _currentTenantService;

    public ReportService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ICurrentTenantService currentTenantService)
    {
        _context = context;
        _userManager = userManager;
        _currentTenantService = currentTenantService;
    }

    public async Task<ReportSummaryResponse> GetSummaryAsync()
    {
        var tenantId = RequireTenantId();

        return new ReportSummaryResponse
        {
            UserCount = await _userManager.Users.CountAsync(u => u.TenantId == tenantId),
            RoleCount = await _context.Roles.CountAsync(r => r.TenantId == tenantId),
            ProductCount = await _context.Products.CountAsync(),
            ActivityLogCount = await _context.ActivityLogs.CountAsync(),
            GeneratedAtUtc = DateTime.UtcNow,
        };
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
