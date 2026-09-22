using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Deliveries;
using PedeLocal.Application.Orders;
using PedeLocal.Contracts.Common;
using PedeLocal.Contracts.Deliveries;
using PedeLocal.Contracts.Orders;

namespace PedeLocal.Api.Controllers.Merchant;

[ApiController]
[Route("api/merchant/establishments/{establishmentId:guid}/orders")]
[Authorize]
public class MerchantOrdersController(OrderService orderService, DeliveryService deliveryService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(Guid establishmentId, [FromQuery] string? status, [FromQuery] PageRequest request, CancellationToken ct) =>
        Ok(await orderService.GetForMerchantAsync(establishmentId, status, request, ct));

    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid establishmentId, Guid orderId, CancellationToken ct) =>
        Ok(await orderService.GetForMerchantAsync(establishmentId, orderId, ct));

    [HttpPost("{orderId:guid}/accept")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Accept(Guid establishmentId, Guid orderId, CancellationToken ct) =>
        Ok(await orderService.AcceptAsync(establishmentId, orderId, ct));

    [HttpPost("{orderId:guid}/preparing")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkPreparing(Guid establishmentId, Guid orderId, CancellationToken ct) =>
        Ok(await orderService.MarkPreparingAsync(establishmentId, orderId, ct));

    [HttpPost("{orderId:guid}/ready")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkReady(Guid establishmentId, Guid orderId, CancellationToken ct) =>
        Ok(await orderService.MarkReadyAsync(establishmentId, orderId, ct));

    [HttpPost("{orderId:guid}/complete-pickup")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompletePickup(Guid establishmentId, Guid orderId, CancellationToken ct) =>
        Ok(await orderService.CompletePickupAsync(establishmentId, orderId, ct));

    [HttpPost("{orderId:guid}/cancel")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(Guid establishmentId, Guid orderId, CancelOrderRequest request, CancellationToken ct) =>
        Ok(await orderService.CancelAsync(establishmentId, orderId, request, ct));

    [HttpGet("{orderId:guid}/occurrences")]
    [ProducesResponseType(typeof(List<DeliveryOccurrenceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOccurrences(Guid establishmentId, Guid orderId, CancellationToken ct) =>
        Ok(await deliveryService.GetOccurrencesForMerchantAsync(establishmentId, orderId, ct));
}
