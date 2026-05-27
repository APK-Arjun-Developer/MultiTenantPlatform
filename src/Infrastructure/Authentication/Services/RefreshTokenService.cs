using Application.Interfaces.Authentication;
using Domain.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Infrastructure.Authentication.Services;

public class RefreshTokenService : IRefreshTokenService
{
    private readonly ApplicationDbContext _context;

    public RefreshTokenService(
        ApplicationDbContext context)
    {
        _context = context;
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
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress
        };

        _context.RefreshTokens.Add(refreshToken);

        await _context.SaveChangesAsync();

        return refreshToken;
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token)
    {
        return await _context.RefreshTokens
            .FirstOrDefaultAsync(x =>
                x.Token == token &&
                x.RevokedAt == null &&
                x.ExpiresAt > DateTime.UtcNow);
    }

    public async Task RevokeAsync(RefreshToken token, string ipAddress)
    {
        token.RevokedAt = DateTime.UtcNow;

        token.RevokedByIp = ipAddress;

        _context.RefreshTokens.Update(token);

        await _context.SaveChangesAsync();
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);

        return Convert.ToBase64String(randomBytes);
    }
}