using Application.DTOs.Products;
using Application.Interfaces.Products;
using Application.Interfaces.Tenant;
using Domain.Entities;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Products;

public class ProductService : IProductService
{
    private readonly ApplicationDbContext _context;

    private readonly ICurrentTenantService _currentTenantService;

    public ProductService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService)
    {
        _context = context;
        _currentTenantService = currentTenantService;
    }

    public async Task<IReadOnlyList<ProductResponse>> GetAllAsync()
    {
        var products = await _context.Products
            .OrderBy(p => p.Name)
            .ToListAsync();

        return products.Select(MapToResponse).ToList();
    }

    public async Task<ProductResponse> GetByNameAsync(string name)
    {
        var product = await FindProductByNameAsync(name);

        if (product == null)
        {
            throw new InvalidOperationException("Product not found.");
        }

        return MapToResponse(product);
    }

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request)
    {
        RequireTenantId();

        var exists = await _context.Products
            .AnyAsync(p => p.Name == request.Name);

        if (exists)
        {
            throw new InvalidOperationException(
                $"Product '{request.Name}' already exists.");
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Price = request.Price,
            CreatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        return MapToResponse(product);
    }

    public async Task<ProductResponse> UpdateAsync(UpdateProductRequest request)
    {
        var product = await FindProductByNameAsync(request.Name);

        if (product == null)
        {
            throw new InvalidOperationException("Product not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.NewName) &&
            request.NewName != product.Name)
        {
            var nameTaken = await _context.Products
                .AnyAsync(p => p.Name == request.NewName && p.Id != product.Id);

            if (nameTaken)
            {
                throw new InvalidOperationException(
                    $"Product '{request.NewName}' already exists.");
            }

            product.Name = request.NewName;
        }

        product.Price = request.Price;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToResponse(product);
    }

    public async Task DeleteAsync(DeleteProductRequest request)
    {
        var product = await FindProductByNameAsync(request.Name);

        if (product == null)
        {
            throw new InvalidOperationException("Product not found.");
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
    }

    private async Task<Product?> FindProductByNameAsync(string name)
    {
        return await _context.Products
            .FirstOrDefaultAsync(p => p.Name == name);
    }

    private Guid RequireTenantId()
    {
        var tenantId = _currentTenantService.TenantId ?? Guid.Empty;

        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Products can only be managed within a tenant context.");
        }

        return tenantId;
    }

    private static ProductResponse MapToResponse(Product product) =>
        new()
        {
            Id = product.Id,
            Name = product.Name,
            Price = product.Price,
            TenantId = product.TenantId,
            CreatedAt = product.CreatedAt
        };
}
