using Application.DTOs.Common;

namespace Application.DTOs.Onboarding;

public class CreateTenantAdminRequest
{
    public Guid TenantId { get; set; }

    public string FullName { get; set; } = default!;

    public string Email { get; set; } = default!;

    /// <summary>Optional address to set on the new admin immediately.</summary>
    public AddressRequest? Address { get; set; }
}
