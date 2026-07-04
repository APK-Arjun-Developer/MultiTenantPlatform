using Application.Common;
using Application.DTOs.AccountSetup;
using Application.DTOs.ActivityLogs;
using Application.Exceptions;
using Application.Interfaces.AccountSetup;
using Application.Interfaces.ActivityLogs;
using Application.Interfaces.Email;
using Infrastructure.Identity.Entities;
using Infrastructure.Onboarding;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.AccountSetup;

public class AccountSetupService : IAccountSetupService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IActivityLogService _activityLogService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AccountSetupService> _logger;
    private readonly string _appBaseUrl;

    public AccountSetupService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IActivityLogService activityLogService,
        IEmailService emailService,
        ILogger<AccountSetupService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _userManager = userManager;
        _activityLogService = activityLogService;
        _emailService = emailService;
        _logger = logger;
        _appBaseUrl = configuration["AppBaseUrl"] ?? "https://app.example.com";
    }

    public async Task<ValidateAccountSetupResponse> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var tokenRecord = await FindTokenAsync(token, cancellationToken);

        if (tokenRecord == null)
        {
            return InvalidResponse("Token not found or already used.");
        }

        if (tokenRecord.IsExpired)
        {
            return InvalidResponse("Token has expired. Please request a new setup email.");
        }

        if (tokenRecord.IsUsed)
        {
            return InvalidResponse("Token has already been used.");
        }

        var user = await _userManager.FindByIdAsync(tokenRecord.UserId.ToString());

        if (user == null)
        {
            return InvalidResponse("Associated user account was not found.");
        }

        if (user.IsActive)
        {
            return InvalidResponse("This account is already set up. Please log in.");
        }

        var hasAddress = await _context.Addresses
            .IgnoreQueryFilters()
            .AnyAsync(a => a.UserId == user.Id && a.DeletedAt == null, cancellationToken);

        return new ValidateAccountSetupResponse
        {
            IsValid = true,
            Email = user.Email,
            FullName = user.FullName,
            HasAddress = hasAddress,
        };
    }

    public async Task<SetPasswordResponse> SetPasswordAsync(
        SetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var tokenRecord = await FindTokenAsync(request.Token, cancellationToken);

        if (tokenRecord == null || tokenRecord.IsExpired || tokenRecord.IsUsed)
        {
            throw new InvalidOperationException(
                "Token is invalid, expired, or already used.");
        }

        var user = await _userManager.FindByIdAsync(tokenRecord.UserId.ToString())
            ?? throw new NotFoundException("User not found.");

        if (user.IsActive)
        {
            throw new ConflictException("Account is already set up.");
        }

        // Reset to new password (Identity generates a valid reset token internally).
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, request.Password);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        // Mark token used + activate + confirm email. Optionally update name.
        tokenRecord.UsedAt = DateTime.UtcNow;
        user.IsActive = true;
        user.EmailConfirmed = true;
        user.PasswordSetAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.FullName))
            user.FullName = request.FullName.Trim();

        await _context.SaveChangesAsync(cancellationToken);

        if (request.Address != null)
        {
            await AddressHelper.ApplyUserAddressUpdateAsync(_context, user, request.Address, false);
            await _context.SaveChangesAsync(cancellationToken);
        }

        await _activityLogService.LogAsync(new LogActivityRequest
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            Action = ActivityActions.Onboarding.AccountSetupCompleted,
            Module = ActivityModules.Onboarding,
            Description = $"User '{user.Email}' completed account setup.",
        }, cancellationToken);

        var loginUrl = $"{_appBaseUrl}/login";

        try
        {
            await _emailService.SendWelcomeEmailAsync(user.Email!, user.FullName, loginUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send Welcome email to {Email} after account setup — non-fatal.",
                user.Email);
        }

        return new SetPasswordResponse
        {
            UserId = user.Id,
            Email = user.Email!,
            IsActive = true,
        };
    }

    private async Task<Domain.Entities.AccountSetupToken?> FindTokenAsync(
        string rawToken,
        CancellationToken cancellationToken)
    {
        var hash = TokenHelper.Hash(rawToken);

        return await _context.AccountSetupTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
    }

    private static ValidateAccountSetupResponse InvalidResponse(string message) =>
        new() { IsValid = false, ErrorMessage = message };
}
