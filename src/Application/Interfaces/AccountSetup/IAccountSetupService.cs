using Application.DTOs.AccountSetup;

namespace Application.Interfaces.AccountSetup;

public interface IAccountSetupService
{
    Task<ValidateAccountSetupResponse> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default);

    Task<SetPasswordResponse> SetPasswordAsync(
        SetPasswordRequest request,
        CancellationToken cancellationToken = default);
}
