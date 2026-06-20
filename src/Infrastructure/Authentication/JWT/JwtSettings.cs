namespace Infrastructure.Authentication.JWT;

public class JwtSettings
{
    public string Issuer { get; set; } = default!;

    public string Audience { get; set; } = default!;

    public string Key { get; set; } = default!;

    public int ExpiryMinutes { get; set; }

    public int RefreshTokenExpiryDays { get; set; }
}