using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Domain.Notifications;

namespace PedeLocal.UnitTests.TestSupport;

public class NoOpNotificationService : INotificationService
{
    public Task NotifyAsync(Guid userId, NotificationEventType type, string title, string body, string? dataJson = null, CancellationToken ct = default) =>
        Task.CompletedTask;
}
