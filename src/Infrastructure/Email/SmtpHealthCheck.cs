using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Email;

/// <summary>
/// Verifies that the configured SMTP server is reachable and accepts credentials.
/// Registered as "email" in the health check system.
/// In Development (stub mode) this check is skipped — it always reports Healthy.
/// </summary>
public sealed class SmtpHealthCheck : IHealthCheck
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpHealthCheck> _logger;

    public SmtpHealthCheck(IOptions<SmtpSettings> settings, ILogger<SmtpHealthCheck> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Skip check when no SMTP host is configured (stub / dev mode).
        if (string.IsNullOrWhiteSpace(_settings.Host))
        {
            return HealthCheckResult.Healthy("SMTP not configured — stub mode.");
        }

        try
        {
            using var client = new SmtpClient();

            var secureOption = _settings.EnableSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            await client.ConnectAsync(_settings.Host, _settings.Port, secureOption, cts.Token);

            if (!string.IsNullOrWhiteSpace(_settings.Username))
            {
                await client.AuthenticateAsync(_settings.Username, _settings.Password, cts.Token);
            }

            await client.DisconnectAsync(quit: true, cts.Token);

            return HealthCheckResult.Healthy($"SMTP reachable at {_settings.Host}:{_settings.Port}.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMTP health check failed for {Host}:{Port}.", _settings.Host, _settings.Port);

            return HealthCheckResult.Degraded(
                $"SMTP unreachable at {_settings.Host}:{_settings.Port}: {ex.Message}");
        }
    }
}
