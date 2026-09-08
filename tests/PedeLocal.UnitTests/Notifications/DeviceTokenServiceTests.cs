using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Notifications;
using PedeLocal.Contracts.Notifications;
using PedeLocal.Domain.Identity;
using PedeLocal.Domain.Notifications;
using PedeLocal.UnitTests.TestSupport;
using Xunit;

namespace PedeLocal.UnitTests.Notifications;

public class DeviceTokenServiceTests
{
    private static (PedeLocal.Infrastructure.Persistence.PedeLocalDbContext Db, Guid UserId) SeedUser()
    {
        var db = InMemoryDbContextFactory.Create();
        var user = new User { Name = "Cliente", Email = $"{Guid.NewGuid():N}@example.com", PhoneNumber = "1", PasswordHash = "x" };
        db.Users.Add(user);
        db.SaveChanges();
        return (db, user.Id);
    }

    [Fact]
    public async Task Register_NewToken_PersistsUnderAuthenticatedUser()
    {
        var (db, userId) = SeedUser();
        var sut = new DeviceTokenService(db, new FakeCurrentUserService(userId));

        await sut.RegisterAsync(new RegisterDeviceTokenRequest("token-1", "Android"), CancellationToken.None);

        var token = Assert.Single(db.DeviceTokens);
        Assert.Equal(userId, token.UserId);
        Assert.Equal("token-1", token.Token);
        Assert.Equal(DevicePlatform.Android, token.Platform);
        Assert.True(token.IsActive);
    }

    [Fact]
    public async Task Register_SameTokenTwice_UpdatesInPlace_DoesNotDuplicate()
    {
        var (db, userId) = SeedUser();
        var sut = new DeviceTokenService(db, new FakeCurrentUserService(userId));

        await sut.RegisterAsync(new RegisterDeviceTokenRequest("token-1", "Android"), CancellationToken.None);
        await sut.RegisterAsync(new RegisterDeviceTokenRequest("token-1", "Ios"), CancellationToken.None);

        var token = Assert.Single(db.DeviceTokens);
        Assert.Equal(DevicePlatform.Ios, token.Platform); // updated, not duplicated.
    }

    [Fact]
    public async Task Register_TokenPreviouslyOwnedByAnotherUser_ReassignsOwnership()
    {
        var (db, firstUserId) = SeedUser();
        var secondUser = new User { Name = "Outro", Email = $"{Guid.NewGuid():N}@example.com", PhoneNumber = "1", PasswordHash = "x" };
        db.Users.Add(secondUser);
        db.SaveChanges();

        var firstSut = new DeviceTokenService(db, new FakeCurrentUserService(firstUserId));
        await firstSut.RegisterAsync(new RegisterDeviceTokenRequest("shared-device-token", "Android"), CancellationToken.None);

        // A different account logs in on the same physical device — the FCM token is reused.
        var secondSut = new DeviceTokenService(db, new FakeCurrentUserService(secondUser.Id));
        await secondSut.RegisterAsync(new RegisterDeviceTokenRequest("shared-device-token", "Android"), CancellationToken.None);

        var token = Assert.Single(db.DeviceTokens);
        Assert.Equal(secondUser.Id, token.UserId);
    }

    [Fact]
    public async Task Register_InvalidPlatform_Throws()
    {
        var (db, userId) = SeedUser();
        var sut = new DeviceTokenService(db, new FakeCurrentUserService(userId));

        await Assert.ThrowsAsync<AppException>(() =>
            sut.RegisterAsync(new RegisterDeviceTokenRequest("token-1", "SmartFridge"), CancellationToken.None));
        Assert.Empty(db.DeviceTokens);
    }

    [Fact]
    public async Task Remove_OwnToken_DeletesIt()
    {
        var (db, userId) = SeedUser();
        var sut = new DeviceTokenService(db, new FakeCurrentUserService(userId));
        await sut.RegisterAsync(new RegisterDeviceTokenRequest("token-1", "Android"), CancellationToken.None);

        await sut.RemoveAsync(new RemoveDeviceTokenRequest("token-1"), CancellationToken.None);

        Assert.Empty(db.DeviceTokens);
    }

    [Fact]
    public async Task Remove_AnotherUsersToken_DoesNotRemoveIt()
    {
        var (db, ownerUserId) = SeedUser();
        var strangerId = Guid.NewGuid();
        db.Users.Add(new User { Id = strangerId, Name = "Estranho", Email = $"{Guid.NewGuid():N}@example.com", PhoneNumber = "1", PasswordHash = "x" });
        db.SaveChanges();

        var ownerSut = new DeviceTokenService(db, new FakeCurrentUserService(ownerUserId));
        await ownerSut.RegisterAsync(new RegisterDeviceTokenRequest("owners-token", "Android"), CancellationToken.None);

        var strangerSut = new DeviceTokenService(db, new FakeCurrentUserService(strangerId));
        await strangerSut.RemoveAsync(new RemoveDeviceTokenRequest("owners-token"), CancellationToken.None);

        var token = Assert.Single(db.DeviceTokens);
        Assert.Equal("owners-token", token.Token);
        Assert.Equal(ownerUserId, token.UserId);
    }

    [Fact]
    public async Task Remove_UnknownToken_IsANoOp()
    {
        var (db, userId) = SeedUser();
        var sut = new DeviceTokenService(db, new FakeCurrentUserService(userId));

        var exception = await Record.ExceptionAsync(() =>
            sut.RemoveAsync(new RemoveDeviceTokenRequest("never-registered"), CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Register_Unauthenticated_Throws()
    {
        var db = InMemoryDbContextFactory.Create();
        var sut = new DeviceTokenService(db, new FakeCurrentUserService(userId: null));

        await Assert.ThrowsAsync<UnauthorizedAppException>(() =>
            sut.RegisterAsync(new RegisterDeviceTokenRequest("token-1", "Android"), CancellationToken.None));
    }
}
