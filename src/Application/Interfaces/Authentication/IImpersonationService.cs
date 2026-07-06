using Application.DTOs.Auth;

namespace Application.Interfaces.Authentication;

public interface IImpersonationService
{
    Task<StartImpersonationResponse> StartAsync(Guid adminUserId, Guid targetUserId, Guid tenantId, string ipAddress);

    Task<StopImpersonationResponse> StopAsync(string restoreToken, string ipAddress);
}
