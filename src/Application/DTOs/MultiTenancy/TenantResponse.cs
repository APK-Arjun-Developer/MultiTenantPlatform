namespace Application.DTOs.Tenant;

public class TenantResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string Slug { get; set; } = default!;

    public bool IsActive { get; set; }
}