using Application.Common;
using Application.DTOs.ActivityLogs;
using Application.DTOs.Common;
using Application.DTOs.Products;
using Application.Exceptions;
using Application.Interfaces.ActivityLogs;
using Application.Interfaces.Caching;
using Application.Interfaces.Products;
using Application.Interfaces.Tenant;
using Infrastructure.Caching;
using Infrastructure.Common;
using Domain.Entities;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Products;

public class ProductService : TenantScopedService, IProductService
{
    private readonly ApplicationDbContext _context;
    private readonly IActivityLogService _activityLogService;
    private readonly IAppCache _cache;
    private readonly CacheOptions _cacheOptions;

    public ProductService(
        ApplicationDbContext context,
        ICurrentTenantService currentTenantService,
        IActivityLogService activityLogService,
        IAppCache cache,
        IOptions<CacheOptions> cacheOptions)
        : base(currentTenantService)
    {
        _context = context;
        _activityLogService = activityLogService;
        _cache = cache;
        _cacheOptions = cacheOptions.Value;
    }

    public async Task<PagedResponse<ProductResponse>> GetAllAsync(
        int page, int pageSize,
        string? search = null,
        string? sortBy = null,
        string? sortOrder = null)
    {
        var tenantId = RequireTenantId();

        (page, pageSize) = Pagination.Normalize(page, pageSize);

        var query = _context.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => p.Name.Contains(search));
        }

        var totalCount = await query.CountAsync();

        query = (sortBy?.ToLowerInvariant(), sortOrder?.ToLowerInvariant()) switch
        {
            ("price", "desc") => query.OrderByDescending(p => p.Price),
            ("price", _)      => query.OrderBy(p => p.Price),
            ("name", "desc")  => query.OrderByDescending(p => p.Name),
            _                 => query.OrderBy(p => p.Name),
        };

        var products = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResponse<ProductResponse>
        {
            Items = products.Select(MapToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<ProductResponse> GetByIdAsync(Guid id)
    {
        var tenantId = RequireTenantId();

        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id)
            ?? throw new NotFoundException($"Product not found.");

        return MapToResponse(product);
    }

    public async Task<ProductResponse> GetByNameAsync(string name)
    {
        var tenantId = RequireTenantId();

        var product = await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Name == name)
            ?? throw new NotFoundException($"Product '{name}' was not found.");

        return MapToResponse(product);
    }

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request)
    {
        var tenantId = RequireTenantId();

        var exists = await _context.Products
            .AnyAsync(p => p.TenantId == tenantId && p.Name == request.Name);

        if (exists)
        {
            throw new ConflictException($"Product '{request.Name}' already exists.");
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

        _cache.InvalidateTenantDashboard(tenantId);

        await LogActivityAsync(ActivityActions.Products.Created, $"Created product '{product.Name}'.");

        return MapToResponse(product);
    }

    public async Task<ProductResponse> UpdateAsync(UpdateProductRequest request)
    {
        var tenantId = RequireTenantId();

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Name == request.Name)
            ?? throw new NotFoundException($"Product '{request.Name}' was not found.");

        if (!string.IsNullOrWhiteSpace(request.NewName) && request.NewName != product.Name)
        {
            var nameTaken = await _context.Products
                .AnyAsync(p => p.TenantId == tenantId && p.Name == request.NewName && p.Id != product.Id);

            if (nameTaken)
            {
                throw new ConflictException($"Product '{request.NewName}' already exists.");
            }

            product.Name = request.NewName;
        }

        product.Price = request.Price;

        await _context.SaveChangesAsync();

        _cache.InvalidateTenantDashboard(product.TenantId);

        await LogActivityAsync(ActivityActions.Products.Updated, $"Updated product '{product.Name}'.");

        return MapToResponse(product);
    }

    public async Task DeleteAsync(DeleteProductRequest request)
    {
        var tenantId = RequireTenantId();

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Name == request.Name)
            ?? throw new NotFoundException($"Product '{request.Name}' was not found.");

        product.MarkDeleted();
        await _context.SaveChangesAsync();

        _cache.InvalidateTenantDashboard(product.TenantId);

        await LogActivityAsync(ActivityActions.Products.Deleted, $"Deleted product '{product.Name}'.");
    }

    private async Task LogActivityAsync(string action, string description)
    {
        await _activityLogService.LogAsync(new LogActivityRequest
        {
            UserId = RequireUserId(),
            Action = action,
            Module = ActivityModules.Products,
            Description = description,
        });
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
