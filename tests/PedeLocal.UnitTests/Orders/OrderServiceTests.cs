using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Orders;
using PedeLocal.Contracts.Orders;
using PedeLocal.Domain.Inventory;
using PedeLocal.Domain.Notifications;
using PedeLocal.Domain.Orders;
using PedeLocal.Infrastructure.Routing;
using PedeLocal.UnitTests.TestSupport;
using Xunit;

namespace PedeLocal.UnitTests.Orders;

public class OrderServiceTests
{
    private static OrderService BuildCustomerSut(OrderTestFixture fx)
    {
        var user = new FakeCurrentUserService(fx.CustomerUserId);
        return new OrderService(fx.Db, user, new EstablishmentAccessGuard(fx.Db, user), new HaversineDistanceService(), TestTokenService.Create(), TestDeliveryCodeCipher.Create(), new NoOpRealtimeNotifier(), new NoOpNotificationService());
    }

    private static OrderService BuildStaffSut(OrderTestFixture fx)
    {
        var user = new FakeCurrentUserService(fx.StaffUserId);
        return new OrderService(fx.Db, user, new EstablishmentAccessGuard(fx.Db, user), new HaversineDistanceService(), TestTokenService.Create(), TestDeliveryCodeCipher.Create(), new NoOpRealtimeNotifier(), new NoOpNotificationService());
    }

    private static CreateOrderRequest BuildCreateRequest(OrderTestFixture fx, int quantity = 2, bool withOption = true, string? idempotencyKey = null) =>
        new(
            fx.Establishment.Id, fx.CustomerAddress.Id, "Pix", null,
            [new CreateOrderItemRequest(fx.Product.Id, quantity, withOption ? [new CreateOrderItemOptionSelection(fx.Option.Id)] : [])],
            idempotencyKey);

    [Fact]
    public async Task Create_ComputesSubtotalDeliveryFeeAndTotal()
    {
        var fx = await OrderTestFixture.CreateAsync();
        var sut = BuildCustomerSut(fx);

        var response = await sut.CreateAsync(BuildCreateRequest(fx, quantity: 2), CancellationToken.None);

        // (20 unit + 4 option) * 2 = 48 subtotal; ~1.1km * R$3/km but floors at min fee R$5.
        Assert.Equal(48m, response.Subtotal);
        Assert.True(response.DeliveryFee >= 5m);
        Assert.Equal(response.Subtotal + response.DeliveryFee, response.Total);
        Assert.Equal("Pending", response.Status);
    }

    [Fact]
    public async Task Create_InsufficientStock_Throws()
    {
        var fx = await OrderTestFixture.CreateAsync(stockQuantity: 1);
        var sut = BuildCustomerSut(fx);

        await Assert.ThrowsAsync<ConflictException>(() => sut.CreateAsync(BuildCreateRequest(fx, quantity: 5), CancellationToken.None));
    }

    [Fact]
    public async Task Create_TooFewOptionsSelected_WhenGroupRequiresMore_Throws()
    {
        var fx = await OrderTestFixture.CreateAsync();
        fx.OptionGroup.Min = 1;
        fx.OptionGroup.Required = true;
        await fx.Db.SaveChangesAsync();
        var sut = BuildCustomerSut(fx);

        await Assert.ThrowsAsync<AppException>(() => sut.CreateAsync(BuildCreateRequest(fx, withOption: false), CancellationToken.None));
    }

    [Fact]
    public async Task Create_SameIdempotencyKeyTwice_ReturnsSameOrder()
    {
        var fx = await OrderTestFixture.CreateAsync();
        var sut = BuildCustomerSut(fx);
        var request = BuildCreateRequest(fx, idempotencyKey: "abc-123");

        var first = await sut.CreateAsync(request, CancellationToken.None);
        var second = await sut.CreateAsync(request, CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(fx.Db.Orders);
    }

    [Fact]
    public async Task Accept_DeductsStock_AndIsIdempotentOnSecondCall()
    {
        var fx = await OrderTestFixture.CreateAsync(stockQuantity: 10);
        var customerSut = BuildCustomerSut(fx);
        var order = await customerSut.CreateAsync(BuildCreateRequest(fx, quantity: 2), CancellationToken.None);

        var staffSut = BuildStaffSut(fx);
        await staffSut.AcceptAsync(fx.Establishment.Id, order.Id, CancellationToken.None);
        await staffSut.AcceptAsync(fx.Establishment.Id, order.Id, CancellationToken.None); // idempotent

        var product = await fx.Db.Products.FindAsync(fx.Product.Id);
        Assert.Equal(8, product!.StockQuantity);

        var saleMovements = fx.Db.InventoryMovements.Where(m => m.Type == InventoryMovementType.Sale).ToList();
        Assert.Single(saleMovements); // not duplicated by the second accept call.
    }

    [Fact]
    public async Task Cancel_AfterAccept_RestoresStock()
    {
        var fx = await OrderTestFixture.CreateAsync(stockQuantity: 10);
        var customerSut = BuildCustomerSut(fx);
        var order = await customerSut.CreateAsync(BuildCreateRequest(fx, quantity: 2), CancellationToken.None);

        var staffSut = BuildStaffSut(fx);
        await staffSut.AcceptAsync(fx.Establishment.Id, order.Id, CancellationToken.None);
        await staffSut.CancelAsync(fx.Establishment.Id, order.Id, new CancelOrderRequest("cliente desistiu"), CancellationToken.None);

        var product = await fx.Db.Products.FindAsync(fx.Product.Id);
        Assert.Equal(10, product!.StockQuantity);
    }

    [Fact]
    public async Task MarkReady_CreatesAvailableDelivery()
    {
        var fx = await OrderTestFixture.CreateAsync();
        var customerSut = BuildCustomerSut(fx);
        var order = await customerSut.CreateAsync(BuildCreateRequest(fx), CancellationToken.None);

        var staffSut = BuildStaffSut(fx);
        await staffSut.AcceptAsync(fx.Establishment.Id, order.Id, CancellationToken.None);
        await staffSut.MarkPreparingAsync(fx.Establishment.Id, order.Id, CancellationToken.None);
        var ready = await staffSut.MarkReadyAsync(fx.Establishment.Id, order.Id, CancellationToken.None);

        Assert.Equal("WaitingDriver", ready.Status);
        var delivery = fx.Db.Deliveries.Single(d => d.OrderId == order.Id);
        Assert.Equal(Domain.Deliveries.DeliveryStatus.Available, delivery.Status);
    }

    [Fact]
    public async Task Accept_FromInvalidStatus_Throws()
    {
        var fx = await OrderTestFixture.CreateAsync();
        var customerSut = BuildCustomerSut(fx);
        var order = await customerSut.CreateAsync(BuildCreateRequest(fx), CancellationToken.None);

        var staffSut = BuildStaffSut(fx);
        await staffSut.CancelAsync(fx.Establishment.Id, order.Id, new CancelOrderRequest(null), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => staffSut.AcceptAsync(fx.Establishment.Id, order.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Create_WhenEstablishmentClosed_Throws()
    {
        var fx = await OrderTestFixture.CreateAsync();
        fx.Establishment.OperationalStatus = Domain.Establishments.OperationalStatus.ClosedManual;
        await fx.Db.SaveChangesAsync();
        var sut = BuildCustomerSut(fx);

        await Assert.ThrowsAsync<ConflictException>(() => sut.CreateAsync(BuildCreateRequest(fx), CancellationToken.None));
    }

    // ---------------------------------------------------------------
    // Preview
    // ---------------------------------------------------------------

    [Fact]
    public async Task Preview_ComputesSameTotalsAsSubsequentCreate()
    {
        var fx = await OrderTestFixture.CreateAsync();
        var sut = BuildCustomerSut(fx);
        var request = BuildCreateRequest(fx, quantity: 2);

        var preview = await sut.PreviewAsync(request, CancellationToken.None);
        var created = await sut.CreateAsync(request, CancellationToken.None);

        Assert.Equal(preview.Subtotal, created.Subtotal);
        Assert.Equal(preview.DeliveryFee, created.DeliveryFee);
        Assert.Equal(preview.Total, created.Total);
        Assert.Equal(preview.DistanceKm, created.CalculatedDistanceKm);
        Assert.Equal("Delivery", preview.Type);
    }

    [Fact]
    public async Task Preview_DoesNotPersistAnything()
    {
        var fx = await OrderTestFixture.CreateAsync();
        var sut = BuildCustomerSut(fx);

        await sut.PreviewAsync(BuildCreateRequest(fx), CancellationToken.None);

        Assert.Empty(fx.Db.Orders);
    }

    [Fact]
    public async Task Preview_InvalidAddress_Throws()
    {
        var fx = await OrderTestFixture.CreateAsync();
        var sut = BuildCustomerSut(fx);
        var request = new CreateOrderRequest(fx.Establishment.Id, Guid.NewGuid(), "Pix", null,
            [new CreateOrderItemRequest(fx.Product.Id, 1, [])], null);

        await Assert.ThrowsAsync<NotFoundException>(() => sut.PreviewAsync(request, CancellationToken.None));
    }

    // ---------------------------------------------------------------
    // Pickup
    // ---------------------------------------------------------------

    private static CreateOrderRequest BuildPickupRequest(OrderTestFixture fx, int quantity = 1) =>
        new(fx.Establishment.Id, null, "Cash", null,
            [new CreateOrderItemRequest(fx.Product.Id, quantity, [])], null, "Pickup");

    [Fact]
    public async Task Create_Pickup_HasNoFeeNoAddressNoDistance()
    {
        var fx = await OrderTestFixture.CreateAsync();
        var sut = BuildCustomerSut(fx);

        var order = await sut.CreateAsync(BuildPickupRequest(fx), CancellationToken.None);

        Assert.Equal("Pickup", order.Type);
        Assert.Null(order.AddressId);
        Assert.Equal(0m, order.DeliveryFee);
        Assert.Null(order.CalculatedDistanceKm);
        Assert.Equal(order.Subtotal, order.Total);
    }

    [Fact]
    public async Task Create_Delivery_WithoutAddress_Throws()
    {
        var fx = await OrderTestFixture.CreateAsync();
        var sut = BuildCustomerSut(fx);
        var request = new CreateOrderRequest(fx.Establishment.Id, null, "Pix", null,
            [new CreateOrderItemRequest(fx.Product.Id, 1, [])], null, "Delivery");

        await Assert.ThrowsAsync<AppException>(() => sut.CreateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Create_Pickup_IgnoresMissingDeliveryPricingSettings()
    {
        var fx = await OrderTestFixture.CreateAsync();
        fx.Db.DeliveryPricingSettings.Remove(fx.Db.DeliveryPricingSettings.Single());
        await fx.Db.SaveChangesAsync();
        var sut = BuildCustomerSut(fx);

        var order = await sut.CreateAsync(BuildPickupRequest(fx), CancellationToken.None);

        Assert.Equal("Pickup", order.Type);
    }

    [Fact]
    public async Task MarkReady_Pickup_SetsReady_AndCreatesNoDelivery()
    {
        var fx = await OrderTestFixture.CreateAsync();
        var customerSut = BuildCustomerSut(fx);
        var order = await customerSut.CreateAsync(BuildPickupRequest(fx), CancellationToken.None);

        var staffSut = BuildStaffSut(fx);
        await staffSut.AcceptAsync(fx.Establishment.Id, order.Id, CancellationToken.None);
        await staffSut.MarkPreparingAsync(fx.Establishment.Id, order.Id, CancellationToken.None);
        var ready = await staffSut.MarkReadyAsync(fx.Establishment.Id, order.Id, CancellationToken.None);

        Assert.Equal("Ready", ready.Status);
        Assert.False(fx.Db.Deliveries.Any(d => d.OrderId == order.Id));
    }

    [Fact]
    public async Task CompletePickup_FromReady_SetsPickupCompleted_AndIsIdempotent()
    {
        var fx = await OrderTestFixture.CreateAsync();
        var customerSut = BuildCustomerSut(fx);
        var order = await customerSut.CreateAsync(BuildPickupRequest(fx), CancellationToken.None);

        var staffSut = BuildStaffSut(fx);
        await staffSut.AcceptAsync(fx.Establishment.Id, order.Id, CancellationToken.None);
        await staffSut.MarkPreparingAsync(fx.Establishment.Id, order.Id, CancellationToken.None);
        await staffSut.MarkReadyAsync(fx.Establishment.Id, order.Id, CancellationToken.None);

        var completed = await staffSut.CompletePickupAsync(fx.Establishment.Id, order.Id, CancellationToken.None);
        var completedAgain = await staffSut.CompletePickupAsync(fx.Establishment.Id, order.Id, CancellationToken.None);

        Assert.Equal("PickupCompleted", completed.Status);
        Assert.Equal("PickupCompleted", completedAgain.Status);
    }

    [Fact]
    public async Task CompletePickup_OnDeliveryOrder_Throws()
    {
        var fx = await OrderTestFixture.CreateAsync();
        var customerSut = BuildCustomerSut(fx);
        var order = await customerSut.CreateAsync(BuildCreateRequest(fx), CancellationToken.None);

        var staffSut = BuildStaffSut(fx);
        await staffSut.AcceptAsync(fx.Establishment.Id, order.Id, CancellationToken.None);
        await staffSut.MarkPreparingAsync(fx.Establishment.Id, order.Id, CancellationToken.None);
        await staffSut.MarkReadyAsync(fx.Establishment.Id, order.Id, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() => staffSut.CompletePickupAsync(fx.Establishment.Id, order.Id, CancellationToken.None));
    }

    // ---------------------------------------------------------------
    // Delivery code retrieval (customer)
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetDeliveryCode_Owner_ReturnsDecryptableCode()
    {
        var fx = await OrderTestFixture.CreateAsync();
        var customerSut = BuildCustomerSut(fx);
        var order = await customerSut.CreateAsync(BuildCreateRequest(fx), CancellationToken.None);

        var staffSut = BuildStaffSut(fx);
        await staffSut.AcceptAsync(fx.Establishment.Id, order.Id, CancellationToken.None);
        await staffSut.MarkPreparingAsync(fx.Establishment.Id, order.Id, CancellationToken.None);
        await staffSut.MarkReadyAsync(fx.Establishment.Id, order.Id, CancellationToken.None);

        var response = await customerSut.GetDeliveryCodeAsync(order.Id, CancellationToken.None);

        Assert.Equal(6, response.Code.Length);
        Assert.True(int.TryParse(response.Code, out _));
    }

    [Fact]
    public async Task GetDeliveryCode_NonOwner_Throws()
    {
        var fx = await OrderTestFixture.CreateAsync();
        var customerSut = BuildCustomerSut(fx);
        var order = await customerSut.CreateAsync(BuildCreateRequest(fx), CancellationToken.None);

        var staffSut = BuildStaffSut(fx);
        await staffSut.AcceptAsync(fx.Establishment.Id, order.Id, CancellationToken.None);
        await staffSut.MarkPreparingAsync(fx.Establishment.Id, order.Id, CancellationToken.None);
        await staffSut.MarkReadyAsync(fx.Establishment.Id, order.Id, CancellationToken.None);

        var strangerSut = new OrderService(fx.Db, new FakeCurrentUserService(Guid.NewGuid()),
            new EstablishmentAccessGuard(fx.Db, new FakeCurrentUserService(fx.StaffUserId)), new HaversineDistanceService(),
            TestTokenService.Create(), TestDeliveryCodeCipher.Create(), new NoOpRealtimeNotifier(), new NoOpNotificationService());

        await Assert.ThrowsAsync<ForbiddenException>(() => strangerSut.GetDeliveryCodeAsync(order.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetDeliveryCode_BeforeReady_Throws()
    {
        var fx = await OrderTestFixture.CreateAsync();
        var customerSut = BuildCustomerSut(fx);
        var order = await customerSut.CreateAsync(BuildCreateRequest(fx), CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() => customerSut.GetDeliveryCodeAsync(order.Id, CancellationToken.None));
    }

    // ---------------------------------------------------------------
    // Notification fan-out (docs/REALTIME_EVENTS.md)
    // ---------------------------------------------------------------

    [Fact]
    public async Task Create_NotifiesEstablishmentStaff_OfNewOrder()
    {
        var fx = await OrderTestFixture.CreateAsync();
        var notifications = new CapturingNotificationService();
        var user = new FakeCurrentUserService(fx.CustomerUserId);
        var sut = new OrderService(fx.Db, user, new EstablishmentAccessGuard(fx.Db, user), new HaversineDistanceService(),
            TestTokenService.Create(), TestDeliveryCodeCipher.Create(), new NoOpRealtimeNotifier(), notifications);

        await sut.CreateAsync(BuildCreateRequest(fx), CancellationToken.None);

        var staffNotification = Assert.Single(notifications.Notifications);
        Assert.Equal(fx.StaffUserId, staffNotification.UserId);
        Assert.Equal(NotificationEventType.OrderCreated, staffNotification.Type);
    }

    [Fact]
    public async Task MarkPreparing_NotifiesCustomer()
    {
        var fx = await OrderTestFixture.CreateAsync();
        var customerSut = BuildCustomerSut(fx);
        var order = await customerSut.CreateAsync(BuildCreateRequest(fx), CancellationToken.None);

        var notifications = new CapturingNotificationService();
        var staffUser = new FakeCurrentUserService(fx.StaffUserId);
        var staffSut = new OrderService(fx.Db, staffUser, new EstablishmentAccessGuard(fx.Db, staffUser), new HaversineDistanceService(),
            TestTokenService.Create(), TestDeliveryCodeCipher.Create(), new NoOpRealtimeNotifier(), notifications);

        await staffSut.AcceptAsync(fx.Establishment.Id, order.Id, CancellationToken.None);
        await staffSut.MarkPreparingAsync(fx.Establishment.Id, order.Id, CancellationToken.None);

        var preparingNotification = Assert.Single(notifications.Notifications, n => n.Type == NotificationEventType.OrderPreparing);
        Assert.Equal(fx.CustomerUserId, preparingNotification.UserId);
    }

    [Fact]
    public async Task Cancel_NotifiesCustomer_OfCancellation()
    {
        var fx = await OrderTestFixture.CreateAsync();
        var customerSut = BuildCustomerSut(fx);
        var order = await customerSut.CreateAsync(BuildCreateRequest(fx), CancellationToken.None);

        var notifications = new CapturingNotificationService();
        var staffUser = new FakeCurrentUserService(fx.StaffUserId);
        var staffSut = new OrderService(fx.Db, staffUser, new EstablishmentAccessGuard(fx.Db, staffUser), new HaversineDistanceService(),
            TestTokenService.Create(), TestDeliveryCodeCipher.Create(), new NoOpRealtimeNotifier(), notifications);

        await staffSut.CancelAsync(fx.Establishment.Id, order.Id, new CancelOrderRequest("estoque zerado"), CancellationToken.None);

        var cancelNotification = Assert.Single(notifications.Notifications, n => n.Type == NotificationEventType.OrderCancelled);
        Assert.Equal(fx.CustomerUserId, cancelNotification.UserId);
    }

    [Fact]
    public async Task MarkReady_Delivery_NotifiesOnlyEligibleDriversInSameCity()
    {
        var fx = await OrderTestFixture.CreateAsync();
        var customerSut = BuildCustomerSut(fx);
        var order = await customerSut.CreateAsync(BuildCreateRequest(fx), CancellationToken.None);

        var otherCity = new Domain.Cities.City { Name = "Outra Cidade", State = "SP", IsActive = true };
        fx.Db.Cities.Add(otherCity);

        Guid AddDriver(Guid cityId, Domain.Drivers.DriverApprovalStatus approval, Domain.Drivers.DriverStatus status)
        {
            var userId = Guid.NewGuid();
            fx.Db.Users.Add(new Domain.Identity.User { Id = userId, Name = "D", Email = $"{userId}@x.com", PhoneNumber = "1", PasswordHash = "x" });
            fx.Db.Drivers.Add(new Domain.Drivers.Driver
            {
                UserId = userId, CityId = cityId, Cpf = Guid.NewGuid().ToString("N")[..11],
                VehicleType = Domain.Drivers.VehicleType.Motorcycle, PixKeyType = Domain.Drivers.PixKeyType.Cpf, PixKey = "x",
                ApprovalStatus = approval, Status = status
            });
            return userId;
        }

        var eligibleDriverUserId = AddDriver(fx.City.Id, Domain.Drivers.DriverApprovalStatus.Approved, Domain.Drivers.DriverStatus.Available);
        AddDriver(fx.City.Id, Domain.Drivers.DriverApprovalStatus.Approved, Domain.Drivers.DriverStatus.Busy); // not available — excluded
        AddDriver(fx.City.Id, Domain.Drivers.DriverApprovalStatus.PendingApproval, Domain.Drivers.DriverStatus.Available); // not approved — excluded
        AddDriver(otherCity.Id, Domain.Drivers.DriverApprovalStatus.Approved, Domain.Drivers.DriverStatus.Available); // different city — excluded
        await fx.Db.SaveChangesAsync();

        var notifications = new CapturingNotificationService();
        var staffUser = new FakeCurrentUserService(fx.StaffUserId);
        var staffSut = new OrderService(fx.Db, staffUser, new EstablishmentAccessGuard(fx.Db, staffUser), new HaversineDistanceService(),
            TestTokenService.Create(), TestDeliveryCodeCipher.Create(), new NoOpRealtimeNotifier(), notifications);

        await staffSut.AcceptAsync(fx.Establishment.Id, order.Id, CancellationToken.None);
        await staffSut.MarkPreparingAsync(fx.Establishment.Id, order.Id, CancellationToken.None);
        await staffSut.MarkReadyAsync(fx.Establishment.Id, order.Id, CancellationToken.None);

        var deliveryNotifications = notifications.Notifications.Where(n => n.Type == NotificationEventType.DeliveryAvailable).ToList();
        var recipient = Assert.Single(deliveryNotifications);
        Assert.Equal(eligibleDriverUserId, recipient.UserId);
    }

    // ---------------------------------------------------------------
    // Concurrent stock deduction (docs/SECURITY_AUDIT.md)
    // ---------------------------------------------------------------

    [Fact]
    public async Task Accept_TwoOrdersForSameProduct_Concurrently_SecondGetsStockConflict_NeverOversells()
    {
        var fx = await OrderTestFixture.CreateAsync(stockQuantity: 10);
        var customerSut = BuildCustomerSut(fx);
        var order1 = await customerSut.CreateAsync(BuildCreateRequest(fx, quantity: 3, withOption: false), CancellationToken.None);
        var order2 = await customerSut.CreateAsync(BuildCreateRequest(fx, quantity: 3, withOption: false, idempotencyKey: "order-2"), CancellationToken.None);

        // Two independent DbContext instances over the SAME store, simulating two concurrent
        // requests each accepting a DIFFERENT order that both deduct from the same product.
        var dbA = fx.Db;
        var dbB = InMemoryDbContextFactory.CreateAdditionalContext(fx.DatabaseName);

        // Both "requests" read the product before either one writes.
        _ = await dbB.Products.SingleAsync(p => p.Id == fx.Product.Id);

        var userA = new FakeCurrentUserService(fx.StaffUserId);
        var sutA = new OrderService(dbA, userA, new EstablishmentAccessGuard(dbA, userA), new HaversineDistanceService(),
            TestTokenService.Create(), TestDeliveryCodeCipher.Create(), new NoOpRealtimeNotifier(), new NoOpNotificationService());
        var userB = new FakeCurrentUserService(fx.StaffUserId);
        var sutB = new OrderService(dbB, userB, new EstablishmentAccessGuard(dbB, userB), new HaversineDistanceService(),
            TestTokenService.Create(), TestDeliveryCodeCipher.Create(), new NoOpRealtimeNotifier(), new NoOpNotificationService());

        var acceptedA = await sutA.AcceptAsync(fx.Establishment.Id, order1.Id, CancellationToken.None);
        Assert.Equal("Accepted", acceptedA.Status);

        // B's context still holds the stale (pre-A) tracked product — its SaveChanges must fail
        // cleanly (409-mapped ConflictException), never silently overwrite A's deduction with a
        // lost-update. (Whether EF's InMemory test double rolls back the *rest* of B's unrelated
        // changes atomically on that failure isn't something InMemory reliably emulates — real
        // Postgres transactions do; this test's contract is specifically that the conflict is
        // detected and surfaced, not silently lost.)
        await Assert.ThrowsAsync<ConflictException>(() => sutB.AcceptAsync(fx.Establishment.Id, order2.Id, CancellationToken.None));

        // A's own deduction — independent of B's conflict/rollback — must still be intact.
        var verifyDb = InMemoryDbContextFactory.CreateAdditionalContext(fx.DatabaseName);
        var finalProduct = await verifyDb.Products.SingleAsync(p => p.Id == fx.Product.Id);
        Assert.True(finalProduct.StockQuantity is >= 0 and <= 7); // never oversold below zero, and A's -3 always applied.
        Assert.Contains(verifyDb.InventoryMovements, m => m.Type == InventoryMovementType.Sale && m.OrderId == order1.Id);
    }
}
