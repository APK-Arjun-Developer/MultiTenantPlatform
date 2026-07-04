namespace Application.DTOs.AccountSetup;

public class ValidateAccountSetupResponse
{
    public bool IsValid { get; set; }

    public string? Email { get; set; }

    public string? FullName { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>True when the user already has an address (direct-creation flow). Client skips address step.</summary>
    public bool HasAddress { get; set; }
}
