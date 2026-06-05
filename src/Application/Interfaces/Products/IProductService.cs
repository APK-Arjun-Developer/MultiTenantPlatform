using Application.DTOs.Common;
using Application.DTOs.Products;

namespace Application.Interfaces.Products;

public interface IProductService
{
    Task<PagedResponse<ProductResponse>> GetAllAsync(int page, int pageSize, string? search = null, string? sortBy = null, string? sortOrder = null);

    Task<ProductResponse> GetByIdAsync(Guid id);

    Task<ProductResponse> GetByNameAsync(string name);

    Task<ProductResponse> CreateAsync(CreateProductRequest request);

    Task<ProductResponse> UpdateAsync(UpdateProductRequest request);

    Task DeleteAsync(DeleteProductRequest request);
}
