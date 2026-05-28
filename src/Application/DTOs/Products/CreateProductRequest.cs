namespace Application.DTOs.Products;

public class CreateProductRequest
{
    public string Name { get; set; } = default!;

    public decimal Price { get; set; }
}
