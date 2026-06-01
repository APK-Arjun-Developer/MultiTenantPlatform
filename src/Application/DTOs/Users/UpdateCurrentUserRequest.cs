namespace Application.DTOs.Users;

/// <summary>
/// Profile update for the authenticated user (JWT). Email and role cannot be changed here.
/// </summary>
public class UpdateCurrentUserRequest
{
    public string FullName { get; set; } = default!;

    public string? Password { get; set; }

    /// <summary>
    /// File id from <c>POST /api/v1/files</c> (same tenant). Omit to leave unchanged.
    /// </summary>
    public Guid? ProfileFileId { get; set; }

    /// <summary>When true, removes the profile image (FK cleared).</summary>
    public bool ClearProfileImage { get; set; }
}
