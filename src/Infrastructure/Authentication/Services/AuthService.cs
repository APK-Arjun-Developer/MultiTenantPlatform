using Application.DTOs.Auth;
using Application.Interfaces.Authentication;
using Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Authentication.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;

    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    private readonly IRefreshTokenService _refreshTokenService;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IJwtTokenGenerator jwtTokenGenerator,
        IRefreshTokenService refreshTokenService)
    {
        _userManager = userManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _refreshTokenService = refreshTokenService;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress)
    {
        var user = await _userManager.FindByEmailAsync(
                request.Email);

        if (user == null)
        {
            throw new Exception("Invalid credentials.");
        }

        var validPassword =
            await _userManager.CheckPasswordAsync(
                user,
                request.Password);

        if (!validPassword)
        {
            throw new Exception("Invalid credentials.");
        }

        var roles = await _userManager.GetRolesAsync(user);

        var token = _jwtTokenGenerator.GenerateTokenAsync(
            user.Id,
            user.Email!,
            user.FullName,
            user.TenantId,
            roles);

        var refreshToken = await _refreshTokenService.CreateAsync(
            user.Id,
            user.TenantId,
            ipAddress);

        return new AuthResponse
        {
            AccessToken = token,
            RefreshToken = refreshToken.Token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            Email = user.Email!,
            FullName = user.FullName
        };
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string ipAddress)
    {
        var existingUser =
            await _userManager.FindByEmailAsync(
                request.Email);

        if (existingUser != null)
        {
            throw new Exception("User already exists.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Empty,
            FullName = request.FullName,
            Email = request.Email,
            UserName = request.Email,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };

        var result =
            await _userManager.CreateAsync(
                user,
                request.Password);

        if (!result.Succeeded)
        {
            throw new Exception(
                string.Join(", ",
                    result.Errors.Select(x => x.Description)));
        }

        var roles = await _userManager.GetRolesAsync(user);

        var token = _jwtTokenGenerator.GenerateTokenAsync(
            user.Id,
            user.Email!,
            user.FullName,
            user.TenantId,
            roles);

        var refreshToken = await _refreshTokenService.CreateAsync(
            user.Id,
            user.TenantId,
            ipAddress);

        return new AuthResponse
        {
            AccessToken = token,
            RefreshToken = refreshToken.Token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            Email = user.Email!,
            FullName = user.FullName
        };
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, string ipAddress)
    {
        var refreshToken = await _refreshTokenService.GetByTokenAsync(request.RefreshToken);

        if (refreshToken == null)
        {
            throw new Exception("Invalid refresh token.");
        }

        var user = await _userManager.FindByIdAsync(refreshToken.UserId.ToString());

        if (user == null)
        {
            throw new Exception("User not found.");
        }

        await _refreshTokenService.RevokeAsync(refreshToken, ipAddress);

        var newRefreshToken = await _refreshTokenService.CreateAsync(user.Id, user.TenantId, ipAddress);

        var roles = await _userManager.GetRolesAsync(user);

        var accessToken = _jwtTokenGenerator.GenerateTokenAsync(
            user.Id,
            user.Email!,
            user.FullName,
            user.TenantId,
            roles);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken.Token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            Email = user.Email!,
            FullName = user.FullName
        };
    }

    public async Task LogoutAsync(LogoutRequest request, string ipAddress)
    {
        var refreshToken = await _refreshTokenService.GetByTokenAsync(request.RefreshToken);

        if (refreshToken == null)
        {
            return;
        }

        await _refreshTokenService.RevokeAsync(refreshToken, ipAddress);
    }
}