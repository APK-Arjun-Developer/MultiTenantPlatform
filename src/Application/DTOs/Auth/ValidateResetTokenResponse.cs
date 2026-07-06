namespace Application.DTOs.Auth;

public class ValidateResetTokenResponse
{
    public bool IsValid { get; set; }

    public string? Email { get; set; }

    public string? ErrorMessage { get; set; }
}
