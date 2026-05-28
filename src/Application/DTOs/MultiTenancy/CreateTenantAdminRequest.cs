namespace Application.DTOs.Tenant;

public class CreateTenantAdminRequest
{
    public string FullName { get; set; } = default!;

    public string Email { get; set; } = default!;

    public string Password { get; set; } = default!;
}