using Application.DTOs.Auth;

namespace Application.Interfaces.Authentication;

public interface IEmailVerificationService
{
    Task SendOtpAsync(ResendEmailOtpRequest request, CancellationToken cancellationToken = default);

    Task VerifyOtpAsync(VerifyEmailOtpRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates and sends a verification OTP directly by email + tenantId.
    /// Used after programmatic user creation (admin creates user with a password)
    /// where the tenant slug is not in scope but the tenantId is.
    /// </summary>
    Task SendVerificationOtpAsync(string email, Guid tenantId, CancellationToken cancellationToken = default);
}
