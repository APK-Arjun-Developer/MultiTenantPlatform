using Application.Common;
using Application.DTOs.ActivityLogs;
using Application.DTOs.Auth;
using Application.Interfaces.ActivityLogs;
using Application.Interfaces.Authentication;
using Domain.Enums;
using Infrastructure.Identity.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

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
            throw new InvalidOperationException("Your account has been deactivated. Please contact your administrator.");
        }

        if (!user.EmailConfirmed)
        {
            throw new InvalidOperationException("Your email address has not been verified. Please check your inbox for the verification code.");
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
            user.SystemRole,
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
            FullName = user.FullName,
            Roles = roles.Select(r => r.Name).ToList()
        };
    }

    public async Task<AuthResponse> RefreshTokenAsync(string refreshToken, string ipAddress)
    {
        var storedToken = await _refreshTokenService.GetByTokenAsync(refreshToken);

        if (storedToken == null)
        {
            throw new InvalidOperationException("Invalid refresh token.");
        }

        var user = await _userManager.FindByIdAsync(storedToken.UserId.ToString());

        if (user == null)
        {
            throw new InvalidOperationException("User not found.");
        }

        await _refreshTokenService.RevokeAsync(storedToken, ipAddress);

        var newRefreshToken = await _refreshTokenService.CreateAsync(user.Id, user.TenantId, ipAddress);

        var roles = await GetUserRolesWithIdsAsync(user);

        var accessToken = _jwtTokenGenerator.GenerateTokenAsync(
            user.Id,
            user.Email!,
            user.FullName,
            user.TenantId,
            user.SystemRole,
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
            FullName = user.FullName,
            Roles = roles.Select(r => r.Name).ToList()
        };
    }

    public async Task LogoutAsync(string? refreshToken, string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var storedToken = await _refreshTokenService.GetByTokenAsync(refreshToken);

        if (storedToken == null)
        {
            return;
        }

        await _refreshTokenService.RevokeAsync(storedToken, ipAddress);

        await _activityLogService.LogAsync(new LogActivityRequest
        {
            UserId = storedToken.UserId,
            TenantId = storedToken.TenantId,
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

    public async Task<MeResponse> GetMeAsync(ClaimsPrincipal principal)
    {
        var userId = Guid.Parse(principal.FindFirstValue("user_id")!);
        var email = principal.FindFirstValue(JwtRegisteredClaimNames.Email)!;
        var fullName = principal.FindFirstValue("full_name")!;
        var tenantId = Guid.Parse(principal.FindFirstValue("tenant_id")!);
        var roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        string? tenantSlug = null;
        if (tenantId != Guid.Empty)
        {
            tenantSlug = await _context.Tenants
                .IgnoreQueryFilters()
                .Where(t => t.Id == tenantId)
                .Select(t => t.Slug)
                .FirstOrDefaultAsync();
        }

        return new MeResponse
        {
            Id = userId,
            Email = email,
            FullName = fullName,
            Roles = roles,
            TenantSlug = tenantSlug,
        };
    }

    private async Task<IList<(Guid Id, string Name)>> GetUserRolesWithIdsAsync(ApplicationUser user)
    {
        // Exclude built-in system role names — those are represented by the system_role JWT claim,
        // not by Roles table entries. Only custom tenant-defined roles belong in the token.
        var builtIn = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            RoleNames.SystemAdmin,
            RoleNames.TenantAdmin,
            RoleNames.TenantUser,
        };

        return await _context.Set<IdentityUserRole<Guid>>()
            .Where(ur => ur.UserId == user.Id)
            .Join(
                _context.Roles.Where(r => r.TenantId == user.TenantId && !builtIn.Contains(r.Name!)),
                ur => ur.RoleId,
                r => r.Id,
                (_, r) => new ValueTuple<Guid, string>(r.Id, r.Name!))
            .ToListAsync();
    }
}
