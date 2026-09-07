using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Drivers;
using PedeLocal.Contracts.Common;
using PedeLocal.Contracts.Drivers;
using PedeLocal.Domain.Identity;

namespace PedeLocal.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/drivers")]
[Authorize(Roles = RoleNames.SuperAdmin)]
public class AdminDriversController(DriverService driverService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DriverResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? approvalStatus, [FromQuery] PageRequest request, CancellationToken ct) =>
        Ok(await driverService.GetAllAsync(approvalStatus, request, ct));

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(DriverResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct) => Ok(await driverService.ApproveAsync(id, ct));

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(DriverResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(Guid id, RejectDriverRequest request, CancellationToken ct) =>
        Ok(await driverService.RejectAsync(id, request, ct));

    [HttpPost("{id:guid}/block")]
    [ProducesResponseType(typeof(DriverResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Block(Guid id, CancellationToken ct) => Ok(await driverService.SetBlockedAsync(id, true, ct));

    [HttpPost("{id:guid}/unblock")]
    [ProducesResponseType(typeof(DriverResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Unblock(Guid id, CancellationToken ct) => Ok(await driverService.SetBlockedAsync(id, false, ct));
}
