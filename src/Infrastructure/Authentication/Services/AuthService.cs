using Application.DTOs.Auth;
using Application.Interfaces.Authentication;
using Infrastructure.Identity.Entities;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Authentication.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;

    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userManager = userManager;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request)
    {
        var user =
            await _userManager.FindByEmailAsync(
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

        var token = await _jwtTokenGenerator.GenerateTokenAsync(
            user.Id,
            user.Email!,
            user.FullName,
            user.TenantId,
            roles);

        return new AuthResponse
        {
            AccessToken = token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            Email = user.Email!,
            FullName = user.FullName
        };
    }

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request)
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

        var token = await _jwtTokenGenerator.GenerateTokenAsync(
            user.Id,
            user.Email!,
            user.FullName,
            user.TenantId,
            roles);

        return new AuthResponse
        {
            AccessToken = token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            Email = user.Email!,
            FullName = user.FullName
        };
    }
}