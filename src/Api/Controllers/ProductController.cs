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
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var response = await _productService.GetAllAsync(page, pageSize);

        return OkEnvelope(response, "Products retrieved.");
    }

    [HttpGet("by-name")]
    [HasPermission(PermissionNames.ProductsView)]
    public async Task<IActionResult> GetByName([FromQuery] string name)
    {
        var response = await _productService.GetByNameAsync(name);

        return OkEnvelope(response, "Product retrieved.");
    }

    [HttpPost]
    [HasPermission(PermissionNames.ProductsCreate)]
    public async Task<IActionResult> Create(CreateProductRequest request)
    {
        var response = await _productService.CreateAsync(request);

        return StatusCode(StatusCodes.Status201Created, new Api.Contracts.ApiEnvelope<ProductResponse>
        {
            Data = response,
            Message = "Product created.",
            TraceId = HttpContext.TraceIdentifier,
        });
    }

    [HttpPut]
    [HasPermission(PermissionNames.ProductsEdit)]
    public async Task<IActionResult> Update(UpdateProductRequest request)
    {
        var response = await _productService.UpdateAsync(request);

        return OkEnvelope(response, "Product updated.");
    }

    [HttpDelete("{name}")]
    [HasPermission(PermissionNames.ProductsDelete)]
    public async Task<IActionResult> Delete(string name)
    {
        await _productService.DeleteAsync(new DeleteProductRequest { Name = name });

        return OkEnvelope("Product deleted.");
    }
}
