using Application.DTOs.Auth;
using Application.Exceptions;
using Application.Interfaces.Authentication;
using Infrastructure.Identity.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Authentication.Services;

public class ImpersonationService : IImpersonationService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IRefreshTokenService _refreshTokenService;

    public ImpersonationService(
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

    public async Task<StartImpersonationResponse> StartAsync(
        Guid adminUserId,
        Guid targetUserId,
        Guid tenantId,
        string ipAddress)
    {
        var targetUser = await _userManager.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == targetUserId && u.TenantId == tenantId);

        if (targetUser == null)
            throw new NotFoundException($"User with ID '{targetUserId}' not found in the specified tenant.");

        if (!targetUser.IsActive)
            throw new InvalidOperationException("Cannot impersonate an inactive user.");

        var adminUser = await _userManager.FindByIdAsync(adminUserId.ToString())
            ?? throw new NotFoundException("Admin user not found.");

        var targetRoles = await GetUserRolesAsync(targetUser);

        var accessToken = _jwtTokenGenerator.GenerateImpersonationToken(
            targetUser.Id,
            targetUser.Email!,
            targetUser.FullName,
            targetUser.TenantId,
            targetUser.SystemRole,
            targetRoles,
            adminUser.Id,
            adminUser.Email!,
            adminUser.FullName);

        return new StartImpersonationResponse
        {
            UserId = targetUser.Id,
            Email = targetUser.Email!,
            FullName = targetUser.FullName,
            SystemRole = targetUser.SystemRole.ToString(),
            Roles = targetRoles.Select(r => r.Name).ToList(),
            ExpiresAt = _jwtTokenGenerator.ComputeAccessTokenExpiryUtc(),
            AccessToken = accessToken,
        };
    }

    public async Task<StopImpersonationResponse> StopAsync(string restoreToken, string ipAddress)
    {
        var storedToken = await _refreshTokenService.GetByTokenAsync(restoreToken);

        if (storedToken == null)
            throw new InvalidOperationException("Restore token is invalid or expired. Please log in again.");

        var adminUser = await _userManager.FindByIdAsync(storedToken.UserId.ToString())
            ?? throw new NotFoundException("Admin user not found.");

        if (!adminUser.IsActive)
            throw new InvalidOperationException("Admin account is no longer active.");

        await _refreshTokenService.RevokeAsync(storedToken, ipAddress);
        var newRefreshToken = await _refreshTokenService.CreateAsync(adminUser.Id, adminUser.TenantId, ipAddress);

        var roles = await GetUserRolesAsync(adminUser);

        var accessToken = _jwtTokenGenerator.GenerateTokenAsync(
            adminUser.Id,
            adminUser.Email!,
            adminUser.FullName,
            adminUser.TenantId,
            adminUser.SystemRole,
            roles);

        return new StopImpersonationResponse
        {
            UserId = adminUser.Id,
            Email = adminUser.Email!,
            FullName = adminUser.FullName,
            SystemRole = adminUser.SystemRole.ToString(),
            AccessToken = accessToken,
            RefreshToken = newRefreshToken.Token,
            ExpiresAt = _jwtTokenGenerator.ComputeAccessTokenExpiryUtc(),
        };
    }

    private async Task<IList<(Guid Id, string Name)>> GetUserRolesAsync(ApplicationUser user)
    {
        return await _context.Set<Microsoft.AspNetCore.Identity.IdentityUserRole<Guid>>()
            .Where(ur => ur.UserId == user.Id)
            .Join(
                _context.Roles.Where(r => r.TenantId == user.TenantId),
                ur => ur.RoleId,
                r => r.Id,
                (_, r) => new ValueTuple<Guid, string>(r.Id, r.Name!))
            .ToListAsync();
    }
}
