namespace Application.DTOs.Products;

public class ProductResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public decimal Price { get; set; }

    public Guid TenantId { get; set; }

    public DateTime CreatedAt { get; set; }
}
