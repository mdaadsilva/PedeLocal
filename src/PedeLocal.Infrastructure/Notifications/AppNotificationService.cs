using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Domain.Notifications;

namespace PedeLocal.Infrastructure.Notifications;

public class AppNotificationService(IAppDbContext db, IPushSender pushSender, ILogger<AppNotificationService> logger) : INotificationService
{
    public async Task NotifyAsync(Guid userId, NotificationEventType type, string title, string body, IDictionary<string, string>? data = null, CancellationToken ct = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            DataJson = data is null ? null : JsonSerializer.Serialize(data)
        };
        db.Notifications.Add(notification);

        var deviceTokens = await db.DeviceTokens.Where(t => t.UserId == userId && t.IsActive).ToListAsync(ct);
        var anySent = false;

        foreach (var deviceToken in deviceTokens)
        {
            // Push delivery is best-effort per device: one bad/expired token or one provider hiccup
            // must never block the others, never lose the persisted notification row (the reliable
            // GET /api/me/notifications fallback), and never fail whatever business operation
            // triggered this notification (order creation, status change, etc.).
            try
            {
                await pushSender.SendAsync(deviceToken.Token, title, body, data, ct);
                anySent = true;
            }
            catch (InvalidPushTokenException)
            {
                deviceToken.IsActive = false;
                logger.LogInformation("Deactivated an invalid/unregistered device token for user {UserId}.", userId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Push send failed for notification type {Type} to user {UserId} — history still recorded.", type, userId);
            }
        }

        if (anySent)
        {
            notification.SentAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }
}
