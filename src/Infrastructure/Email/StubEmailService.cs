using Application.Interfaces.Email;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Email;

/// <summary>
/// No-op email service that logs outgoing messages to Serilog.
/// Used in Development so developers can click URLs from logs without needing an SMTP server.
/// Replace with SmtpEmailService (or a provider SDK) in Production via DI.
/// </summary>
public sealed class StubEmailService : IEmailService
{
    private readonly ILogger<StubEmailService> _logger;

    public StubEmailService(ILogger<StubEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendAccountSetupEmailAsync(
        string toEmail, string fullName, string setupUrl,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[STUB EMAIL] AccountSetup → {Email} ({FullName}): {Url}",
            toEmail, fullName, setupUrl);
        return Task.CompletedTask;
    }

    public Task SendWelcomeEmailAsync(
        string toEmail, string fullName, string loginUrl,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[STUB EMAIL] Welcome → {Email} ({FullName}): {Url}",
            toEmail, fullName, loginUrl);
        return Task.CompletedTask;
    }

    public Task SendTenantAdminInvitationAsync(
        string toEmail, string invitationUrl, string tenantName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[STUB EMAIL] TenantAdminInvitation → {Email} (tenant: {Tenant}): {Url}",
            toEmail, tenantName, invitationUrl);
        return Task.CompletedTask;
    }

    public Task SendTenantUserInvitationAsync(
        string toEmail, string invitationUrl, string tenantName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[STUB EMAIL] TenantUserInvitation → {Email} (tenant: {Tenant}): {Url}",
            toEmail, tenantName, invitationUrl);
        return Task.CompletedTask;
    }

    public Task SendNewTenantInvitationAsync(
        string toEmail, string invitationUrl,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[STUB EMAIL] NewTenantInvitation → {Email}: {Url}",
            toEmail, invitationUrl);
        return Task.CompletedTask;
    }

    public Task SendAccountActivationEmailAsync(
        string toEmail, string fullName, string loginUrl,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[STUB EMAIL] AccountActivated → {Email} ({FullName}): {Url}",
            toEmail, fullName, loginUrl);
        return Task.CompletedTask;
    }

    public Task SendAccountDeactivationEmailAsync(
        string toEmail, string fullName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[STUB EMAIL] AccountDeactivated → {Email} ({FullName})",
            toEmail, fullName);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(
        string toEmail, string fullName, string resetUrl,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[STUB EMAIL] PasswordReset → {Email} ({FullName}): {Url}",
            toEmail, fullName, resetUrl);
        return Task.CompletedTask;
    }

    public Task SendEmailVerificationOtpAsync(
        string toEmail, string fullName, string otp,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[STUB EMAIL] EmailVerificationOtp → {Email} ({FullName}): OTP={Otp}",
            toEmail, fullName, otp);
        return Task.CompletedTask;
    }
}
