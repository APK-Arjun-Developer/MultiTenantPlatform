using Application.Common;
using Application.DTOs.ActivityLogs;
using Application.DTOs.Auth;
using Application.Interfaces.ActivityLogs;
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

    private readonly IActivityLogService _activityLogService;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        IJwtTokenGenerator jwtTokenGenerator,
        IRefreshTokenService refreshTokenService,
        IActivityLogService activityLogService)
    {
        _userManager = userManager;
        _context = context;
        _jwtTokenGenerator = jwtTokenGenerator;
        _refreshTokenService = refreshTokenService;
        _activityLogService = activityLogService;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string ipAddress)
    {
        var user = await FindUserForLoginAsync(request);

        if (user == null)
        {
            throw new InvalidOperationException("Invalid credentials.");
        }

        if (!user.IsActive)
        {
            throw new InvalidOperationException("Account is not active. Please complete your account setup.");
        }

        var validPassword =
            await _userManager.CheckPasswordAsync(
                user,
                request.Password);

        if (!validPassword)
        {
            throw new InvalidOperationException("Invalid credentials.");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var roles = await GetUserRolesWithIdsAsync(user);

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

        await _activityLogService.LogAsync(new LogActivityRequest
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            Action = ActivityActions.Auth.Login,
            Module = ActivityModules.Auth,
            Description = $"User '{user.Email}' logged in.",
            IpAddress = ipAddress,
        });

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

        var roles = await GetUserRolesWithIdsAsync(user);

        var accessToken = _jwtTokenGenerator.GenerateTokenAsync(
            user.Id,
            user.Email!,
            user.FullName,
            user.TenantId,
            roles);

        await _activityLogService.LogAsync(new LogActivityRequest
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            Action = ActivityActions.Auth.Refresh,
            Module = ActivityModules.Auth,
            Description = $"User '{user.Email}' refreshed token.",
            IpAddress = ipAddress,
        });

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

        await _activityLogService.LogAsync(new LogActivityRequest
        {
            UserId = refreshToken.UserId,
            TenantId = refreshToken.TenantId,
            Action = ActivityActions.Auth.Logout,
            Module = ActivityModules.Auth,
            Description = "User logged out.",
            IpAddress = ipAddress,
        });
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

    private async Task<IList<(Guid Id, string Name)>> GetUserRolesWithIdsAsync(ApplicationUser user)
    {
        return await _context.Set<IdentityUserRole<Guid>>()
            .Where(ur => ur.UserId == user.Id)
            .Join(
                _context.Roles.Where(r => r.TenantId == user.TenantId),
                ur => ur.RoleId,
                r => r.Id,
                (_, r) => new ValueTuple<Guid, string>(r.Id, r.Name!))
            .ToListAsync();
    }
}
