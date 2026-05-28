namespace Application.Interfaces.Authentication;

public interface IJwtTokenGenerator
{
    string GenerateTokenAsync(
        Guid userId,
        string email,
        string fullName,
        Guid tenantId,
        Guid? roleId,
        IList<string> roles);
}