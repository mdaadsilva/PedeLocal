using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Catalog;
using PedeLocal.Contracts.Catalog;

namespace PedeLocal.Api.Controllers.Merchant;

[ApiController]
[Route("api/merchant/establishments/{establishmentId:guid}")]
[Authorize]
public class MerchantCatalogController(CatalogService catalogService) : ControllerBase
{
    [HttpGet("menu-categories")]
    [ProducesResponseType(typeof(List<MenuCategoryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMenuCategories(Guid establishmentId, CancellationToken ct) =>
        Ok(await catalogService.GetMenuCategoriesAsync(establishmentId, ct));

    [HttpPost("menu-categories")]
    [ProducesResponseType(typeof(MenuCategoryResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateMenuCategory(Guid establishmentId, CreateMenuCategoryRequest request, CancellationToken ct)
    {
        var response = await catalogService.CreateMenuCategoryAsync(establishmentId, request, ct);
        return CreatedAtAction(nameof(GetMenuCategories), new { establishmentId }, response);
    }

    [HttpPut("menu-categories/{categoryId:guid}")]
    [ProducesResponseType(typeof(MenuCategoryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMenuCategory(Guid establishmentId, Guid categoryId, UpdateMenuCategoryRequest request, CancellationToken ct) =>
        Ok(await catalogService.UpdateMenuCategoryAsync(establishmentId, categoryId, request, ct));

    [HttpGet("products")]
    [ProducesResponseType(typeof(List<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProducts(Guid establishmentId, CancellationToken ct) =>
        Ok(await catalogService.GetProductsAsync(establishmentId, ct));

    [HttpGet("products/{productId:guid}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProduct(Guid establishmentId, Guid productId, CancellationToken ct) =>
        Ok(await catalogService.GetProductByIdAsync(establishmentId, productId, ct));

    [HttpPost("products")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateProduct(Guid establishmentId, CreateProductRequest request, CancellationToken ct)
    {
        var response = await catalogService.CreateProductAsync(establishmentId, request, ct);
        return CreatedAtAction(nameof(GetProduct), new { establishmentId, productId = response.Id }, response);
    }

    [HttpPut("products/{productId:guid}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProduct(Guid establishmentId, Guid productId, UpdateProductRequest request, CancellationToken ct) =>
        Ok(await catalogService.UpdateProductAsync(establishmentId, productId, request, ct));

    [HttpPatch("products/{productId:guid}/availability")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetAvailability(Guid establishmentId, Guid productId, UpdateProductAvailabilityRequest request, CancellationToken ct) =>
        Ok(await catalogService.SetProductAvailabilityAsync(establishmentId, productId, request, ct));
}
