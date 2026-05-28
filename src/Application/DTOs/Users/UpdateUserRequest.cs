namespace Application.DTOs.Users;

public class UpdateUserRequest
{
    public string Email { get; set; } = default!;

    public string FullName { get; set; } = default!;

    public string? RoleName { get; set; }

    public string? Password { get; set; }
}
