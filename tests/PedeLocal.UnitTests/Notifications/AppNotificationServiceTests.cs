using Microsoft.Extensions.Logging.Abstractions;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Domain.Identity;
using PedeLocal.Domain.Notifications;
using PedeLocal.Infrastructure.Notifications;
using PedeLocal.UnitTests.TestSupport;
using Xunit;

namespace PedeLocal.UnitTests.Notifications;

file class FakePushSender : IPushSender
{
    public string? LastTitle { get; private set; }
    public string? LastToken { get; private set; }
    public int CallCount { get; private set; }
    public bool ThrowOnSend { get; set; }

    public Task SendAsync(string? deviceToken, string title, string body, IDictionary<string, string>? data, CancellationToken ct = default)
    {
        CallCount++;
        if (ThrowOnSend)
        {
            throw new InvalidOperationException("Simulated push provider outage.");
        }
        LastToken = deviceToken;
        LastTitle = title;
        return Task.CompletedTask;
    }
}

public class AppNotificationServiceTests
{
    [Fact]
    public async Task Notify_PersistsRow_AndSkipsPush_WhenNoFcmToken()
    {
        var db = InMemoryDbContextFactory.Create();
        var user = new User { Name = "Maria", Email = "m@example.com", PhoneNumber = "1", PasswordHash = "x" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var pushSender = new FakePushSender();
        var sut = new AppNotificationService(db, pushSender, NullLogger<AppNotificationService>.Instance);

        await sut.NotifyAsync(user.Id, NotificationEventType.OrderAccepted, "Pedido aceito", "corpo", ct: CancellationToken.None);

        var notification = Assert.Single(db.Notifications);
        Assert.Equal("Pedido aceito", notification.Title);
        Assert.Null(notification.SentAtUtc);
        Assert.Equal(0, pushSender.CallCount);
    }

    [Fact]
    public async Task Notify_SendsPush_WhenFcmTokenPresent()
    {
        var db = InMemoryDbContextFactory.Create();
        var user = new User { Name = "Maria", Email = "m@example.com", PhoneNumber = "1", PasswordHash = "x", FcmToken = "device-token" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var pushSender = new FakePushSender();
        var sut = new AppNotificationService(db, pushSender, NullLogger<AppNotificationService>.Instance);

        await sut.NotifyAsync(user.Id, NotificationEventType.OrderDelivered, "Entregue", "corpo", ct: CancellationToken.None);

        Assert.Equal(1, pushSender.CallCount);
        Assert.Equal("device-token", pushSender.LastToken);
        var notification = Assert.Single(db.Notifications);
        Assert.NotNull(notification.SentAtUtc);
    }

    [Fact]
    public async Task Notify_PushProviderThrows_StillPersistsRow_AndDoesNotThrow()
    {
        var db = InMemoryDbContextFactory.Create();
        var user = new User { Name = "Maria", Email = "m@example.com", PhoneNumber = "1", PasswordHash = "x", FcmToken = "device-token" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var pushSender = new FakePushSender { ThrowOnSend = true };
        var sut = new AppNotificationService(db, pushSender, NullLogger<AppNotificationService>.Instance);

        var exception = await Record.ExceptionAsync(() =>
            sut.NotifyAsync(user.Id, NotificationEventType.OrderDelivered, "Entregue", "corpo", ct: CancellationToken.None));

        Assert.Null(exception);
        var notification = Assert.Single(db.Notifications);
        Assert.Null(notification.SentAtUtc); // push never actually succeeded.
    }
}
