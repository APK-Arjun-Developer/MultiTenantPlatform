using Application.DTOs.Common;

namespace Application.DTOs.Tenant;

public class UpdateTenantRequest
{
    public string? Slug { get; set; }

    public string Name { get; set; } = default!;

    public string? NewSlug { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid? ProfileFileId { get; set; }

    public bool ClearProfileImage { get; set; }

    public AddressRequest? Address { get; set; }

    public bool ClearAddress { get; set; }
}
