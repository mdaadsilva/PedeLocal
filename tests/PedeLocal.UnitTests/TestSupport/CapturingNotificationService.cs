using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Domain.Notifications;

namespace PedeLocal.UnitTests.TestSupport;

public record CapturedNotification(Guid UserId, NotificationEventType Type, string Title, string Body, IDictionary<string, string>? Data);

public class CapturingNotificationService : INotificationService
{
    public List<CapturedNotification> Notifications { get; } = [];

    public Task NotifyAsync(Guid userId, NotificationEventType type, string title, string body, IDictionary<string, string>? data = null, CancellationToken ct = default)
    {
        Notifications.Add(new CapturedNotification(userId, type, title, body, data));
        return Task.CompletedTask;
    }
}
