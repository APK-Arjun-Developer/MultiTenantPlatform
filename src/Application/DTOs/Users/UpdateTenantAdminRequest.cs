using Application.DTOs.Common;

namespace Application.DTOs.Users;

public class UpdateTenantAdminRequest
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = default!;

    /// <summary>Optional — replaces all current role assignments when provided.</summary>
    public Guid? RoleId { get; set; }

    public Guid? ProfileFileId { get; set; }

    public bool ClearProfileImage { get; set; }

    public AddressRequest? Address { get; set; }

    public bool ClearAddress { get; set; }
}
