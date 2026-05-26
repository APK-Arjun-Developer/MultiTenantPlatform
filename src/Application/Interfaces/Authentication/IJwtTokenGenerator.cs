namespace Application.Interfaces.Authentication;

public interface IJwtTokenGenerator
{
    Task<string> GenerateTokenAsync(
        Guid userId,
        string email,
        string fullName,
        Guid tenantId,
        IList<string> roles);
}