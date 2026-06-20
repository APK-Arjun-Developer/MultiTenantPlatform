using Application.Interfaces.Authentication;
using Domain.Enums;
using Infrastructure.Authentication.JWT;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Infrastructure.Authentication.Services;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _jwtSettings;

    public JwtTokenGenerator(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    public string GenerateTokenAsync(
        Guid userId,
        string email,
        string fullName,
        Guid tenantId,
        SystemRole systemRole,
        IList<(Guid Id, string Name)> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("user_id", userId.ToString()),
            new("tenant_id", tenantId.ToString()),
            new("full_name", fullName),
            new("system_role", ((int)systemRole).ToString()),
        };

        // One claim per role — unambiguous for multi-role users.
        foreach (var (id, name) in roles)
        {
            claims.Add(new Claim("role_ids", id.ToString()));
            claims.Add(new Claim(ClaimTypes.Role, name));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = ComputeAccessTokenExpiryUtc();

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public DateTime ComputeAccessTokenExpiryUtc() =>
        DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes);
}
