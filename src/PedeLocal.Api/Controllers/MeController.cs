using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PedeLocal.Application.Me;
using PedeLocal.Contracts.Common;
using PedeLocal.Contracts.Me;

namespace PedeLocal.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController(MeService meService) : ControllerBase
{
    [HttpGet("profile")]
    [ProducesResponseType(typeof(MyProfileResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile(CancellationToken ct) => Ok(await meService.GetProfileAsync(ct));

    [HttpPut("profile")]
    [ProducesResponseType(typeof(MyProfileResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken ct) =>
        Ok(await meService.UpdateProfileAsync(request, ct));

    [HttpPut("fcm-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateFcmToken(UpdateFcmTokenRequest request, CancellationToken ct)
    {
        await meService.UpdateFcmTokenAsync(request, ct);
        return NoContent();
    }

    [HttpGet("notifications")]
    [ProducesResponseType(typeof(PagedResult<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotifications([FromQuery] PageRequest request, CancellationToken ct) =>
        Ok(await meService.GetNotificationsAsync(request, ct));

    [HttpPost("notifications/{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkNotificationRead(Guid id, CancellationToken ct)
    {
        await meService.MarkNotificationReadAsync(id, ct);
        return NoContent();
    }

    [HttpGet("data")]
    [ProducesResponseType(typeof(MyDataExportResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyData(CancellationToken ct) => Ok(await meService.GetMyDataAsync(ct));

    [HttpDelete("account")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAccount(CancellationToken ct)
    {
        await meService.DeleteAccountAsync(ct);
        return NoContent();
    }
}
