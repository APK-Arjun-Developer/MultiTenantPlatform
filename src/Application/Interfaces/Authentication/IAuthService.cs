using Application.DTOs.Auth;

namespace Application.Interfaces.Authentication;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress);

    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, string ipAddress);

    Task LogoutAsync(LogoutRequest request, string ipAddress);
}