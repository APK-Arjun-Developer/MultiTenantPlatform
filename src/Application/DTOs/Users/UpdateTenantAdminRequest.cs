using Application.DTOs.Common;

namespace Application.DTOs.Users;

public class UpdateTenantAdminRequest
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = default!;

    public string? RoleName { get; set; }

    public Guid? ProfileFileId { get; set; }

    public bool ClearProfileImage { get; set; }

    public AddressRequest? Address { get; set; }

    public bool ClearAddress { get; set; }
}
