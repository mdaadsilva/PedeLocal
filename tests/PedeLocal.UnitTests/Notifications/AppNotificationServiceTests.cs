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
    public List<string> SentToTokens { get; } = [];
    public HashSet<string> InvalidTokens { get; } = [];
    public HashSet<string> ThrowingTokens { get; } = [];

    public Task SendAsync(string deviceToken, string title, string body, IDictionary<string, string>? data, CancellationToken ct = default)
    {
        if (InvalidTokens.Contains(deviceToken))
        {
            throw new InvalidPushTokenException("Simulated invalid/unregistered token.");
        }
        if (ThrowingTokens.Contains(deviceToken))
        {
            throw new InvalidOperationException("Simulated push provider outage.");
        }

        SentToTokens.Add(deviceToken);
        return Task.CompletedTask;
    }
}

public class AppNotificationServiceTests
{
    private static User AddUser(PedeLocal.Infrastructure.Persistence.PedeLocalDbContext db)
    {
        var user = new User { Name = "Maria", Email = $"{Guid.NewGuid():N}@example.com", PhoneNumber = "1", PasswordHash = "x" };
        db.Users.Add(user);
        return user;
    }

    private static DeviceToken AddDeviceToken(PedeLocal.Infrastructure.Persistence.PedeLocalDbContext db, Guid userId, string token, bool isActive = true)
    {
        var deviceToken = new DeviceToken { UserId = userId, Token = token, Platform = DevicePlatform.Android, IsActive = isActive };
        db.DeviceTokens.Add(deviceToken);
        return deviceToken;
    }

    [Fact]
    public async Task Notify_PersistsRow_AndSendsNoPush_WhenNoDeviceTokens()
    {
        var db = InMemoryDbContextFactory.Create();
        var user = AddUser(db);
        await db.SaveChangesAsync();

        var pushSender = new FakePushSender();
        var sut = new AppNotificationService(db, pushSender, NullLogger<AppNotificationService>.Instance);

        await sut.NotifyAsync(user.Id, NotificationEventType.OrderAccepted, "Pedido aceito", "corpo", ct: CancellationToken.None);

        var notification = Assert.Single(db.Notifications);
        Assert.Equal("Pedido aceito", notification.Title);
        Assert.Null(notification.SentAtUtc);
        Assert.Empty(pushSender.SentToTokens);
    }

    [Fact]
    public async Task Notify_SendsPush_ToEveryActiveDeviceToken_ButNotInactiveOnes()
    {
        var db = InMemoryDbContextFactory.Create();
        var user = AddUser(db);
        AddDeviceToken(db, user.Id, "token-phone");
        AddDeviceToken(db, user.Id, "token-tablet");
        AddDeviceToken(db, user.Id, "token-old-inactive", isActive: false);
        await db.SaveChangesAsync();

        var pushSender = new FakePushSender();
        var sut = new AppNotificationService(db, pushSender, NullLogger<AppNotificationService>.Instance);

        await sut.NotifyAsync(user.Id, NotificationEventType.OrderDelivered, "Entregue", "corpo", ct: CancellationToken.None);

        Assert.Equal(2, pushSender.SentToTokens.Count);
        Assert.Contains("token-phone", pushSender.SentToTokens);
        Assert.Contains("token-tablet", pushSender.SentToTokens);
        Assert.DoesNotContain("token-old-inactive", pushSender.SentToTokens);
        var notification = Assert.Single(db.Notifications);
        Assert.NotNull(notification.SentAtUtc);
    }

    [Fact]
    public async Task Notify_NeverSendsToAnotherUsersDeviceToken()
    {
        var db = InMemoryDbContextFactory.Create();
        var user = AddUser(db);
        var otherUser = AddUser(db);
        AddDeviceToken(db, otherUser.Id, "token-belongs-to-other-user");
        await db.SaveChangesAsync();

        var pushSender = new FakePushSender();
        var sut = new AppNotificationService(db, pushSender, NullLogger<AppNotificationService>.Instance);

        await sut.NotifyAsync(user.Id, NotificationEventType.OrderAccepted, "Pedido aceito", "corpo", ct: CancellationToken.None);

        Assert.Empty(pushSender.SentToTokens);
    }

    [Fact]
    public async Task Notify_PushProviderThrows_StillPersistsRow_AndDoesNotThrow()
    {
        var db = InMemoryDbContextFactory.Create();
        var user = AddUser(db);
        AddDeviceToken(db, user.Id, "token-flaky");
        await db.SaveChangesAsync();

        var pushSender = new FakePushSender();
        pushSender.ThrowingTokens.Add("token-flaky");
        var sut = new AppNotificationService(db, pushSender, NullLogger<AppNotificationService>.Instance);

        var exception = await Record.ExceptionAsync(() =>
            sut.NotifyAsync(user.Id, NotificationEventType.OrderDelivered, "Entregue", "corpo", ct: CancellationToken.None));

        Assert.Null(exception);
        var notification = Assert.Single(db.Notifications);
        Assert.Null(notification.SentAtUtc); // push never actually succeeded.
        // The token itself is untouched by a transient failure — still active for next time.
        Assert.True(db.DeviceTokens.Single(t => t.Token == "token-flaky").IsActive);
    }

    [Fact]
    public async Task Notify_InvalidTokenReported_DeactivatesThatTokenOnly()
    {
        var db = InMemoryDbContextFactory.Create();
        var user = AddUser(db);
        AddDeviceToken(db, user.Id, "token-good");
        AddDeviceToken(db, user.Id, "token-invalid");
        await db.SaveChangesAsync();

        var pushSender = new FakePushSender();
        pushSender.InvalidTokens.Add("token-invalid");
        var sut = new AppNotificationService(db, pushSender, NullLogger<AppNotificationService>.Instance);

        await sut.NotifyAsync(user.Id, NotificationEventType.OrderDelivered, "Entregue", "corpo", ct: CancellationToken.None);

        Assert.False(db.DeviceTokens.Single(t => t.Token == "token-invalid").IsActive);
        Assert.True(db.DeviceTokens.Single(t => t.Token == "token-good").IsActive);
        // The still-valid token got the push and the notification counts as sent.
        Assert.Contains("token-good", pushSender.SentToTokens);
        Assert.NotNull(Assert.Single(db.Notifications).SentAtUtc);
    }

    [Fact]
    public async Task Notify_PersistsDataPayload_AsJson()
    {
        var db = InMemoryDbContextFactory.Create();
        var user = AddUser(db);
        await db.SaveChangesAsync();

        var sut = new AppNotificationService(db, new FakePushSender(), NullLogger<AppNotificationService>.Instance);
        var data = new Dictionary<string, string> { ["orderId"] = "abc-123", ["eventType"] = "OrderAccepted" };

        await sut.NotifyAsync(user.Id, NotificationEventType.OrderAccepted, "Pedido aceito", "corpo", data, CancellationToken.None);

        var notification = Assert.Single(db.Notifications);
        Assert.Contains("abc-123", notification.DataJson);
        Assert.Contains("OrderAccepted", notification.DataJson);
    }
}
