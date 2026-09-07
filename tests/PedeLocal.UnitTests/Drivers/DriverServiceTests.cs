using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Drivers;
using PedeLocal.Contracts.Drivers;
using PedeLocal.Domain.Drivers;
using PedeLocal.Domain.Identity;
using PedeLocal.UnitTests.TestSupport;
using Xunit;

namespace PedeLocal.UnitTests.Drivers;

public class DriverServiceTests
{
    private static async Task<(PedeLocal.Infrastructure.Persistence.PedeLocalDbContext Db, Domain.Drivers.Driver Driver, Guid UserId)> SeedAsync(
        DriverApprovalStatus approvalStatus = DriverApprovalStatus.PendingApproval, DriverStatus status = DriverStatus.Offline)
    {
        var db = InMemoryDbContextFactory.Create();
        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Name = "João", Email = "joao@example.com", PhoneNumber = "1", PasswordHash = "x" });
        var driver = new Domain.Drivers.Driver
        {
            UserId = userId, CityId = Guid.NewGuid(), Cpf = "12345678901", VehicleType = VehicleType.Motorcycle,
            PixKeyType = PixKeyType.Cpf, PixKey = "12345678901", ApprovalStatus = approvalStatus, Status = status
        };
        db.Drivers.Add(driver);
        await db.SaveChangesAsync();
        return (db, driver, userId);
    }

    [Fact]
    public async Task Approve_SetsApprovedAndTimestamp()
    {
        var (db, driver, _) = await SeedAsync();
        var sut = new DriverService(db, new FakeCurrentUserService(Guid.NewGuid(), RoleNames.SuperAdmin));

        var response = await sut.ApproveAsync(driver.Id, CancellationToken.None);

        Assert.Equal("Approved", response.ApprovalStatus);
        Assert.NotNull(response.ApprovedAtUtc);
    }

    [Fact]
    public async Task Approve_IsIdempotent_DoesNotDuplicateAuditLog()
    {
        var (db, driver, _) = await SeedAsync();
        var sut = new DriverService(db, new FakeCurrentUserService(Guid.NewGuid(), RoleNames.SuperAdmin));

        await sut.ApproveAsync(driver.Id, CancellationToken.None);
        await sut.ApproveAsync(driver.Id, CancellationToken.None);

        Assert.Single(db.AuditLogs);
    }

    [Fact]
    public async Task GoOnline_WhenPendingApproval_Throws()
    {
        var (db, driver, userId) = await SeedAsync();
        var sut = new DriverService(db, new FakeCurrentUserService(userId));

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GoOnlineAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GoOnline_WhenApproved_SetsAvailable()
    {
        var (db, driver, userId) = await SeedAsync(DriverApprovalStatus.Approved);
        var sut = new DriverService(db, new FakeCurrentUserService(userId));

        var response = await sut.GoOnlineAsync(CancellationToken.None);

        Assert.Equal("Available", response.Status);
    }

    [Fact]
    public async Task GoOnline_WhenBlocked_Throws()
    {
        var (db, driver, userId) = await SeedAsync(DriverApprovalStatus.Approved, DriverStatus.Blocked);
        var sut = new DriverService(db, new FakeCurrentUserService(userId));

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GoOnlineAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GoOffline_WhenBusy_Throws()
    {
        var (db, driver, userId) = await SeedAsync(DriverApprovalStatus.Approved, DriverStatus.Busy);
        var sut = new DriverService(db, new FakeCurrentUserService(userId));

        await Assert.ThrowsAsync<ConflictException>(() => sut.GoOfflineAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Reject_SetsRejectedStatus()
    {
        var (db, driver, _) = await SeedAsync();
        var sut = new DriverService(db, new FakeCurrentUserService(Guid.NewGuid(), RoleNames.SuperAdmin));

        var response = await sut.RejectAsync(driver.Id, new RejectDriverRequest("Documentos inválidos"), CancellationToken.None);

        Assert.Equal("Rejected", response.ApprovalStatus);
    }

    [Fact]
    public async Task Block_ThenUnblock_TogglesStatus()
    {
        var (db, driver, _) = await SeedAsync(DriverApprovalStatus.Approved);
        var sut = new DriverService(db, new FakeCurrentUserService(Guid.NewGuid(), RoleNames.SuperAdmin));

        var blocked = await sut.SetBlockedAsync(driver.Id, true, CancellationToken.None);
        Assert.Equal("Blocked", blocked.Status);

        var unblocked = await sut.SetBlockedAsync(driver.Id, false, CancellationToken.None);
        Assert.Equal("Offline", unblocked.Status);
    }
}
