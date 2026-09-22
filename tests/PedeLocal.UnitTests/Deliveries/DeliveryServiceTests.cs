using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Application.Deliveries;
using PedeLocal.Application.Identity;
using PedeLocal.Application.Orders;
using PedeLocal.Contracts.Deliveries;
using PedeLocal.Contracts.Orders;
using PedeLocal.Domain.Deliveries;
using PedeLocal.Domain.Drivers;
using PedeLocal.Domain.Identity;
using PedeLocal.Domain.Notifications;
using PedeLocal.Infrastructure.Persistence;
using PedeLocal.Infrastructure.Routing;
using PedeLocal.UnitTests.TestSupport;
using Xunit;

namespace PedeLocal.UnitTests.Deliveries;

public class DeliveryServiceTests
{
    private static DeliveryService CreateSut(
        PedeLocalDbContext db, Guid userId, ITokenService? tokenService = null,
        IRealtimeNotifier? realtime = null, INotificationService? notifications = null)
    {
        var currentUser = new FakeCurrentUserService(userId);
        return new DeliveryService(
            db, currentUser, tokenService ?? TestTokenService.Create(), realtime ?? new NoOpRealtimeNotifier(),
            notifications ?? new NoOpNotificationService(), new EstablishmentAccessGuard(db, currentUser));
    }

    private static async Task<(OrderTestFixture Fx, Guid DeliveryId, Guid OrderId)> SeedReadyDeliveryAsync()
    {
        var fx = await OrderTestFixture.CreateAsync();
        var customerUser = new FakeCurrentUserService(fx.CustomerUserId);
        var staffUser = new FakeCurrentUserService(fx.StaffUserId);
        var orderService = new OrderService(fx.Db, customerUser, new EstablishmentAccessGuard(fx.Db, customerUser),
            new HaversineDistanceService(), TestTokenService.Create(), TestDeliveryCodeCipher.Create(), new NoOpRealtimeNotifier(), new NoOpNotificationService());
        var staffOrderService = new OrderService(fx.Db, staffUser, new EstablishmentAccessGuard(fx.Db, staffUser),
            new HaversineDistanceService(), TestTokenService.Create(), TestDeliveryCodeCipher.Create(), new NoOpRealtimeNotifier(), new NoOpNotificationService());

        var order = await orderService.CreateAsync(
            new CreateOrderRequest(fx.Establishment.Id, fx.CustomerAddress.Id, "Pix", null,
                [new CreateOrderItemRequest(fx.Product.Id, 1, [])], null),
            CancellationToken.None);

        await staffOrderService.AcceptAsync(fx.Establishment.Id, order.Id, CancellationToken.None);
        await staffOrderService.MarkPreparingAsync(fx.Establishment.Id, order.Id, CancellationToken.None);
        await staffOrderService.MarkReadyAsync(fx.Establishment.Id, order.Id, CancellationToken.None);

        var delivery = fx.Db.Deliveries.Single(d => d.OrderId == order.Id);
        return (fx, delivery.Id, order.Id);
    }

    private static async Task<Guid> SeedDriverAsync(
        PedeLocalDbContext db, Guid cityId, DriverStatus status = DriverStatus.Available,
        DriverOnboardingStatus onboardingStatus = DriverOnboardingStatus.Approved, bool withValidLicense = true)
    {
        var userId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        db.Users.Add(new Domain.Identity.User { Id = userId, Name = "Entregador", Email = $"{userId}@example.com", PhoneNumber = "1", PasswordHash = "x" });
        db.Drivers.Add(new Domain.Drivers.Driver
        {
            Id = driverId, UserId = userId, CityId = cityId, Cpf = Guid.NewGuid().ToString("N")[..11], VehicleType = VehicleType.Motorcycle,
            PixKeyType = PixKeyType.Cpf, PixKey = "x", OnboardingStatus = onboardingStatus, Status = status
        });
        if (withValidLicense)
        {
            db.DriverLicenses.Add(new DriverLicense
            {
                DriverId = driverId, RegistrationNumber = Guid.NewGuid().ToString("N")[..11], Category = "A",
                ExpiresAt = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1))
            });
        }
        await db.SaveChangesAsync();
        return userId;
    }

    [Fact]
    public async Task Accept_ByApprovedAvailableDriver_Succeeds_AndSetsDriverBusy()
    {
        var (fx, deliveryId, _) = await SeedReadyDeliveryAsync();
        var driverUserId = await SeedDriverAsync(fx.Db, fx.City.Id);
        var sut = CreateSut(fx.Db, driverUserId);

        var response = await sut.AcceptAsync(deliveryId, CancellationToken.None);

        Assert.Equal("Assigned", response.Status);
        var driver = fx.Db.Drivers.Single(d => d.UserId == driverUserId);
        Assert.Equal(DriverStatus.Busy, driver.Status);
    }

    [Fact]
    public async Task Accept_ByDriverNotApproved_Throws()
    {
        var (fx, deliveryId, _) = await SeedReadyDeliveryAsync();
        var driverUserId = await SeedDriverAsync(fx.Db, fx.City.Id, onboardingStatus: DriverOnboardingStatus.UnderReview);
        var sut = CreateSut(fx.Db, driverUserId);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => sut.AcceptAsync(deliveryId, CancellationToken.None));
        Assert.Equal("DRIVER_NOT_APPROVED", ex.Code);
    }

    [Fact]
    public async Task Accept_ByDriverWithExpiredLicense_Throws()
    {
        var (fx, deliveryId, _) = await SeedReadyDeliveryAsync();
        var driverUserId = await SeedDriverAsync(fx.Db, fx.City.Id, withValidLicense: false);
        var driverId = fx.Db.Drivers.Single(d => d.UserId == driverUserId).Id;
        fx.Db.DriverLicenses.Add(new DriverLicense
        {
            DriverId = driverId, RegistrationNumber = "1", Category = "A",
            ExpiresAt = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))
        });
        await fx.Db.SaveChangesAsync();
        var sut = CreateSut(fx.Db, driverUserId);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => sut.AcceptAsync(deliveryId, CancellationToken.None));
        Assert.Equal("DRIVER_DOCUMENT_EXPIRED", ex.Code);
    }

    [Fact]
    public async Task Accept_ConcurrentDrivers_OnlyOneSucceeds_OtherGetsConflict()
    {
        var (fx, deliveryId, _) = await SeedReadyDeliveryAsync();
        var driverAUserId = await SeedDriverAsync(fx.Db, fx.City.Id);
        var driverBUserId = await SeedDriverAsync(fx.Db, fx.City.Id);

        // Two independent DbContext instances over the SAME store, simulating two concurrent
        // HTTP requests each with their own per-request DbContext.
        var dbA = fx.Db;
        var dbB = InMemoryDbContextFactory.CreateAdditionalContext(fx.DatabaseName);

        // Both "requests" read the delivery before either one writes.
        var deliveryTrackedByB = await dbB.Deliveries.SingleAsync(d => d.Id == deliveryId);

        var sutA = CreateSut(dbA, driverAUserId);
        var sutB = CreateSut(dbB, driverBUserId);

        var acceptedByA = await sutA.AcceptAsync(deliveryId, CancellationToken.None);
        Assert.Equal("Assigned", acceptedByA.Status);

        // B's context still holds the stale (pre-A) tracked delivery — this is what a second,
        // near-simultaneous request would also observe. Its SaveChanges must fail, never succeed.
        await Assert.ThrowsAsync<ConflictException>(() => sutB.AcceptAsync(deliveryId, CancellationToken.None));

        // Check truth from a fresh context — dbB's own change tracker still holds the driver
        // mutation it attempted locally even though SaveChanges never committed it, same as a
        // disposed-after-failure request-scoped DbContext would never see it persisted.
        var verifyDb = InMemoryDbContextFactory.CreateAdditionalContext(fx.DatabaseName);
        var finalDelivery = await verifyDb.Deliveries.SingleAsync(d => d.Id == deliveryId);
        Assert.Equal(driverAUserId, verifyDb.Drivers.Single(d => d.Id == finalDelivery.DriverId).UserId);

        var persistedDriverB = verifyDb.Drivers.Single(d => d.UserId == driverBUserId);
        Assert.Equal(DriverStatus.Available, persistedDriverB.Status); // never actually persisted as busy.
    }

    [Fact]
    public async Task Accept_ByDriverAlreadyOnAnotherDelivery_Throws()
    {
        var (fx, deliveryId, _) = await SeedReadyDeliveryAsync();
        var driverUserId = await SeedDriverAsync(fx.Db, fx.City.Id, DriverStatus.Busy);
        var sut = CreateSut(fx.Db, driverUserId);

        await Assert.ThrowsAsync<ConflictException>(() => sut.AcceptAsync(deliveryId, CancellationToken.None));
    }

    [Fact]
    public async Task Confirm_WrongCode_IncrementsAttempts_AndDoesNotComplete()
    {
        var (fx, deliveryId, _) = await SeedReadyDeliveryAsync();
        var driverUserId = await SeedDriverAsync(fx.Db, fx.City.Id);
        var sut = CreateSut(fx.Db, driverUserId);

        await sut.AcceptAsync(deliveryId, CancellationToken.None);
        await sut.PickupAsync(deliveryId, CancellationToken.None);
        await sut.OutForDeliveryAsync(deliveryId, CancellationToken.None);

        await Assert.ThrowsAsync<AppException>(() => sut.ConfirmAsync(deliveryId, new ConfirmDeliveryRequest("000000"), CancellationToken.None));

        var reloaded = fx.Db.DeliveryConfirmations.Single(c => c.DeliveryId == deliveryId);
        Assert.Equal(1, reloaded.Attempts);
        Assert.Equal(DeliveryStatus.OutForDelivery, fx.Db.Deliveries.Single(d => d.Id == deliveryId).Status);
    }

    [Fact]
    public async Task Confirm_CorrectCode_CompletesDelivery_MarksOrderDelivered_DriverAvailable_AndCreatesReceivableOnce()
    {
        var (fx, deliveryId, orderId) = await SeedReadyDeliveryAsync();
        var driverUserId = await SeedDriverAsync(fx.Db, fx.City.Id);
        var tokenService = TestTokenService.Create();
        var sut = CreateSut(fx.Db, driverUserId, tokenService);

        await sut.AcceptAsync(deliveryId, CancellationToken.None);
        await sut.PickupAsync(deliveryId, CancellationToken.None);
        await sut.OutForDeliveryAsync(deliveryId, CancellationToken.None);

        // Overwrite the hash with a known code — the plaintext code MarkReady generated is never
        // persisted, by design, so tests exercise the same hash-compare path with a known value.
        const string knownCode = "654321";
        var confirmation = fx.Db.DeliveryConfirmations.Single(c => c.DeliveryId == deliveryId);
        confirmation.CodeHash = tokenService.HashToken(knownCode);
        await fx.Db.SaveChangesAsync();

        var result = await sut.ConfirmAsync(deliveryId, new ConfirmDeliveryRequest(knownCode), CancellationToken.None);
        var resultAgain = await sut.ConfirmAsync(deliveryId, new ConfirmDeliveryRequest(knownCode), CancellationToken.None); // idempotent

        Assert.Equal("Completed", result.Status);
        Assert.Equal("Completed", resultAgain.Status);

        var order = fx.Db.Orders.Single(o => o.Id == orderId);
        Assert.Equal(Domain.Orders.OrderStatus.Delivered, order.Status);

        var driver = fx.Db.Drivers.Single(d => d.UserId == driverUserId);
        Assert.Equal(DriverStatus.Available, driver.Status);

        Assert.Single(fx.Db.DriverReceivables.Where(r => r.DeliveryId == deliveryId));
    }

    [Fact]
    public async Task Confirm_OnlyResponsibleDriver_CanConfirm()
    {
        var (fx, deliveryId, _) = await SeedReadyDeliveryAsync();
        var driverAUserId = await SeedDriverAsync(fx.Db, fx.City.Id);
        var driverBUserId = await SeedDriverAsync(fx.Db, fx.City.Id);

        var sutA = CreateSut(fx.Db, driverAUserId);
        await sutA.AcceptAsync(deliveryId, CancellationToken.None);

        var sutB = CreateSut(fx.Db, driverBUserId);
        await Assert.ThrowsAsync<ForbiddenException>(() => sutB.PickupAsync(deliveryId, CancellationToken.None));
    }

    [Fact]
    public async Task GetDetails_ByAssignedDriver_ReturnsEstablishmentItemsAndDeliveryAddress()
    {
        var (fx, deliveryId, orderId) = await SeedReadyDeliveryAsync();
        var driverUserId = await SeedDriverAsync(fx.Db, fx.City.Id);
        var sut = CreateSut(fx.Db, driverUserId);
        await sut.AcceptAsync(deliveryId, CancellationToken.None);

        var details = await sut.GetDetailsAsync(deliveryId, CancellationToken.None);

        Assert.Equal(orderId, details.Order.Id);
        Assert.Equal(fx.Establishment.Name, details.Establishment.Name);
        Assert.Single(details.Order.Items);
        Assert.Equal(fx.CustomerAddress.Street, details.DeliveryAddress.Street);
    }

    [Fact]
    public async Task GetDetails_ByUnrelatedDriver_Throws()
    {
        var (fx, deliveryId, _) = await SeedReadyDeliveryAsync();
        var driverAUserId = await SeedDriverAsync(fx.Db, fx.City.Id);
        var driverBUserId = await SeedDriverAsync(fx.Db, fx.City.Id);

        var sutA = CreateSut(fx.Db, driverAUserId);
        await sutA.AcceptAsync(deliveryId, CancellationToken.None);

        var sutB = CreateSut(fx.Db, driverBUserId);
        await Assert.ThrowsAsync<ForbiddenException>(() => sutB.GetDetailsAsync(deliveryId, CancellationToken.None));
    }

    [Fact]
    public async Task Confirm_MarksDeliveryCodeAsUsed_ForCustomerRetrieval()
    {
        var (fx, deliveryId, orderId) = await SeedReadyDeliveryAsync();
        var driverUserId = await SeedDriverAsync(fx.Db, fx.City.Id);
        var tokenService = TestTokenService.Create();
        var sut = CreateSut(fx.Db, driverUserId, tokenService);

        await sut.AcceptAsync(deliveryId, CancellationToken.None);
        await sut.PickupAsync(deliveryId, CancellationToken.None);
        await sut.OutForDeliveryAsync(deliveryId, CancellationToken.None);

        const string knownCode = "654321";
        var confirmation = fx.Db.DeliveryConfirmations.Single(c => c.DeliveryId == deliveryId);
        confirmation.CodeHash = tokenService.HashToken(knownCode);
        await fx.Db.SaveChangesAsync();

        await sut.ConfirmAsync(deliveryId, new ConfirmDeliveryRequest(knownCode), CancellationToken.None);

        var customerUser = new FakeCurrentUserService(fx.CustomerUserId);
        var orderService = new OrderService(
            fx.Db, customerUser, new EstablishmentAccessGuard(fx.Db, customerUser),
            new HaversineDistanceService(), tokenService, TestDeliveryCodeCipher.Create(), new NoOpRealtimeNotifier(), new NoOpNotificationService());

        await Assert.ThrowsAsync<ConflictException>(() => orderService.GetDeliveryCodeAsync(orderId, CancellationToken.None));
    }

    // ---------------------------------------------------------------
    // Delivery occurrences (section 10)
    // ---------------------------------------------------------------

    [Fact]
    public async Task AddOccurrence_ByAssignedDriver_Succeeds()
    {
        var (fx, deliveryId, _) = await SeedReadyDeliveryAsync();
        var driverUserId = await SeedDriverAsync(fx.Db, fx.City.Id);
        var sut = CreateSut(fx.Db, driverUserId);
        await sut.AcceptAsync(deliveryId, CancellationToken.None);

        var occurrence = await sut.AddOccurrenceAsync(deliveryId,
            new CreateDeliveryOccurrenceRequest("CustomerNotFound", "Ninguém atendeu."), CancellationToken.None);

        Assert.Equal("CustomerNotFound", occurrence.Type);
        Assert.Equal(deliveryId, occurrence.DeliveryId);
    }

    [Fact]
    public async Task AddOccurrence_ByUnrelatedDriver_Throws()
    {
        var (fx, deliveryId, _) = await SeedReadyDeliveryAsync();
        var driverAUserId = await SeedDriverAsync(fx.Db, fx.City.Id);
        var driverBUserId = await SeedDriverAsync(fx.Db, fx.City.Id);
        var sutA = CreateSut(fx.Db, driverAUserId);
        await sutA.AcceptAsync(deliveryId, CancellationToken.None);

        var sutB = CreateSut(fx.Db, driverBUserId);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sutB.AddOccurrenceAsync(deliveryId, new CreateDeliveryOccurrenceRequest("Other", null), CancellationToken.None));
    }

    [Fact]
    public async Task GetOccurrences_ByEstablishmentStaff_Succeeds()
    {
        var (fx, deliveryId, _) = await SeedReadyDeliveryAsync();
        var driverUserId = await SeedDriverAsync(fx.Db, fx.City.Id);
        var driverSut = CreateSut(fx.Db, driverUserId);
        await driverSut.AcceptAsync(deliveryId, CancellationToken.None);
        await driverSut.AddOccurrenceAsync(deliveryId, new CreateDeliveryOccurrenceRequest("WrongAddress", null), CancellationToken.None);

        var staffSut = CreateSut(fx.Db, fx.StaffUserId);
        var occurrences = await staffSut.GetOccurrencesAsync(deliveryId, CancellationToken.None);

        Assert.Single(occurrences);
    }

    [Fact]
    public async Task GetOccurrences_ByUnrelatedUser_Throws()
    {
        var (fx, deliveryId, _) = await SeedReadyDeliveryAsync();
        var driverUserId = await SeedDriverAsync(fx.Db, fx.City.Id);
        var driverSut = CreateSut(fx.Db, driverUserId);
        await driverSut.AcceptAsync(deliveryId, CancellationToken.None);

        var strangerSut = CreateSut(fx.Db, Guid.NewGuid());
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => strangerSut.GetOccurrencesAsync(deliveryId, CancellationToken.None));
        Assert.Equal("OCCURRENCE_ACCESS_DENIED", ex.Code);
    }

    // ---------------------------------------------------------------
    // Realtime / notification fan-out (docs/REALTIME_EVENTS.md)
    // ---------------------------------------------------------------

    [Fact]
    public async Task Accept_NotifiesEstablishmentAndCityDrivers_ForFirstAcceptWinsInvalidation()
    {
        var (fx, deliveryId, _) = await SeedReadyDeliveryAsync();
        var driverUserId = await SeedDriverAsync(fx.Db, fx.City.Id);
        var realtime = new CapturingRealtimeNotifier();
        var sut = CreateSut(fx.Db, driverUserId, realtime: realtime);

        await sut.AcceptAsync(deliveryId, CancellationToken.None);

        var call = Assert.Single(realtime.DeliveryAcceptedCalls);
        Assert.Equal(fx.City.Id, call.CityId);
        Assert.Equal(fx.Establishment.Id, call.EstablishmentId);
        Assert.Equal(deliveryId, call.DeliveryId);
    }

    [Fact]
    public async Task OutForDelivery_NotifiesCustomer()
    {
        var (fx, deliveryId, _) = await SeedReadyDeliveryAsync();
        var driverUserId = await SeedDriverAsync(fx.Db, fx.City.Id);
        var notifications = new CapturingNotificationService();
        var sut = CreateSut(fx.Db, driverUserId, notifications: notifications);

        await sut.AcceptAsync(deliveryId, CancellationToken.None);
        await sut.PickupAsync(deliveryId, CancellationToken.None);
        await sut.OutForDeliveryAsync(deliveryId, CancellationToken.None);

        var notification = Assert.Single(notifications.Notifications, n => n.Type == NotificationEventType.OrderOutForDelivery);
        Assert.Equal(fx.CustomerUserId, notification.UserId);
    }
}
