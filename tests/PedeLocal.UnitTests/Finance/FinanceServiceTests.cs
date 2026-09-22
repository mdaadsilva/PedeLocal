using PedeLocal.Application.Common;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Finance;
using PedeLocal.Contracts.Finance;
using PedeLocal.Domain.Drivers;
using PedeLocal.Domain.Establishments;
using PedeLocal.Domain.Finance;
using PedeLocal.Domain.Identity;
using PedeLocal.Infrastructure.Persistence;
using PedeLocal.UnitTests.TestSupport;
using Xunit;

namespace PedeLocal.UnitTests.Finance;

public class FinanceServiceTests
{
    private static async Task<(PedeLocalDbContext Db, Guid EstablishmentId, Guid StaffUserId, Guid DriverId, Guid DriverUserId, DriverReceivable R1, DriverReceivable R2)> SeedAsync()
    {
        var db = InMemoryDbContextFactory.Create();
        var establishment = new Establishment
        {
            CityId = Guid.NewGuid(), Name = "Loja", Slug = "loja", Document = "1", Phone = "1", Email = "a@a.com",
            CategoryId = Guid.NewGuid(), AddressId = Guid.NewGuid()
        };
        var staffUserId = Guid.NewGuid();
        db.Establishments.Add(establishment);
        db.EstablishmentUsers.Add(new EstablishmentUser { EstablishmentId = establishment.Id, UserId = staffUserId, StaffRole = RoleNames.EstablishmentAdmin });

        var driverUserId = Guid.NewGuid();
        db.Users.Add(new User { Id = driverUserId, Name = "Entregador", Email = "e@e.com", PhoneNumber = "1", PasswordHash = "x" });
        var driver = new Domain.Drivers.Driver
        {
            UserId = driverUserId, CityId = establishment.CityId, Cpf = "12345678901", VehicleType = VehicleType.Motorcycle,
            PixKeyType = PixKeyType.Cpf, PixKey = "x", OnboardingStatus = DriverOnboardingStatus.Approved, Status = DriverStatus.Available
        };
        db.Drivers.Add(driver);

        var r1 = new DriverReceivable { DeliveryId = Guid.NewGuid(), DriverId = driver.Id, EstablishmentId = establishment.Id, Amount = 7m, Status = ReceivableStatus.Pending };
        var r2 = new DriverReceivable { DeliveryId = Guid.NewGuid(), DriverId = driver.Id, EstablishmentId = establishment.Id, Amount = 8m, Status = ReceivableStatus.Pending };
        db.DriverReceivables.AddRange(r1, r2);

        await db.SaveChangesAsync();
        return (db, establishment.Id, staffUserId, driver.Id, driverUserId, r1, r2);
    }

    private static FinanceService BuildSut(PedeLocalDbContext db, PedeLocal.Application.Common.Interfaces.ICurrentUserService user) =>
        new(db, new EstablishmentAccessGuard(db, user), new DriverAccessGuard(db, user), new NoOpNotificationService());

    [Fact]
    public async Task CreateBatch_SumsAmounts_AndLinksReceivables()
    {
        var (db, establishmentId, staffUserId, driverId, _, r1, r2) = await SeedAsync();
        var sut = BuildSut(db, new FakeCurrentUserService(staffUserId));

        var batch = await sut.CreateBatchAsync(establishmentId, new CreatePaymentBatchRequest(driverId, [r1.Id, r2.Id]), CancellationToken.None);

        Assert.Equal(15m, batch.TotalAmount);
        Assert.Equal(2, batch.ReceivableIds.Count);
    }

    [Fact]
    public async Task CreateBatch_ReceivableAlreadyBatched_Throws()
    {
        var (db, establishmentId, staffUserId, driverId, _, r1, r2) = await SeedAsync();
        var sut = BuildSut(db, new FakeCurrentUserService(staffUserId));
        await sut.CreateBatchAsync(establishmentId, new CreatePaymentBatchRequest(driverId, [r1.Id]), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.CreateBatchAsync(establishmentId, new CreatePaymentBatchRequest(driverId, [r1.Id, r2.Id]), CancellationToken.None));
    }

    [Fact]
    public async Task FullFlow_ReportThenConfirm_CascadesReceivableStatus()
    {
        var (db, establishmentId, staffUserId, driverId, driverUserId, r1, r2) = await SeedAsync();
        var merchantSut = BuildSut(db, new FakeCurrentUserService(staffUserId));
        var driverSut = BuildSut(db, new FakeCurrentUserService(driverUserId));

        var batch = await merchantSut.CreateBatchAsync(establishmentId, new CreatePaymentBatchRequest(driverId, [r1.Id, r2.Id]), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => driverSut.ConfirmReceivedAsync(batch.Id, CancellationToken.None));

        var reported = await merchantSut.ReportPaymentAsync(establishmentId, batch.Id, CancellationToken.None);
        Assert.Equal("PaymentReported", reported.Status);
        Assert.Equal(ReceivableStatus.PaymentReported, db.DriverReceivables.Single(r => r.Id == r1.Id).Status);

        var confirmed = await driverSut.ConfirmReceivedAsync(batch.Id, CancellationToken.None);
        Assert.Equal("Received", confirmed.Status);
        Assert.Equal(ReceivableStatus.Received, db.DriverReceivables.Single(r => r.Id == r2.Id).Status);
    }

    [Fact]
    public async Task ConfirmReceived_ByAnotherDriver_Throws()
    {
        var (db, establishmentId, staffUserId, driverId, _, r1, _) = await SeedAsync();
        var merchantSut = BuildSut(db, new FakeCurrentUserService(staffUserId));
        var batch = await merchantSut.CreateBatchAsync(establishmentId, new CreatePaymentBatchRequest(driverId, [r1.Id]), CancellationToken.None);
        await merchantSut.ReportPaymentAsync(establishmentId, batch.Id, CancellationToken.None);

        var otherDriverUserId = Guid.NewGuid();
        db.Users.Add(new User { Id = otherDriverUserId, Name = "Outro", Email = "outro@example.com", PhoneNumber = "1", PasswordHash = "x" });
        db.Drivers.Add(new Domain.Drivers.Driver
        {
            UserId = otherDriverUserId, CityId = Guid.NewGuid(), Cpf = "98765432100", VehicleType = VehicleType.Bike,
            PixKeyType = PixKeyType.Cpf, PixKey = "y", OnboardingStatus = DriverOnboardingStatus.Approved, Status = DriverStatus.Available
        });
        await db.SaveChangesAsync();

        var otherDriverSut = BuildSut(db, new FakeCurrentUserService(otherDriverUserId));
        await Assert.ThrowsAsync<ForbiddenException>(() => otherDriverSut.ConfirmReceivedAsync(batch.Id, CancellationToken.None));
    }
}
