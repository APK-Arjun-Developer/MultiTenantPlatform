using Application.DTOs.Common;

namespace Application.DTOs.Onboarding;

public class CreateTenantUserRequest
{
    public string FullName { get; set; } = default!;

    public string Email { get; set; } = default!;

    public List<Guid> RoleIds { get; set; } = [];

    /// <summary>Optional address to set on the new user immediately.</summary>
    public AddressRequest? Address { get; set; }
}
