namespace Application.Interfaces.Email;

public interface IEmailService
{
    Task SendAccountSetupEmailAsync(
        string toEmail,
        string fullName,
        string setupUrl,
        CancellationToken cancellationToken = default);

    Task SendTenantAdminInvitationAsync(
        string toEmail,
        string invitationUrl,
        CancellationToken cancellationToken = default);

    Task SendTenantUserInvitationAsync(
        string toEmail,
        string invitationUrl,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetEmailAsync(
        string toEmail,
        string fullName,
        string resetUrl,
        CancellationToken cancellationToken = default);
}
