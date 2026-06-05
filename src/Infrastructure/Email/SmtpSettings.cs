namespace Infrastructure.Email;

public sealed class SmtpSettings
{
    public const string SectionName = "Email";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// For Gmail: use an App Password (never the account password).
    /// Set via environment variable Email__Password or a secrets manager — never in appsettings.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "MultiTenant Platform";

    /// <summary>
    /// true  → STARTTLS on port 587 (recommended for Gmail / most providers).
    /// false → plain connection (dev/local only — Mailpit, Papercut, etc.).
    /// </summary>
    public bool EnableSsl { get; set; } = true;
}
