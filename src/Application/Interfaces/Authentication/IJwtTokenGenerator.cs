using Domain.Enums;

namespace Application.Interfaces.Authentication;

public interface IJwtTokenGenerator
{
    string GenerateTokenAsync(
        Guid userId,
        string email,
        string fullName,
        Guid tenantId,
        SystemRole systemRole,
        IList<(Guid Id, string Name)> roles);

    DateTime ComputeAccessTokenExpiryUtc();
}