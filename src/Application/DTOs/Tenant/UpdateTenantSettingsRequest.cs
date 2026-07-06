using Application.DTOs.Common;

namespace Application.DTOs.Tenant;

public class UpdateTenantSettingsRequest
{
    public string Name { get; set; } = default!;

    public Guid? ProfileFileId { get; set; }

    public bool ClearProfileImage { get; set; }

    public AddressRequest? Address { get; set; }

    public bool ClearAddress { get; set; }
}
