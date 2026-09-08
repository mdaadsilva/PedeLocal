using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Domain.Notifications;

namespace PedeLocal.UnitTests.TestSupport;

public record CapturedNotification(Guid UserId, NotificationEventType Type, string Title, string Body);

public class CapturingNotificationService : INotificationService
{
    public List<CapturedNotification> Notifications { get; } = [];

    public Task NotifyAsync(Guid userId, NotificationEventType type, string title, string body, string? dataJson = null, CancellationToken ct = default)
    {
        Notifications.Add(new CapturedNotification(userId, type, title, body));
        return Task.CompletedTask;
    }
}
