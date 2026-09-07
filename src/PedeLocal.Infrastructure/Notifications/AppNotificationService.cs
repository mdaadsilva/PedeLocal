using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Domain.Notifications;

namespace PedeLocal.Infrastructure.Notifications;

public class AppNotificationService(IAppDbContext db, IPushSender pushSender, ILogger<AppNotificationService> logger) : INotificationService
{
    public async Task NotifyAsync(Guid userId, NotificationEventType type, string title, string body, string? dataJson = null, CancellationToken ct = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            DataJson = dataJson
        };
        db.Notifications.Add(notification);

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
        if (!string.IsNullOrWhiteSpace(user?.FcmToken))
        {
            // Push delivery is best-effort: a provider outage must never block persisting the
            // notification row (GET /api/me/notifications is the reliable fallback) or fail
            // whatever business operation triggered this notification.
            try
            {
                await pushSender.SendAsync(user.FcmToken, title, body, null, ct);
                notification.SentAtUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Push send failed for notification type {Type} to user {UserId} — history still recorded.", type, userId);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
