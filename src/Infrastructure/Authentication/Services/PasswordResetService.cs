using Application.Common;
using Application.DTOs.ActivityLogs;
using Application.DTOs.Auth;
using Application.Exceptions;
using Application.Interfaces.ActivityLogs;
using Application.Interfaces.Authentication;
using Application.Interfaces.Email;
using Domain.Entities;
using Infrastructure.Identity.Entities;
using Infrastructure.Onboarding;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Authentication.Services;

public class PasswordResetService : IPasswordResetService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly IActivityLogService _activityLogService;
    private readonly string _appBaseUrl;

    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    public PasswordResetService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        IActivityLogService activityLogService,
        IConfiguration configuration)
    {
        _context = context;
        _userManager = userManager;
        _emailService = emailService;
        _activityLogService = activityLogService;
        _appBaseUrl = configuration["AppBaseUrl"] ?? "https://app.example.com";
    }

    public async Task SendResetEmailAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUserAsync(request.Email, request.TenantSlug, cancellationToken);

        // Always return success — never reveal whether the email exists.
        if (user == null || !user.IsActive)
        {
            return;
        }

        // Invalidate any existing unused tokens for this user.
        var existing = await _context.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var old in existing)
        {
            old.UsedAt = DateTime.UtcNow;
        }

        var (rawToken, hash) = TokenHelper.Generate();
        var now = DateTime.UtcNow;

        var record = new PasswordResetToken
        {
            Id        = Guid.NewGuid(),
            UserId    = user.Id,
            TenantId  = user.TenantId,
            TokenHash = hash,
            ExpiresAt = now.Add(TokenLifetime),
            CreatedAt = now,
        };

        _context.PasswordResetTokens.Add(record);
        await _context.SaveChangesAsync(cancellationToken);

        var resetUrl = $"{_appBaseUrl}/reset-password?token={rawToken}";

        await _emailService.SendPasswordResetEmailAsync(
            user.Email!, user.FullName, resetUrl, cancellationToken);

        await _activityLogService.LogAsync(new LogActivityRequest
        {
            UserId      = user.Id,
            TenantId    = user.TenantId,
            Action      = ActivityActions.Auth.ForgotPassword,
            Module      = ActivityModules.Auth,
            Description = $"Password reset email sent to '{user.Email}'.",
        }, cancellationToken);
    }

    public async Task<ValidateResetTokenResponse> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var record = await FindTokenAsync(token, cancellationToken);

        if (record == null)
        {
            return Invalid("Token not found or already used.");
        }

        if (record.IsUsed)
        {
            return Invalid("This reset link has already been used.");
        }

        if (record.IsExpired)
        {
            return Invalid("This reset link has expired. Please request a new one.");
        }

        var user = await _userManager.FindByIdAsync(record.UserId.ToString());

        if (user == null || !user.IsActive)
        {
            return Invalid("Associated account was not found.");
        }

        return new ValidateResetTokenResponse
        {
            IsValid = true,
            Email   = user.Email,
        };
    }

    public async Task ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.NewPassword != request.ConfirmPassword)
        {
            throw new InvalidOperationException("New password and confirmation do not match.");
        }

        var record = await FindTokenAsync(request.Token, cancellationToken)
            ?? throw new NotFoundException("Reset token not found.");

        if (record.IsUsed)
        {
            throw new InvalidOperationException("This reset link has already been used.");
        }

        if (record.IsExpired)
        {
            throw new InvalidOperationException("This reset link has expired. Please request a new one.");
        }

        var user = await _userManager.FindByIdAsync(record.UserId.ToString())
            ?? throw new NotFoundException("User not found.");

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        record.UsedAt = DateTime.UtcNow;
        user.PasswordSetAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(new LogActivityRequest
        {
            UserId      = user.Id,
            TenantId    = user.TenantId,
            Action      = ActivityActions.Auth.ResetPassword,
            Module      = ActivityModules.Auth,
            Description = $"Password reset completed for '{user.Email}'.",
        }, cancellationToken);
    }

    private async Task<ApplicationUser?> FindUserAsync(
        string email, string? tenantSlug, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(tenantSlug))
        {
            return await _userManager.Users
                .FirstOrDefaultAsync(u =>
                    u.NormalizedEmail == normalizedEmail &&
                    u.TenantId == Guid.Empty,
                    cancellationToken);
        }

        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t =>
                t.Slug == tenantSlug && t.DeletedAt == null && t.IsActive,
                cancellationToken);

        if (tenant == null)
        {
            return null;
        }

        return await _userManager.Users
            .FirstOrDefaultAsync(u =>
                u.NormalizedEmail == normalizedEmail &&
                u.TenantId == tenant.Id,
                cancellationToken);
    }

    private async Task<PasswordResetToken?> FindTokenAsync(
        string rawToken, CancellationToken cancellationToken)
    {
        var hash = TokenHelper.Hash(rawToken);

        return await _context.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
    }

    private static ValidateResetTokenResponse Invalid(string message) =>
        new() { IsValid = false, ErrorMessage = message };
}
