namespace Application.Interfaces.Authentication;

public interface IJwtTokenGenerator
{
    string GenerateTokenAsync(
        Guid userId,
        string email,
        string fullName,
        Guid tenantId,
        IList<(Guid Id, string Name)> roles);

    DateTime ComputeAccessTokenExpiryUtc();
}