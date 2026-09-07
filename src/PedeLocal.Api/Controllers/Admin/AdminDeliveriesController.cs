using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Deliveries;
using PedeLocal.Contracts.Common;
using PedeLocal.Contracts.Deliveries;
using PedeLocal.Domain.Identity;

namespace PedeLocal.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/deliveries")]
[Authorize(Roles = RoleNames.SuperAdmin)]
public class AdminDeliveriesController(DeliveryService deliveryService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DeliveryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? cityId, [FromQuery] string? status, [FromQuery] PageRequest request, CancellationToken ct) =>
        Ok(await deliveryService.GetForAdminAsync(cityId, status, request, ct));
}
