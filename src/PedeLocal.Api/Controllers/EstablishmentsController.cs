using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Catalog;
using PedeLocal.Application.Establishments;
using PedeLocal.Contracts.Catalog;
using PedeLocal.Contracts.Common;
using PedeLocal.Contracts.Establishments;

namespace PedeLocal.Api.Controllers;

[ApiController]
[Route("api/establishments")]
[Authorize]
public class EstablishmentsController(EstablishmentService establishmentService, CatalogService catalogService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EstablishmentResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] Guid cityId, [FromQuery] PageRequest request, CancellationToken ct) =>
        Ok(await establishmentService.SearchForCustomersAsync(cityId, request, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EstablishmentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await establishmentService.GetForCustomerAsync(id, ct));

    [HttpGet("{id:guid}/products")]
    [ProducesResponseType(typeof(List<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProducts(Guid id, CancellationToken ct) =>
        Ok(await catalogService.GetPublicProductsAsync(id, ct));
}
