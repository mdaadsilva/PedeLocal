using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Drivers;
using PedeLocal.Contracts.Drivers;
using PedeLocal.Domain.Identity;

namespace PedeLocal.Api.Controllers.Driver;

[ApiController]
[Route("api/driver/me")]
[Authorize(Roles = RoleNames.Driver)]
public class DriverProfileController(DriverService driverService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(DriverResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyProfile(CancellationToken ct) => Ok(await driverService.GetMyProfileAsync(ct));

    [HttpPost("status/online")]
    [ProducesResponseType(typeof(DriverResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GoOnline(CancellationToken ct) => Ok(await driverService.GoOnlineAsync(ct));

    [HttpPost("status/offline")]
    [ProducesResponseType(typeof(DriverResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GoOffline(CancellationToken ct) => Ok(await driverService.GoOfflineAsync(ct));
}
