using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Domain.Notifications;

namespace PedeLocal.Infrastructure.Notifications;

public class AppNotificationService(IAppDbContext db, IPushSender pushSender) : INotificationService
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
            await pushSender.SendAsync(user.FcmToken, title, body, null, ct);
            notification.SentAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }
}
