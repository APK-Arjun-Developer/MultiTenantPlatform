using Application.DTOs.Auth;

namespace Application.Interfaces.Authentication;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request);

    Task<AuthResponse> RegisterAsync(RegisterRequest request);
}