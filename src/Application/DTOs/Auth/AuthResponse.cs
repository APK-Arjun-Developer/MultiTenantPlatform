namespace Application.DTOs.Auth;

public class AuthResponse
{
    public string AccessToken { get; set; } = default!;

    public string RefreshToken { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }

    public string Email { get; set; } = default!;

    public string FullName { get; set; } = default!;

    public IList<string> Roles { get; set; } = [];
}