using Application.Common;
using Application.DTOs.ActivityLogs;
using Application.DTOs.Emails;
using Application.Exceptions;
using Application.Interfaces.ActivityLogs;
using Application.Interfaces.Email;
using Application.Interfaces.Emails;
using Application.Interfaces.Tenant;
using Domain.Enums;
using Infrastructure.Common;
using Infrastructure.Identity.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Emails;

public class EmailBroadcastService : TenantScopedService, IEmailBroadcastService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly IActivityLogService _activityLogService;
    private readonly ILogger<EmailBroadcastService> _logger;

    public EmailBroadcastService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ICurrentTenantService currentTenantService,
        IEmailService emailService,
        IActivityLogService activityLogService,
        ILogger<EmailBroadcastService> logger)
        : base(currentTenantService)
    {
        _context = context;
        _userManager = userManager;
        _emailService = emailService;
        _activityLogService = activityLogService;
        _logger = logger;
    }

    public async Task<EmailBroadcastResponse> BroadcastAsync(
        SendEmailBroadcastRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Subject))
            throw new InvalidOperationException("Subject is required.");

        if (string.IsNullOrWhiteSpace(request.HtmlBody))
            throw new InvalidOperationException("Email body is required.");

        var targetType = IsSystemAdmin()
            ? request.TargetType
            : BroadcastTarget.CurrentTenant;

        List<string> recipientEmails;

        switch (targetType)
        {
            case BroadcastTarget.AllTenants:
                if (!IsSystemAdmin())
                    throw new ForbiddenException("Only system administrators can broadcast to all tenants.");

                recipientEmails = await _context.Users
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(u => u.TenantId != Guid.Empty
                                && u.IsActive
                                && u.EmailConfirmed
                                && u.DeletedAt == null
                                && u.Email != null)
                    .Select(u => u.Email!)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                break;

            case BroadcastTarget.SpecificTenants:
                if (!IsSystemAdmin())
                    throw new ForbiddenException("Only system administrators can broadcast to specific tenants.");

                if (request.TenantIds == null || request.TenantIds.Count == 0)
                    throw new InvalidOperationException("TenantIds is required for SpecificTenants target.");

                recipientEmails = await _context.Users
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(u => request.TenantIds.Contains(u.TenantId)
                                && u.IsActive
                                && u.EmailConfirmed
                                && u.DeletedAt == null
                                && u.Email != null)
                    .Select(u => u.Email!)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                break;

            case BroadcastTarget.SpecificUsers:
                if (request.UserIds == null || request.UserIds.Count == 0)
                    throw new InvalidOperationException("UserIds is required for SpecificUsers target.");

                var userQuery = _context.Users
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(u => request.UserIds.Contains(u.Id)
                                && u.IsActive
                                && u.EmailConfirmed
                                && u.DeletedAt == null
                                && u.Email != null);

                // TenantAdmin can only send to users within their own tenant.
                if (!IsSystemAdmin())
                {
                    var scopedTenantId = RequireTenantId();
                    userQuery = userQuery.Where(u => u.TenantId == scopedTenantId);
                }

                recipientEmails = await userQuery
                    .Select(u => u.Email!)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                break;

            default: // CurrentTenant
                var tenantId = RequireTenantId();

                recipientEmails = await _context.Users
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(u => u.TenantId == tenantId
                                && u.IsActive
                                && u.EmailConfirmed
                                && u.DeletedAt == null
                                && u.Email != null)
                    .Select(u => u.Email!)
                    .Distinct()
                    .ToListAsync(cancellationToken);
                break;
        }

        var recipientCount = recipientEmails.Count;
        var logTenantId = IsSystemAdmin() ? (Guid?)null : RequireTenantId();

        // Fire-and-forget: start sending in the background so the API returns immediately.
        _ = Task.Run(async () =>
        {
            var sent = 0;
            var failed = 0;

            foreach (var email in recipientEmails)
            {
                try
                {
                    await _emailService.SendCustomEmailAsync(email, request.Subject, request.HtmlBody);
                    sent++;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogWarning(ex,
                        "Broadcast email failed for {Email}. Subject={Subject}",
                        email, request.Subject);
                }
            }

            _logger.LogInformation(
                "Email broadcast complete. Subject={Subject} Sent={Sent} Failed={Failed}",
                request.Subject, sent, failed);
        }, CancellationToken.None);

        await _activityLogService.LogAsync(new LogActivityRequest
        {
            UserId = RequireUserId(),
            TenantId = logTenantId,
            Action = ActivityActions.Emails.BroadcastSent,
            Module = ActivityModules.Emails,
            Description = $"Email broadcast queued. Subject='{request.Subject}' Recipients={recipientCount}.",
        }, cancellationToken);

        return new EmailBroadcastResponse
        {
            RecipientCount = recipientCount,
            Message = $"Email broadcast is being sent to {recipientCount} recipient(s).",
        };
    }
}
