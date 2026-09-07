using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Orders;
using PedeLocal.Contracts.Common;
using PedeLocal.Contracts.Deliveries;
using PedeLocal.Contracts.Orders;

namespace PedeLocal.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController(OrderService orderService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken ct)
    {
        var response = await orderService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpPost("preview")]
    [ProducesResponseType(typeof(OrderPreviewResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview(CreateOrderRequest request, CancellationToken ct) =>
        Ok(await orderService.PreviewAsync(request, ct));

    [HttpGet("{id:guid}/delivery-code")]
    [ProducesResponseType(typeof(DeliveryCodeResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDeliveryCode(Guid id, CancellationToken ct) =>
        Ok(await orderService.GetDeliveryCodeAsync(id, ct));

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine([FromQuery] PageRequest request, CancellationToken ct) =>
        Ok(await orderService.GetMineAsync(request, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct) =>
        Ok(await orderService.GetForCustomerAsync(id, ct));
}
