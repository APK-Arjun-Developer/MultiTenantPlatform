using Application.Interfaces.Email;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Email;

/// <summary>
/// No-op email service that logs outgoing messages to Serilog.
/// Replace with a real SMTP / SendGrid / SES implementation in production.
/// </summary>
public class StubEmailService : IEmailService
{
    private readonly ILogger<StubEmailService> _logger;

    public StubEmailService(ILogger<StubEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendAccountSetupEmailAsync(
        string toEmail,
        string fullName,
        string setupUrl,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[EMAIL] AccountSetup → {Email} ({FullName}): {Url}",
            toEmail, fullName, setupUrl);
        return Task.CompletedTask;
    }

    public Task SendTenantAdminInvitationAsync(
        string toEmail,
        string invitationUrl,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[EMAIL] TenantAdminInvitation → {Email}: {Url}",
            toEmail, invitationUrl);
        return Task.CompletedTask;
    }

    public Task SendTenantUserInvitationAsync(
        string toEmail,
        string invitationUrl,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[EMAIL] TenantUserInvitation → {Email}: {Url}",
            toEmail, invitationUrl);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(
        string toEmail,
        string fullName,
        string resetUrl,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[EMAIL] PasswordReset → {Email} ({FullName}): {Url}",
            toEmail, fullName, resetUrl);
        return Task.CompletedTask;
    }
}
