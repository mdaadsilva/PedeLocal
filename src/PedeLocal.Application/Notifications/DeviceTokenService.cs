using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Contracts.Notifications;
using PedeLocal.Domain.Notifications;

namespace PedeLocal.Application.Notifications;

/// <summary>Registers/removes FCM device tokens for the authenticated user
/// (docs/PUSH_NOTIFICATIONS.md). A user can have several active devices at once; the token string
/// itself is the natural unique key — see <see cref="DeviceToken"/> for why re-registering an
/// already-known token reassigns rather than duplicates. The user is always the one derived from the
/// JWT (<see cref="ICurrentUserService"/>) — a request body never carries a userId.</summary>
public class DeviceTokenService(IAppDbContext db, ICurrentUserService currentUser)
{
    public async Task RegisterAsync(RegisterDeviceTokenRequest request, CancellationToken ct)
    {
        var userId = RequireUserId();
        var token = request.Token.Trim();
        if (!Enum.TryParse<DevicePlatform>(request.Platform, ignoreCase: true, out var platform))
        {
            throw new AppException("INVALID_DEVICE_PLATFORM", "Plataforma de dispositivo inválida.");
        }

        var existing = await db.DeviceTokens.SingleOrDefaultAsync(t => t.Token == token, ct);
        if (existing is null)
        {
            db.DeviceTokens.Add(new DeviceToken { UserId = userId, Token = token, Platform = platform, IsActive = true });
        }
        else
        {
            // Reassigns ownership when this exact token used to belong to a different user — e.g. a
            // shared or factory-reset device where a different account is now logged in. Legitimate:
            // FCM tokens are unique per app install, not per account.
            existing.UserId = userId;
            existing.Platform = platform;
            existing.IsActive = true;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Idempotent no-op if the token doesn't exist or belongs to a different user — never
    /// reveals which case it was, and never removes another user's token.</summary>
    public async Task RemoveAsync(RemoveDeviceTokenRequest request, CancellationToken ct)
    {
        var userId = RequireUserId();
        var token = request.Token.Trim();

        var existing = await db.DeviceTokens.SingleOrDefaultAsync(t => t.Token == token && t.UserId == userId, ct);
        if (existing is not null)
        {
            db.DeviceTokens.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
    }

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new UnauthorizedAppException("UNAUTHENTICATED", "Usuário não autenticado.");
}
