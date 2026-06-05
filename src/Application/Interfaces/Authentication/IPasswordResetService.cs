using Application.DTOs.Auth;

namespace Application.Interfaces.Authentication;

public interface IPasswordResetService
{
    Task SendResetEmailAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);

    Task<ValidateResetTokenResponse> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
}
