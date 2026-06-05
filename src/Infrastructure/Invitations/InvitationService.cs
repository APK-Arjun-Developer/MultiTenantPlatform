using System.Text.Json;
using Application.Common;
using Application.DTOs.ActivityLogs;
using Application.DTOs.Common;
using Application.DTOs.Invitations;
using Application.DTOs.Onboarding;
using Application.Exceptions;
using Application.Interfaces.ActivityLogs;
using Application.Interfaces.Email;
using Application.Interfaces.Invitations;
using Application.Interfaces.Tenant;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Common;
using Infrastructure.Identity;
using Infrastructure.Identity.Entities;
using Infrastructure.Onboarding;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Invitations;

public class InvitationService : TenantScopedService, IInvitationService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IIdentityRoleService _identityRoleService;
    private readonly IEmailService _emailService;
    private readonly IActivityLogService _activityLogService;
    private readonly ILogger<InvitationService> _logger;
    private readonly string _appBaseUrl;

    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    public InvitationService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ICurrentTenantService currentTenantService,
        IIdentityRoleService identityRoleService,
        IEmailService emailService,
        IActivityLogService activityLogService,
        ILogger<InvitationService> logger,
        IConfiguration configuration)
        : base(currentTenantService)
    {
        _context = context;
        _userManager = userManager;
        _identityRoleService = identityRoleService;
        _emailService = emailService;
        _activityLogService = activityLogService;
        _logger = logger;
        _appBaseUrl = configuration["AppBaseUrl"] ?? "https://app.example.com";
    }

    // ── System Admin: list tenant admin invitations ──────────────────────────

    public async Task<PagedResponse<InvitationListItemResponse>> GetTenantAdminInvitationsAsync(
        int page, int pageSize,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsSystemAdmin())
        {
            throw new ForbiddenException("Only system administrators can list tenant admin invitations.");
        }

        (page, pageSize) = Pagination.Normalize(page, pageSize);

        var query = _context.Invitations
            .AsNoTracking()
            .Where(i => i.InvitationType == InvitationType.TenantAdmin);

        query = ApplyStatusFilter(query, status);

        var totalCount = await query.CountAsync(cancellationToken);

        var invitations = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var tenantIds = invitations.Select(i => i.TenantId).Distinct().ToList();

        var tenantNames = await _context.Tenants
            .IgnoreQueryFilters()
            .Where(t => tenantIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        return new PagedResponse<InvitationListItemResponse>
        {
            Items = invitations.Select(i => MapToListItem(i, tenantNames.GetValueOrDefault(i.TenantId))).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    // ── System Admin: invite tenant admin ────────────────────────────────────

    public async Task<InviteResponse> InviteTenantAdminAsync(
        InviteTenantAdminRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsSystemAdmin())
        {
            throw new ForbiddenException("Only system administrators can invite tenant admins.");
        }

        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                t => t.Slug == request.TenantSlug && t.DeletedAt == null,
                cancellationToken)
            ?? throw new NotFoundException($"Tenant '{request.TenantSlug}' was not found.");

        await EnsureEmailNotActiveInTenantAsync(request.Email, tenant.Id, cancellationToken);
        await EnsureNoPendingInvitationAsync(request.Email, tenant.Id, cancellationToken);

        var invitation = await CreateInvitationAsync(
            tenant.Id,
            request.Email,
            InvitationType.TenantAdmin,
            request.RoleIds,
            cancellationToken);

        var url = BuildInvitationUrl(InvitationType.TenantAdmin, invitation.RawToken);

        await SendEmailSafeAsync(
            () => _emailService.SendTenantAdminInvitationAsync(request.Email, url, tenant.Name, cancellationToken),
            emailType: "TenantAdminInvitation",
            toEmail: request.Email);

        await LogAsync(
            ActivityActions.Onboarding.TenantAdminInvited,
            $"Invited '{request.Email}' as tenant admin for '{tenant.Slug}'.",
            tenant.Id);

        return BuildInviteResponse(invitation.Record, url);
    }

    // ── Tenant Admin: list user invitations ──────────────────────────────────

    public async Task<PagedResponse<InvitationListItemResponse>> GetUserInvitationsAsync(
        int page, int pageSize,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();

        (page, pageSize) = Pagination.Normalize(page, pageSize);

        var query = _context.Invitations
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.InvitationType == InvitationType.TenantUser);

        query = ApplyStatusFilter(query, status);

        var totalCount = await query.CountAsync(cancellationToken);

        var invitations = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        return new PagedResponse<InvitationListItemResponse>
        {
            Items = invitations.Select(i => MapToListItem(i, tenant?.Name)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    // ── Tenant Admin: invite tenant user ─────────────────────────────────────

    public async Task<InviteResponse> InviteTenantUserAsync(
        InviteTenantUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();

        await EnsureEmailNotActiveInTenantAsync(request.Email, tenantId, cancellationToken);
        await EnsureNoPendingInvitationAsync(request.Email, tenantId, cancellationToken);

        var invitation = await CreateInvitationAsync(
            tenantId,
            request.Email,
            InvitationType.TenantUser,
            request.RoleIds,
            cancellationToken);

        var url = BuildInvitationUrl(InvitationType.TenantUser, invitation.RawToken);

        var tenantName = await _context.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.Id == tenantId && t.DeletedAt == null)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        await SendEmailSafeAsync(
            () => _emailService.SendTenantUserInvitationAsync(request.Email, url, tenantName, cancellationToken),
            emailType: "TenantUserInvitation",
            toEmail: request.Email);

        await LogAsync(
            ActivityActions.Onboarding.TenantUserInvited,
            $"Invited '{request.Email}' as tenant user.",
            tenantId);

        return BuildInviteResponse(invitation.Record, url);
    }

    // ── Revoke invitation ────────────────────────────────────────────────────

    public async Task RevokeInvitationAsync(
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        var invitation = await _context.Invitations
            .FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken)
            ?? throw new NotFoundException("Invitation not found.");

        if (!IsSystemAdmin())
        {
            var tenantId = RequireTenantId();

            if (invitation.TenantId != tenantId)
            {
                throw new ForbiddenException("Invitation does not belong to your tenant.");
            }
        }

        if (invitation.IsRevoked)
        {
            throw new ConflictException("Invitation is already revoked.");
        }

        if (invitation.IsAccepted)
        {
            throw new ConflictException("Cannot revoke an already accepted invitation.");
        }

        invitation.RevokedAt = DateTime.UtcNow;
        invitation.UpdatedAt = DateTime.UtcNow;
        invitation.UpdatedBy = CurrentTenantService.UserId;

        await _context.SaveChangesAsync(cancellationToken);

        await LogAsync(
            ActivityActions.Onboarding.InvitationRevoked,
            $"Revoked invitation for '{invitation.Email}'.",
            invitation.TenantId);
    }

    // ── Public: validate invitation token ────────────────────────────────────

    public async Task<ValidateInvitationResponse> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var invitation = await FindInvitationByTokenAsync(token, cancellationToken);

        if (invitation == null)
        {
            return InvalidInvitationResponse("Invitation not found.");
        }

        if (invitation.IsRevoked)
        {
            return InvalidInvitationResponse("This invitation has been revoked.");
        }

        if (invitation.IsAccepted)
        {
            return InvalidInvitationResponse("This invitation has already been accepted.");
        }

        if (invitation.IsExpired)
        {
            return InvalidInvitationResponse("This invitation has expired.");
        }

        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == invitation.TenantId, cancellationToken);

        return new ValidateInvitationResponse
        {
            IsValid = true,
            Email = invitation.Email,
            InvitationType = invitation.InvitationType,
            TenantName = tenant?.Name,
        };
    }

    // ── Public: accept tenant admin invitation ───────────────────────────────

    public async Task<AcceptInvitationResponse> AcceptTenantAdminInvitationAsync(
        AcceptTenantAdminInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        var invitation = await GetValidInvitationAsync(
            request.Token, InvitationType.TenantAdmin, cancellationToken);

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var roleIds = DeserializeRoleIds(invitation.RoleIdsJson);

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                TenantId = invitation.TenantId,
                FullName = request.FullName,
                Email = invitation.Email,
                UserName = invitation.Email,
                NormalizedEmail = invitation.Email.ToUpperInvariant(),
                NormalizedUserName = invitation.Email.ToUpperInvariant(),
                EmailConfirmed = true,
                PhoneNumber = request.Phone,
                IsActive = true,
                PasswordSetAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            };

            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            foreach (var roleId in roleIds)
            {
                await _identityRoleService.AddUserToRoleAsync(user.Id, roleId);
            }

            invitation.AcceptedAt = DateTime.UtcNow;
            invitation.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            await tx.CommitAsync(cancellationToken);

            var roles = await _userManager.GetRolesAsync(user);

            await LogInternalAsync(
                user.Id,
                invitation.TenantId,
                ActivityActions.Onboarding.InvitationAccepted,
                $"User '{user.Email}' accepted tenant admin invitation.");

            return new AcceptInvitationResponse
            {
                UserId = user.Id,
                Email = user.Email!,
                FullName = user.FullName,
                TenantId = user.TenantId,
                Roles = roles.ToList(),
                InvitationType = InvitationType.TenantAdmin,
                IsActive = true,
            };
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // ── Public: accept tenant user invitation ────────────────────────────────

    public async Task<AcceptInvitationResponse> AcceptTenantUserInvitationAsync(
        AcceptTenantUserInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        var invitation = await GetValidInvitationAsync(
            request.Token, InvitationType.TenantUser, cancellationToken);

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var roleIds = DeserializeRoleIds(invitation.RoleIdsJson);

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                TenantId = invitation.TenantId,
                FullName = request.FullName,
                Email = invitation.Email,
                UserName = invitation.Email,
                NormalizedEmail = invitation.Email.ToUpperInvariant(),
                NormalizedUserName = invitation.Email.ToUpperInvariant(),
                EmailConfirmed = true,
                PhoneNumber = request.Phone,
                IsActive = true,
                PasswordSetAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            };

            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            foreach (var roleId in roleIds)
            {
                await _identityRoleService.AddUserToRoleAsync(user.Id, roleId);
            }

            invitation.AcceptedAt = DateTime.UtcNow;
            invitation.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            await tx.CommitAsync(cancellationToken);

            var roles = await _userManager.GetRolesAsync(user);

            await LogInternalAsync(
                user.Id,
                invitation.TenantId,
                ActivityActions.Onboarding.InvitationAccepted,
                $"User '{user.Email}' accepted tenant user invitation.");

            return new AcceptInvitationResponse
            {
                UserId = user.Id,
                Email = user.Email!,
                FullName = user.FullName,
                TenantId = user.TenantId,
                Roles = roles.ToList(),
                InvitationType = InvitationType.TenantUser,
                IsActive = true,
            };
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<(Invitation Record, string RawToken)> CreateInvitationAsync(
        Guid tenantId,
        string email,
        InvitationType type,
        List<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        var (rawToken, hash) = TokenHelper.Generate();
        var now = DateTime.UtcNow;

        var record = new Invitation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            InvitationType = type,
            RoleIdsJson = JsonSerializer.Serialize(roleIds),
            TokenHash = hash,
            ExpiresAt = now.Add(InvitationLifetime),
            InvitedByUserId = CurrentTenantService.UserId ?? Guid.Empty,
            CreatedAt = now,
            CreatedBy = CurrentTenantService.UserId,
        };

        _context.Invitations.Add(record);
        await _context.SaveChangesAsync(cancellationToken);

        return (record, rawToken);
    }

    private async Task<Invitation?> FindInvitationByTokenAsync(
        string rawToken,
        CancellationToken cancellationToken)
    {
        var hash = TokenHelper.Hash(rawToken);

        return await _context.Invitations
            .FirstOrDefaultAsync(i => i.TokenHash == hash, cancellationToken);
    }

    private async Task<Invitation> GetValidInvitationAsync(
        string rawToken,
        InvitationType expectedType,
        CancellationToken cancellationToken)
    {
        var invitation = await FindInvitationByTokenAsync(rawToken, cancellationToken)
            ?? throw new NotFoundException("Invitation not found.");

        if (invitation.InvitationType != expectedType)
        {
            throw new InvalidOperationException("Invitation type mismatch.");
        }

        if (invitation.IsRevoked)
        {
            throw new InvalidOperationException("This invitation has been revoked.");
        }

        if (invitation.IsAccepted)
        {
            throw new ConflictException("This invitation has already been accepted.");
        }

        if (invitation.IsExpired)
        {
            throw new InvalidOperationException("This invitation has expired.");
        }

        var emailTaken = await _userManager.Users.AnyAsync(
            u => u.NormalizedEmail == invitation.Email.ToUpperInvariant()
                 && u.TenantId == invitation.TenantId,
            cancellationToken);

        if (emailTaken)
        {
            throw new ConflictException(
                "An account with this email already exists for this tenant.");
        }

        return invitation;
    }

    private async Task EnsureEmailNotActiveInTenantAsync(
        string email,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var exists = await _userManager.Users.AnyAsync(
            u => u.NormalizedEmail == email.ToUpperInvariant() && u.TenantId == tenantId,
            cancellationToken);

        if (exists)
        {
            throw new ConflictException($"A user with email '{email}' already exists in this tenant.");
        }
    }

    private async Task EnsureNoPendingInvitationAsync(
        string email,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var pending = await _context.Invitations.AnyAsync(
            i => i.Email == email
                 && i.TenantId == tenantId
                 && i.AcceptedAt == null
                 && i.RevokedAt == null
                 && i.ExpiresAt > DateTime.UtcNow,
            cancellationToken);

        if (pending)
        {
            throw new ConflictException(
                $"An active invitation for '{email}' already exists. Revoke it before sending a new one.");
        }
    }

    private static List<Guid> DeserializeRoleIds(string json)
    {
        return JsonSerializer.Deserialize<List<Guid>>(json) ?? [];
    }

    private string BuildInvitationUrl(InvitationType type, string rawToken)
    {
        var path = type == InvitationType.TenantAdmin
            ? "register/tenant-admin"
            : "register/user";

        return $"{_appBaseUrl}/{path}?token={rawToken}";
    }

    private static InviteResponse BuildInviteResponse(Invitation record, string url) =>
        new()
        {
            InvitationId = record.Id,
            Email = record.Email,
            InvitationType = record.InvitationType,
            ExpiresAt = record.ExpiresAt,
            InvitationUrl = url,
        };

    private static ValidateInvitationResponse InvalidInvitationResponse(string message) =>
        new() { IsValid = false, ErrorMessage = message };

    private static IQueryable<Invitation> ApplyStatusFilter(IQueryable<Invitation> query, string? status) =>
        status?.ToLowerInvariant() switch
        {
            "pending"  => query.Where(i => i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > DateTime.UtcNow),
            "accepted" => query.Where(i => i.AcceptedAt != null),
            "revoked"  => query.Where(i => i.RevokedAt != null),
            "expired"  => query.Where(i => i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt <= DateTime.UtcNow),
            _          => query,
        };

    private static string DeriveStatus(Invitation i)
    {
        if (i.IsAccepted) return "Accepted";
        if (i.IsRevoked)  return "Revoked";
        if (i.IsExpired)  return "Expired";

        return "Pending";
    }

    /// <summary>
    /// Sends an email without letting delivery failures propagate to the caller.
    /// The DB operation already committed successfully — a failed email is logged
    /// as an error (with enough context to retry) but does not roll back the action.
    /// TODO: Replace with an outbox pattern for guaranteed delivery at scale.
    /// </summary>
    private async Task SendEmailSafeAsync(Func<Task> send, string emailType, string toEmail)
    {
        try
        {
            await send();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send {EmailType} email to {Email}. " +
                "The operation succeeded in the database — trigger a resend manually if needed.",
                emailType, toEmail);
        }
    }

    private static InvitationListItemResponse MapToListItem(Invitation i, string? tenantName) =>
        new()
        {
            Id              = i.Id,
            Email           = i.Email,
            InvitationType  = i.InvitationType,
            TenantId        = i.TenantId,
            TenantName      = tenantName,
            ExpiresAt       = i.ExpiresAt,
            AcceptedAt      = i.AcceptedAt,
            RevokedAt       = i.RevokedAt,
            IsExpired       = i.IsExpired,
            IsAccepted      = i.IsAccepted,
            IsRevoked       = i.IsRevoked,
            Status          = DeriveStatus(i),
            CreatedAt       = i.CreatedAt,
            InvitedByUserId = i.InvitedByUserId,
        };

    private async Task LogAsync(string action, string description, Guid tenantId)
    {
        await _activityLogService.LogAsync(new LogActivityRequest
        {
            UserId = RequireUserId(),
            TenantId = tenantId,
            Action = action,
            Module = ActivityModules.Onboarding,
            Description = description,
        });
    }

    private async Task LogInternalAsync(
        Guid userId, Guid tenantId, string action, string description)
    {
        await _activityLogService.LogAsync(new LogActivityRequest
        {
            UserId = userId,
            TenantId = tenantId,
            Action = action,
            Module = ActivityModules.Onboarding,
            Description = description,
        });
    }
}
