namespace Application.DTOs.Tenant;

public class CreateTenantRequest
{
    public string Name { get; set; } = default!;

    public string Slug { get; set; } = default!;
}