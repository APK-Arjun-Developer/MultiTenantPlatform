using Application.Interfaces.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Polly;
using Polly.Retry;

namespace Infrastructure.Email;

/// <summary>
/// Production email service backed by MailKit SMTP with a Polly retry pipeline.
/// Configure via the "Email" section of appsettings (or environment variables).
/// For Gmail: set Email__Password to an App Password — never the account password.
/// </summary>
public sealed class SmtpEmailService : IEmailService
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;
    private readonly ResiliencePipeline _retry;

    public SmtpEmailService(
        IOptions<SmtpSettings> settings,
        ILogger<SmtpEmailService> logger)
    {
        _settings = settings.Value;
        _logger   = logger;

        // 3 attempts with exponential back-off: 2s → 4s → 8s.
        // Retries on transient network / SMTP protocol errors.
        _retry = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType      = DelayBackoffType.Exponential,
                Delay            = TimeSpan.FromSeconds(2),
                ShouldHandle     = new PredicateBuilder()
                    .Handle<SmtpCommandException>()
                    .Handle<SmtpProtocolException>()
                    .Handle<IOException>()
                    .Handle<TimeoutException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "SMTP retry {Attempt} after {Delay:g}: {Message}",
                        args.AttemptNumber + 1,
                        args.RetryDelay,
                        args.Outcome.Exception?.Message);
                    return default;
                },
            })
            .Build();
    }

    // ── IEmailService implementation ─────────────────────────────────────────

    public Task SendAccountSetupEmailAsync(
        string toEmail, string fullName, string setupUrl,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            toEmail,
            $"Set up your account",
            EmailTemplates.AccountSetup(fullName, setupUrl),
            emailType: "AccountSetup",
            cancellationToken);

    public Task SendWelcomeEmailAsync(
        string toEmail, string fullName, string loginUrl,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            toEmail,
            "Welcome to MultiTenant Platform",
            EmailTemplates.Welcome(fullName, loginUrl),
            emailType: "Welcome",
            cancellationToken);

    public Task SendTenantAdminInvitationAsync(
        string toEmail, string invitationUrl, string tenantName,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            toEmail,
            $"You're invited to manage {tenantName}",
            EmailTemplates.TenantAdminInvitation(toEmail, invitationUrl, tenantName),
            emailType: "TenantAdminInvitation",
            cancellationToken);

    public Task SendTenantUserInvitationAsync(
        string toEmail, string invitationUrl, string tenantName,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            toEmail,
            $"You're invited to join {tenantName}",
            EmailTemplates.TenantUserInvitation(toEmail, invitationUrl, tenantName),
            emailType: "TenantUserInvitation",
            cancellationToken);

    public Task SendNewTenantInvitationAsync(
        string toEmail, string invitationUrl,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            toEmail,
            "You've been invited to create a new tenant",
            EmailTemplates.NewTenantInvitation(toEmail, invitationUrl),
            emailType: "NewTenantInvitation",
            cancellationToken);

    public Task SendAccountActivationEmailAsync(
        string toEmail, string fullName, string loginUrl,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            toEmail,
            "Your account has been activated",
            EmailTemplates.AccountActivation(fullName, loginUrl),
            emailType: "AccountActivation",
            cancellationToken);

    public Task SendAccountDeactivationEmailAsync(
        string toEmail, string fullName,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            toEmail,
            "Your account has been deactivated",
            EmailTemplates.AccountDeactivation(fullName),
            emailType: "AccountDeactivation",
            cancellationToken);

    public Task SendPasswordResetEmailAsync(
        string toEmail, string fullName, string resetUrl,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            toEmail,
            "Reset your password",
            EmailTemplates.PasswordReset(fullName, resetUrl),
            emailType: "PasswordReset",
            cancellationToken);

    public Task SendEmailVerificationOtpAsync(
        string toEmail, string fullName, string otp,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            toEmail,
            "Your email verification code",
            EmailTemplates.EmailVerificationOtp(fullName, otp),
            emailType: "EmailVerificationOtp",
            cancellationToken);

    // ── Core send ─────────────────────────────────────────────────────────────

    private async Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string emailType,
        CancellationToken cancellationToken)
    {
        var message = BuildMessage(toEmail, subject, htmlBody);

        await _retry.ExecuteAsync(async ct =>
        {
            using var client = new SmtpClient();

            var secureOption = _settings.EnableSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            await client.ConnectAsync(_settings.Host, _settings.Port, secureOption, ct);

            if (!string.IsNullOrWhiteSpace(_settings.Username))
            {
                await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);
            }

            await client.SendAsync(message, ct);

            await client.DisconnectAsync(quit: true, ct);

            _logger.LogInformation(
                "Email sent. Type={EmailType} To={Email} Subject={Subject}",
                emailType, toEmail, subject);
        },
        cancellationToken);
    }

    private MimeMessage BuildMessage(string toEmail, string subject, string htmlBody)
    {
        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var body = new BodyBuilder
        {
            HtmlBody = htmlBody,
        };

        message.Body = body.ToMessageBody();

        return message;
    }
}
