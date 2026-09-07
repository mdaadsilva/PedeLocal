using PedeLocal.Application.Common;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Orders;
using PedeLocal.Contracts.Orders;
using PedeLocal.Domain.Inventory;
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
        return new OrderService(fx.Db, user, new EstablishmentAccessGuard(fx.Db, user), new HaversineDistanceService(), TestTokenService.Create(), new NoOpRealtimeNotifier(), new NoOpNotificationService());
    }

    private static OrderService BuildStaffSut(OrderTestFixture fx)
    {
        var user = new FakeCurrentUserService(fx.StaffUserId);
        return new OrderService(fx.Db, user, new EstablishmentAccessGuard(fx.Db, user), new HaversineDistanceService(), TestTokenService.Create(), new NoOpRealtimeNotifier(), new NoOpNotificationService());
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
}
