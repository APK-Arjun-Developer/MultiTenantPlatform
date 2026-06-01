namespace Application.DTOs.Users;

/// <summary>
/// Profile update for the authenticated user (JWT). Email and role cannot be changed here.
/// </summary>
public class UpdateCurrentUserRequest
{
    public string FullName { get; set; } = default!;

    public string? Password { get; set; }
}
