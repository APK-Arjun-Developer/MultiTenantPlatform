using System.Security.Cryptography;

namespace Infrastructure.Onboarding;

internal static class TokenHelper
{
    private const int RawTokenBytes = 64;

    /// <summary>Returns a cryptographically secure URL-safe base64 token and its SHA-256 hash.</summary>
    internal static (string RawToken, string Hash) Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(RawTokenBytes);
        var rawToken = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var hash = Hash(rawToken);
        return (rawToken, hash);
    }

    /// <summary>Returns the SHA-256 hex string of the given token value.</summary>
    internal static string Hash(string rawToken)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(rawToken);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
