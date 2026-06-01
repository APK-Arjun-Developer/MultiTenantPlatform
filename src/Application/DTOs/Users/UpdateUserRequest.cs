namespace Application.DTOs.Users;

public class UpdateUserRequest
{
    public string Email { get; set; } = default!;

    public string FullName { get; set; } = default!;

    public string? RoleName { get; set; }

    public string? Password { get; set; }

    /// <summary>
    /// File id from Files table (same tenant as user). Omit to leave unchanged.
    /// </summary>
    public Guid? ProfileFileId { get; set; }

    /// <summary>When true, removes the profile image (FK cleared).</summary>
    public bool ClearProfileImage { get; set; }
}
