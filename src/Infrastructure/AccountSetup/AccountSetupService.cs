using Application.Common;
using Application.DTOs.AccountSetup;
using Application.DTOs.ActivityLogs;
using Application.Exceptions;
using Application.Interfaces.AccountSetup;
using Application.Interfaces.ActivityLogs;
using Infrastructure.Identity.Entities;
using Infrastructure.Onboarding;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.AccountSetup;

public class AccountSetupService : IAccountSetupService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IActivityLogService _activityLogService;

    public AccountSetupService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IActivityLogService activityLogService)
    {
        _context = context;
        _userManager = userManager;
        _activityLogService = activityLogService;
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

        return new ValidateAccountSetupResponse
        {
            IsValid = true,
            Email = user.Email,
            FullName = user.FullName,
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

        // Mark token used + activate account in one SaveChanges.
        tokenRecord.UsedAt = DateTime.UtcNow;
        user.IsActive = true;
        user.PasswordSetAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(new LogActivityRequest
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            Action = ActivityActions.Onboarding.AccountSetupCompleted,
            Module = ActivityModules.Onboarding,
            Description = $"User '{user.Email}' completed account setup.",
        }, cancellationToken);

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
