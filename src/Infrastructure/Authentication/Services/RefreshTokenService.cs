using Application.Interfaces.Authentication;
using Domain.Entities;
using Infrastructure.Authentication.JWT;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Infrastructure.Authentication.Services;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly ApplicationDbContext _context;
    private readonly JwtSettings _jwtSettings;

    public RefreshTokenService(
        ApplicationDbContext context,
        IOptions<JwtSettings> jwtSettings)
    {
        _context = context;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<RefreshToken> CreateAsync(Guid userId, Guid tenantId, string ipAddress)
    {
        var token = GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress
        };

        _context.RefreshTokens.Add(refreshToken);

        await _context.SaveChangesAsync();

        return refreshToken;
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token)
    {
        // IgnoreQueryFilters: refresh tokens are looked up cross-tenant (e.g. during impersonation
        // restore, the caller's tenant context differs from the token owner's TenantId).
        return await _context.RefreshTokens
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x =>
                x.Token == token &&
                x.RevokedAt == null &&
                x.ExpiresAt > DateTime.UtcNow &&
                x.DeletedAt == null);
    }

    public async Task RevokeAsync(RefreshToken token, string ipAddress)
    {
        token.RevokedAt = DateTime.UtcNow;

        token.RevokedByIp = ipAddress;

        _context.RefreshTokens.Update(token);

        await _context.SaveChangesAsync();
    }

    public async Task RevokeAllForUserAsync(Guid userId, string ipAddress)
    {
        var tokens = await _context.RefreshTokens
            .IgnoreQueryFilters()
            .Where(x => x.UserId == userId && x.RevokedAt == null && x.DeletedAt == null)
            .ToListAsync();

        foreach (var token in tokens)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedByIp = ipAddress;
        }

        await _context.SaveChangesAsync();
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);

        return Convert.ToBase64String(randomBytes);
    }
}