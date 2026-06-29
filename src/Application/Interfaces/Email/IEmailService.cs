namespace Application.Interfaces.Email;

public interface IEmailService
{
    // ── Onboarding ────────────────────────────────────────────────────────────

    Task SendAccountSetupEmailAsync(
        string toEmail,
        string fullName,
        string setupUrl,
        CancellationToken cancellationToken = default);

    Task SendWelcomeEmailAsync(
        string toEmail,
        string fullName,
        string loginUrl,
        CancellationToken cancellationToken = default);

    // ── Invitations ───────────────────────────────────────────────────────────

    Task SendTenantAdminInvitationAsync(
        string toEmail,
        string invitationUrl,
        string tenantName,
        CancellationToken cancellationToken = default);

    Task SendTenantUserInvitationAsync(
        string toEmail,
        string invitationUrl,
        string tenantName,
        CancellationToken cancellationToken = default);

    Task SendNewTenantInvitationAsync(
        string toEmail,
        string invitationUrl,
        CancellationToken cancellationToken = default);

    // ── Account lifecycle ─────────────────────────────────────────────────────

    Task SendAccountActivationEmailAsync(
        string toEmail,
        string fullName,
        string loginUrl,
        CancellationToken cancellationToken = default);

    Task SendAccountDeactivationEmailAsync(
        string toEmail,
        string fullName,
        CancellationToken cancellationToken = default);

    // ── Password ──────────────────────────────────────────────────────────────

    Task SendPasswordResetEmailAsync(
        string toEmail,
        string fullName,
        string resetUrl,
        CancellationToken cancellationToken = default);

    // ── Email verification ────────────────────────────────────────────────────

    Task SendEmailVerificationOtpAsync(
        string toEmail,
        string fullName,
        string otp,
        CancellationToken cancellationToken = default);
}
