using Application.DTOs.Products;

namespace Application.Interfaces.Products;

public interface IProductService
{
    Task<IReadOnlyList<ProductResponse>> GetAllAsync();

    Task<ProductResponse> GetByNameAsync(string name);

    Task<ProductResponse> CreateAsync(CreateProductRequest request);

    Task<ProductResponse> UpdateAsync(UpdateProductRequest request);

    Task DeleteAsync(DeleteProductRequest request);
}
