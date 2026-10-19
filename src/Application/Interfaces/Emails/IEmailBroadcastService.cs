using Application.DTOs.Emails;

namespace Application.Interfaces.Emails;

public interface IEmailBroadcastService
{
    Task<EmailBroadcastResponse> BroadcastAsync(
        SendEmailBroadcastRequest request,
        CancellationToken cancellationToken = default);
}
