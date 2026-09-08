using PedeLocal.Domain.Notifications;

namespace PedeLocal.Application.Common.Interfaces;

/// <summary>Business-level notification API (docs/PUSH_NOTIFICATIONS.md). Always persists a
/// Notification row (so /api/me/notifications has history even if push delivery fails or the user
/// has no device registered) and fans out a push to every one of the user's active device tokens;
/// push delivery itself is delegated to IPushSender.</summary>
public interface INotificationService
{
    /// <param name="data">Small set of identifiers only (e.g. orderId, eventType) — never personal
    /// data. Stored as the Notification row's DataJson and passed through as the FCM data
    /// payload.</param>
    Task NotifyAsync(Guid userId, NotificationEventType type, string title, string body, IDictionary<string, string>? data = null, CancellationToken ct = default);
}
