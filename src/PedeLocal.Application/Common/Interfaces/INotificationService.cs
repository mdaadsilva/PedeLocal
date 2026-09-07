using PedeLocal.Domain.Notifications;

namespace PedeLocal.Application.Common.Interfaces;

/// <summary>Business-level notification API (docs section 57). Always persists a Notification
/// row (so /api/me/notifications has history even if push delivery fails or no device token is
/// registered yet); push delivery itself is delegated to IPushSender.</summary>
public interface INotificationService
{
    Task NotifyAsync(Guid userId, NotificationEventType type, string title, string body, string? dataJson = null, CancellationToken ct = default);
}
