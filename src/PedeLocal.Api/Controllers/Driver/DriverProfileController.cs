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

    [HttpGet("documents")]
    [ProducesResponseType(typeof(DriverDetailResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyDocuments(CancellationToken ct) => Ok(await driverService.GetMyDocumentsAsync(ct));

    [HttpPut("documents/license")]
    [ProducesResponseType(typeof(DriverDetailResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitLicense(SubmitDriverLicenseRequest request, CancellationToken ct) =>
        Ok(await driverService.SubmitLicenseAsync(request, ct));

    [HttpPut("documents/vehicle")]
    [ProducesResponseType(typeof(DriverDetailResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitVehicle(SubmitDriverVehicleRequest request, CancellationToken ct) =>
        Ok(await driverService.SubmitVehicleAsync(request, ct));

    [HttpPost("status/online")]
    [ProducesResponseType(typeof(DriverResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GoOnline(CancellationToken ct) => Ok(await driverService.GoOnlineAsync(ct));

    [HttpPost("status/offline")]
    [ProducesResponseType(typeof(DriverResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GoOffline(CancellationToken ct) => Ok(await driverService.GoOfflineAsync(ct));
}
