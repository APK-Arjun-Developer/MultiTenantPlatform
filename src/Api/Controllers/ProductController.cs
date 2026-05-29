using Api.Attributes;
using Application.Common;
using Application.DTOs.Products;
using Application.Interfaces.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/products")]
[Authorize]
public class ProductController : ApiControllerBase
{
    private readonly IProductService _productService;

    public ProductController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    [HasPermission(PermissionNames.ProductsView)]
    public async Task<IActionResult> GetAll()
    {
        var response = await _productService.GetAllAsync();

        return OkEnvelope(response, "Products retrieved.");
    }

    [HttpGet("by-name")]
    [HasPermission(PermissionNames.ProductsView)]
    public async Task<IActionResult> GetByName([FromQuery] string name)
    {
        var response = await _productService.GetByNameAsync(name);

        return OkEnvelope(response, "Products retrieved.");
    }

    [HttpPost]
    [HasPermission(PermissionNames.ProductsCreate)]
    public async Task<IActionResult> Create(CreateProductRequest request)
    {
        var response = await _productService.CreateAsync(request);

        return OkEnvelope(response, "Product created.");
    }

    [HttpPut]
    [HasPermission(PermissionNames.ProductsEdit)]
    public async Task<IActionResult> Update(UpdateProductRequest request)
    {
        var response = await _productService.UpdateAsync(request);

        return OkEnvelope(response, "Product updated.");
    }

    [HttpDelete]
    [HasPermission(PermissionNames.ProductsDelete)]
    public async Task<IActionResult> Delete(DeleteProductRequest request)
    {
        await _productService.DeleteAsync(request);

        return OkEnvelope("Product deleted.");
    }
}
