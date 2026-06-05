using Application.Common;
using Application.DTOs.ActivityLogs;
using Application.DTOs.Onboarding;
using Application.Exceptions;
using Application.Interfaces.ActivityLogs;
using Application.Interfaces.Email;
using Application.Interfaces.Onboarding;
using Application.Interfaces.Tenant;
using Domain.Entities;
using Infrastructure.Common;
using Infrastructure.Identity;
using Infrastructure.Identity.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Onboarding;

public class OnboardingService : TenantScopedService, IOnboardingService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IIdentityRoleService _identityRoleService;
    private readonly IEmailService _emailService;
    private readonly IActivityLogService _activityLogService;
    private readonly ILogger<OnboardingService> _logger;
    private readonly string _appBaseUrl;

    private static readonly TimeSpan SetupTokenLifetime = TimeSpan.FromDays(7);

    public OnboardingService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ICurrentTenantService currentTenantService,
        IIdentityRoleService identityRoleService,
        IEmailService emailService,
        IActivityLogService activityLogService,
        ILogger<OnboardingService> logger,
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

    // ── System Admin: create tenant admin ────────────────────────────────────

    public async Task<CreateTenantAdminResponse> CreateTenantAdminAsync(
        CreateTenantAdminRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsSystemAdmin())
        {
            throw new ForbiddenException("Only system administrators can create tenant admins.");
        }

        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                t => t.Slug == request.TenantSlug && t.DeletedAt == null,
                cancellationToken)
            ?? throw new NotFoundException($"Tenant '{request.TenantSlug}' was not found.");

        await EnsureEmailNotTakenAsync(request.Email, tenant.Id, cancellationToken);

        var roles = await ResolveRolesByNameAsync(tenant.Id, request.RoleNames, cancellationToken);

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = CreateInactiveUser(request.FullName, request.Email, tenant.Id);
            await CreateUserOrThrowAsync(user);

            foreach (var role in roles)
            {
                await _identityRoleService.AddUserToRoleAsync(user.Id, role.Id);
            }

            var (rawToken, setupUrl) = await IssueSetupTokenAsync(user, cancellationToken);

            await tx.CommitAsync(cancellationToken);

            await SendEmailSafeAsync(
                () => _emailService.SendAccountSetupEmailAsync(user.Email!, user.FullName, setupUrl, cancellationToken),
                emailType: "AccountSetup", toEmail: user.Email!);

            await LogAsync(
                ActivityActions.Onboarding.TenantAdminCreated,
                $"Created tenant admin '{user.Email}' for tenant '{tenant.Slug}'.",
                tenant.Id);

            return new CreateTenantAdminResponse
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                TenantId = tenant.Id,
                TenantSlug = tenant.Slug,
                Roles = request.RoleNames,
                IsActive = false,
                SetupUrl = setupUrl,
            };
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // ── System Admin: resend setup email for tenant admin ────────────────────

    public async Task ResendTenantAdminSetupEmailAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!IsSystemAdmin())
        {
            throw new ForbiddenException("Only system administrators can resend tenant admin setup emails.");
        }

        var user = await FindInactiveUserAsync(userId, cancellationToken);

        var setupUrl = await RefreshSetupTokenAsync(user, cancellationToken);

        await _emailService.SendAccountSetupEmailAsync(
            user.Email!, user.FullName, setupUrl, cancellationToken);

        await LogAsync(
            ActivityActions.Onboarding.OnboardingEmailResent,
            $"Resent account setup email to '{user.Email}'.",
            user.TenantId);
    }

    // ── Shared: activate / deactivate ────────────────────────────────────────

    public async Task<UserStatusResponse> ActivateUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await FindManagedUserAsync(userId, cancellationToken);

        if (user.IsActive)
        {
            throw new ConflictException("User is already active.");
        }

        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        await LogAsync(
            ActivityActions.Onboarding.UserActivated,
            $"Activated user '{user.Email}'.",
            user.TenantId);

        var loginUrl = $"{_appBaseUrl}/login";
        await SendEmailSafeAsync(
            () => _emailService.SendAccountActivationEmailAsync(user.Email!, user.FullName, loginUrl),
            emailType: "AccountActivation", toEmail: user.Email!);

        return new UserStatusResponse { UserId = user.Id, Email = user.Email!, IsActive = true };
    }

    public async Task<UserStatusResponse> DeactivateUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await FindManagedUserAsync(userId, cancellationToken);

        if (!user.IsActive)
        {
            throw new ConflictException("User is already inactive.");
        }

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        await LogAsync(
            ActivityActions.Onboarding.UserDeactivated,
            $"Deactivated user '{user.Email}'.",
            user.TenantId);

        await SendEmailSafeAsync(
            () => _emailService.SendAccountDeactivationEmailAsync(user.Email!, user.FullName),
            emailType: "AccountDeactivation", toEmail: user.Email!);

        return new UserStatusResponse { UserId = user.Id, Email = user.Email!, IsActive = false };
    }

    // ── Tenant Admin: create tenant user ─────────────────────────────────────

    public async Task<CreateTenantUserResponse> CreateTenantUserAsync(
        CreateTenantUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();

        await EnsureEmailNotTakenAsync(request.Email, tenantId, cancellationToken);

        var roles = await ResolveRolesByNameAsync(tenantId, request.RoleNames, cancellationToken);

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = CreateInactiveUser(request.FullName, request.Email, tenantId);
            await CreateUserOrThrowAsync(user);

            foreach (var role in roles)
            {
                await _identityRoleService.AddUserToRoleAsync(user.Id, role.Id);
            }

            var (_, setupUrl) = await IssueSetupTokenAsync(user, cancellationToken);

            await tx.CommitAsync(cancellationToken);

            await SendEmailSafeAsync(
                () => _emailService.SendAccountSetupEmailAsync(user.Email!, user.FullName, setupUrl, cancellationToken),
                emailType: "AccountSetup", toEmail: user.Email!);

            await LogAsync(
                ActivityActions.Onboarding.TenantUserCreated,
                $"Created tenant user '{user.Email}'.",
                tenantId);

            return new CreateTenantUserResponse
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                TenantId = tenantId,
                Roles = request.RoleNames,
                IsActive = false,
                SetupUrl = setupUrl,
            };
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // ── Tenant Admin: resend setup email for tenant user ─────────────────────

    public async Task ResendTenantUserSetupEmailAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        var user = await FindInactiveUserAsync(userId, cancellationToken);

        if (user.TenantId != tenantId)
        {
            throw new ForbiddenException("User does not belong to your tenant.");
        }

        var setupUrl = await RefreshSetupTokenAsync(user, cancellationToken);

        await _emailService.SendAccountSetupEmailAsync(
            user.Email!, user.FullName, setupUrl, cancellationToken);

        await LogAsync(
            ActivityActions.Onboarding.OnboardingEmailResent,
            $"Resent account setup email to '{user.Email}'.",
            tenantId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ApplicationUser CreateInactiveUser(
        string fullName, string email, Guid tenantId) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FullName = fullName,
            Email = email,
            UserName = email,
            NormalizedEmail = email.ToUpperInvariant(),
            NormalizedUserName = email.ToUpperInvariant(),
            EmailConfirmed = true,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
        };

    private async Task CreateUserOrThrowAsync(ApplicationUser user)
    {
        // UserManager requires a password; we use a random placeholder that the
        // user can never log in with (they must go through the setup flow).
        var placeholder = $"Placeholder!{Guid.NewGuid():N}";
        var result = await _userManager.CreateAsync(user, placeholder);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    private async Task<(string RawToken, string SetupUrl)> IssueSetupTokenAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        // Invalidate any prior unused tokens for this user.
        var stale = await _context.AccountSetupTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null)
            .ToListAsync(cancellationToken);

        _context.AccountSetupTokens.RemoveRange(stale);

        var (rawToken, hash) = TokenHelper.Generate();
        var now = DateTime.UtcNow;

        _context.AccountSetupTokens.Add(new AccountSetupToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = user.TenantId,
            TokenHash = hash,
            ExpiresAt = now.Add(SetupTokenLifetime),
            CreatedAt = now,
        });

        await _context.SaveChangesAsync(cancellationToken);

        var setupUrl = $"{_appBaseUrl}/setup-account?token={rawToken}";
        return (rawToken, setupUrl);
    }

    private async Task<string> RefreshSetupTokenAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var (_, setupUrl) = await IssueSetupTokenAsync(user, cancellationToken);
        return setupUrl;
    }

    private async Task EnsureEmailNotTakenAsync(
        string email,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var exists = await _userManager.Users
            .AnyAsync(
                u => u.NormalizedEmail == email.ToUpperInvariant() && u.TenantId == tenantId,
                cancellationToken);

        if (exists)
        {
            throw new ConflictException($"A user with email '{email}' already exists in this tenant.");
        }
    }

    private async Task<List<ApplicationRole>> ResolveRolesByNameAsync(
        Guid tenantId,
        List<string> roleNames,
        CancellationToken cancellationToken)
    {
        var roles = new List<ApplicationRole>();

        foreach (var name in roleNames)
        {
            if (name == RoleNames.SuperAdmin)
            {
                throw new ForbiddenException("Cannot assign the SuperAdmin role.");
            }

            var role = await _identityRoleService.FindRoleByNameAsync(tenantId, name)
                ?? throw new NotFoundException($"Role '{name}' was not found for this tenant.");

            roles.Add(role);
        }

        return roles;
    }

    private async Task<ApplicationUser> FindInactiveUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("User not found.");

        if (user.IsActive)
        {
            throw new ConflictException("User is already active; setup email is not applicable.");
        }

        return user;
    }

    private async Task<ApplicationUser> FindManagedUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("User not found.");

        if (!IsSystemAdmin())
        {
            var tenantId = RequireTenantId();

            if (user.TenantId != tenantId)
            {
                throw new ForbiddenException("User does not belong to your tenant.");
            }
        }

        return user;
    }

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
}
