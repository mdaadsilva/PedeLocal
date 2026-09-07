using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Inventory;
using PedeLocal.Contracts.Common;
using PedeLocal.Contracts.Inventory;

namespace PedeLocal.Api.Controllers.Merchant;

[ApiController]
[Route("api/merchant/establishments/{establishmentId:guid}/products/{productId:guid}/inventory")]
[Authorize]
public class MerchantInventoryController(InventoryService inventoryService) : ControllerBase
{
    [HttpPost("adjust")]
    [ProducesResponseType(typeof(InventoryMovementResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Adjust(Guid establishmentId, Guid productId, AdjustStockRequest request, CancellationToken ct)
    {
        var response = await inventoryService.AdjustStockAsync(establishmentId, productId, request, ct);
        return CreatedAtAction(nameof(GetMovements), new { establishmentId, productId }, response);
    }

    [HttpGet("movements")]
    [ProducesResponseType(typeof(PagedResult<InventoryMovementResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMovements(Guid establishmentId, Guid productId, [FromQuery] PageRequest request, CancellationToken ct) =>
        Ok(await inventoryService.GetMovementsAsync(establishmentId, productId, request, ct));
}
