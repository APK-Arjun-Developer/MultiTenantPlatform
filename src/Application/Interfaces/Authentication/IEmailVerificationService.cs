using Application.DTOs.Auth;

namespace Application.Interfaces.Authentication;

public interface IEmailVerificationService
{
    Task SendOtpAsync(ResendEmailOtpRequest request, CancellationToken cancellationToken = default);

    Task VerifyOtpAsync(VerifyEmailOtpRequest request, CancellationToken cancellationToken = default);
}
