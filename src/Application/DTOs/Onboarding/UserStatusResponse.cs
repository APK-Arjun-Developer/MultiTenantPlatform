namespace Application.DTOs.Onboarding;

public class UserStatusResponse
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = default!;

    public bool IsActive { get; set; }
}
