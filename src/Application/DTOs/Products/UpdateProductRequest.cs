namespace Application.DTOs.Products;

public class UpdateProductRequest
{
    public string Name { get; set; } = default!;

    public string? NewName { get; set; }

    public decimal Price { get; set; }
}
