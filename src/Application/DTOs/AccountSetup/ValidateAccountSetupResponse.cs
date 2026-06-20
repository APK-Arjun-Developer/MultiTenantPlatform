namespace Application.DTOs.AccountSetup;

public class ValidateAccountSetupResponse
{
    public bool IsValid { get; set; }

    public string? Email { get; set; }

    public string? FullName { get; set; }

    public string? TenantSlug { get; set; }

    public string? ErrorMessage { get; set; }
}
