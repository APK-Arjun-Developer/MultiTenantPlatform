using Application.Common;
using Application.DTOs.ActivityLogs;
using Application.DTOs.Auth;
using Application.Exceptions;
using Application.Interfaces.ActivityLogs;
using Application.Interfaces.Authentication;
using Application.Interfaces.Email;
using Infrastructure.Identity.Entities;
using Infrastructure.Onboarding;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using Domain.Entities;

namespace Infrastructure.Authentication.Services;

public class EmailVerificationService : IEmailVerificationService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly IActivityLogService _activityLogService;
    private readonly ILogger<EmailVerificationService> _logger;

    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(15);

    public EmailVerificationService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        IActivityLogService activityLogService,
        ILogger<EmailVerificationService> logger)
    {
        _context = context;
        _userManager = userManager;
        _emailService = emailService;
        _activityLogService = activityLogService;
        _logger = logger;
    }

    public async Task SendOtpAsync(
        ResendEmailOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUserAsync(request.Email, request.TenantSlug, cancellationToken);

        // Silently succeed — never reveal whether the email exists or is already confirmed.
        if (user == null || user.EmailConfirmed)
        {
            return;
        }

        var otp = GenerateOtp();
        var otpHash = HashOtp(otp);
        var now = DateTime.UtcNow;

        // Invalidate any previous unused OTPs for this user.
        var stale = await _context.EmailVerificationOtps
            .Where(o => o.UserId == user.Id && o.UsedAt == null)
            .ToListAsync(cancellationToken);

        _context.EmailVerificationOtps.RemoveRange(stale);

        _context.EmailVerificationOtps.Add(new EmailVerificationOtp
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = user.TenantId,
            OtpHash = otpHash,
            ExpiresAt = now.Add(OtpLifetime),
            CreatedAt = now,
        });

        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            await _emailService.SendEmailVerificationOtpAsync(
                user.Email!, user.FullName, otp, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send email verification OTP to {Email}. " +
                "The OTP was stored — the user can request a new one.",
                user.Email);
        }

        await _activityLogService.LogAsync(new LogActivityRequest
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            Action = ActivityActions.Auth.VerificationOtpResent,
            Module = ActivityModules.Auth,
            Description = $"Email verification OTP sent to '{user.Email}'.",
        }, cancellationToken);
    }

    public async Task VerifyOtpAsync(
        VerifyEmailOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUserAsync(request.Email, request.TenantSlug, cancellationToken)
            ?? throw new InvalidOperationException("Invalid verification request.");

        if (user.EmailConfirmed)
        {
            throw new ConflictException("Email address is already verified.");
        }

        var otpHash = HashOtp(request.Otp);

        var record = await _context.EmailVerificationOtps
            .FirstOrDefaultAsync(
                o => o.UserId == user.Id && o.OtpHash == otpHash,
                cancellationToken);

        if (record == null)
        {
            throw new InvalidOperationException("Invalid or expired verification code.");
        }

        if (record.IsUsed)
        {
            throw new InvalidOperationException("This verification code has already been used.");
        }

        if (record.IsExpired)
        {
            throw new InvalidOperationException("This verification code has expired. Please request a new one.");
        }

        record.UsedAt = DateTime.UtcNow;
        user.EmailConfirmed = true;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        await _activityLogService.LogAsync(new LogActivityRequest
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            Action = ActivityActions.Auth.EmailVerified,
            Module = ActivityModules.Auth,
            Description = $"Email address verified for '{user.Email}'.",
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
                t.Slug == tenantSlug && t.DeletedAt == null,
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

    private static string GenerateOtp()
    {
        var bytes = RandomNumberGenerator.GetBytes(4);
        var value = (int)(BitConverter.ToUInt32(bytes, 0) % 900000) + 100000;
        return value.ToString("D6");
    }

    private static string HashOtp(string otp)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(otp);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
