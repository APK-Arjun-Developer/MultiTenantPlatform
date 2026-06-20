using Application.DTOs.Auth;
using System.Security.Claims;

namespace Application.Interfaces.Authentication;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress);

    Task<AuthResponse> RefreshTokenAsync(string refreshToken, string ipAddress);

    Task LogoutAsync(string? refreshToken, string ipAddress);

    Task<MeResponse> GetMeAsync(ClaimsPrincipal principal);
}