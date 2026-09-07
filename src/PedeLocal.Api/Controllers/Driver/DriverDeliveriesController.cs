using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PedeLocal.Application.Deliveries;
using PedeLocal.Contracts.Deliveries;
using PedeLocal.Domain.Identity;

namespace PedeLocal.Api.Controllers.Driver;

[ApiController]
[Route("api/driver/deliveries")]
[Authorize(Roles = RoleNames.Driver)]
public class DriverDeliveriesController(DeliveryService deliveryService) : ControllerBase
{
    [HttpGet("available")]
    [ProducesResponseType(typeof(List<DeliveryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailable(CancellationToken ct) => Ok(await deliveryService.GetAvailableAsync(ct));

    [HttpGet("current")]
    [ProducesResponseType(typeof(DeliveryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetCurrent(CancellationToken ct)
    {
        var current = await deliveryService.GetCurrentAsync(ct);
        return current is null ? NoContent() : Ok(current);
    }

    [HttpPost("{id:guid}/accept")]
    [ProducesResponseType(typeof(DeliveryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct) => Ok(await deliveryService.AcceptAsync(id, ct));

    [HttpPost("{id:guid}/pickup")]
    [ProducesResponseType(typeof(DeliveryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Pickup(Guid id, CancellationToken ct) => Ok(await deliveryService.PickupAsync(id, ct));

    [HttpPost("{id:guid}/out-for-delivery")]
    [ProducesResponseType(typeof(DeliveryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> OutForDelivery(Guid id, CancellationToken ct) => Ok(await deliveryService.OutForDeliveryAsync(id, ct));

    [HttpPost("{id:guid}/confirm")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(DeliveryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Confirm(Guid id, ConfirmDeliveryRequest request, CancellationToken ct) =>
        Ok(await deliveryService.ConfirmAsync(id, request, ct));
}
