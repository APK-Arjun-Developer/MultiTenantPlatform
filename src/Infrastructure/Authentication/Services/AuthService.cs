using Application.DTOs.Auth;
using Application.Interfaces.Authentication;
using Infrastructure.Identity.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Authentication.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;

    private readonly ApplicationDbContext _context;

    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    private readonly IRefreshTokenService _refreshTokenService;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        IJwtTokenGenerator jwtTokenGenerator,
        IRefreshTokenService refreshTokenService)
    {
        _userManager = userManager;
        _context = context;
        _jwtTokenGenerator = jwtTokenGenerator;
        _refreshTokenService = refreshTokenService;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress)
    {
        var user = await FindUserForLoginAsync(request);

        if (user == null)
        {
            throw new InvalidOperationException("Invalid credentials.");
        }

        var validPassword =
            await _userManager.CheckPasswordAsync(
                user,
                request.Password);

        if (!validPassword)
        {
            throw new InvalidOperationException("Invalid credentials.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var roleId = await GetPrimaryRoleIdAsync(user);

        var token = _jwtTokenGenerator.GenerateTokenAsync(
            user.Id,
            user.Email!,
            user.FullName,
            user.TenantId,
            roleId,
            roles);

        var refreshToken = await _refreshTokenService.CreateAsync(
            user.Id,
            user.TenantId,
            ipAddress);

        return new AuthResponse
        {
            AccessToken = token,
            RefreshToken = refreshToken.Token,
            ExpiresAt = _jwtTokenGenerator.ComputeAccessTokenExpiryUtc(),
            Email = user.Email!,
            FullName = user.FullName
        };
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, string ipAddress)
    {
        var refreshToken = await _refreshTokenService.GetByTokenAsync(request.RefreshToken);

        if (refreshToken == null)
        {
            throw new InvalidOperationException("Invalid refresh token.");
        }

        var user = await _userManager.FindByIdAsync(refreshToken.UserId.ToString());

        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        await _refreshTokenService.RevokeAsync(refreshToken, ipAddress);

        var newRefreshToken = await _refreshTokenService.CreateAsync(user.Id, user.TenantId, ipAddress);

        var roles = await _userManager.GetRolesAsync(user);
        var roleId = await GetPrimaryRoleIdAsync(user);

        var accessToken = _jwtTokenGenerator.GenerateTokenAsync(
            user.Id,
            user.Email!,
            user.FullName,
            user.TenantId,
            roleId,
            roles);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken.Token,
            ExpiresAt = _jwtTokenGenerator.ComputeAccessTokenExpiryUtc(),
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

    private async Task<ApplicationUser?> FindUserForLoginAsync(LoginRequest request)
    {
        var normalizedEmail = request.Email.ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(request.TenantSlug))
        {
            return await _userManager.Users
                .FirstOrDefaultAsync(u =>
                    u.NormalizedEmail == normalizedEmail &&
                    u.TenantId == Guid.Empty);
        }

        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t =>
                t.Slug == request.TenantSlug
                && t.DeletedAt == null
                && t.IsActive);

        if (tenant == null)
        {
            return null;
        }

        return await _userManager.Users
            .FirstOrDefaultAsync(u =>
                u.NormalizedEmail == normalizedEmail &&
                u.TenantId == tenant.Id);
    }

    private async Task<Guid?> GetPrimaryRoleIdAsync(ApplicationUser user)
    {
        var roleId = await _context.Set<IdentityUserRole<Guid>>()
            .Where(ur => ur.UserId == user.Id)
            .Join(
                _context.Roles.Where(r => r.TenantId == user.TenantId),
                ur => ur.RoleId,
                r => r.Id,
                (_, r) => r.Id)
            .FirstOrDefaultAsync();

        return roleId == Guid.Empty ? null : roleId;
    }
}
