using Application.DTOs.Common;

namespace Application.DTOs.Users;

public class UpdateCurrentUserRequest
{
    public string FullName { get; set; } = default!;

    public string? Password { get; set; }

    public Guid? ProfileFileId { get; set; }

    public bool ClearProfileImage { get; set; }

    public AddressRequest? Address { get; set; }

    public bool ClearAddress { get; set; }
}
