using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PedeLocal.Application.Me;
using PedeLocal.Application.Notifications;
using PedeLocal.Contracts.Common;
using PedeLocal.Contracts.Me;
using PedeLocal.Contracts.Notifications;

namespace PedeLocal.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController(MeService meService, DeviceTokenService deviceTokenService) : ControllerBase
{
    [HttpGet("profile")]
    [ProducesResponseType(typeof(MyProfileResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile(CancellationToken ct) => Ok(await meService.GetProfileAsync(ct));

    [HttpPut("profile")]
    [ProducesResponseType(typeof(MyProfileResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken ct) =>
        Ok(await meService.UpdateProfileAsync(request, ct));

    /// <summary>Registers (or refreshes) one device's FCM token for push notifications — call on
    /// every app start/token rotation. Idempotent: same token twice just updates it, never
    /// duplicates. See docs/PUSH_NOTIFICATIONS.md.</summary>
    [HttpPut("device-tokens")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RegisterDeviceToken(RegisterDeviceTokenRequest request, CancellationToken ct)
    {
        await deviceTokenService.RegisterAsync(request, ct);
        return NoContent();
    }

    /// <summary>Removes one device token — call on logout/uninstall so that device stops receiving
    /// push. Idempotent no-op if the token isn't registered to the caller.</summary>
    [HttpDelete("device-tokens")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveDeviceToken(RemoveDeviceTokenRequest request, CancellationToken ct)
    {
        await deviceTokenService.RemoveAsync(request, ct);
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
