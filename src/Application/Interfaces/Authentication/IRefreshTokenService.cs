using Domain.Entities;

namespace Application.Interfaces.Authentication;

public interface IRefreshTokenService
{
    Task<RefreshToken> CreateAsync(Guid userId, Guid tenantId, string ipAddress);

    Task<RefreshToken?> GetByTokenAsync(string token);

    Task RevokeAsync(RefreshToken token, string ipAddress);
}