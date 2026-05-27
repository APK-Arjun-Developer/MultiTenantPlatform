namespace Application.DTOs.Auth;

public class LogoutRequest
{
    public string RefreshToken { get; set; } = default!;
}