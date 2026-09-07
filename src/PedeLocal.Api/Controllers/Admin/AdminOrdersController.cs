using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Orders;
using PedeLocal.Contracts.Common;
using PedeLocal.Contracts.Orders;
using PedeLocal.Domain.Identity;

namespace PedeLocal.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Roles = RoleNames.SuperAdmin)]
public class AdminOrdersController(OrderService orderService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? establishmentId, [FromQuery] string? status, [FromQuery] PageRequest request, CancellationToken ct) =>
        Ok(await orderService.GetForAdminAsync(establishmentId, status, request, ct));
}
