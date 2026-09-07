using PedeLocal.Application.Common;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Orders;
using PedeLocal.Contracts.Orders;
using PedeLocal.Infrastructure.Routing;
using PedeLocal.UnitTests.TestSupport;
using Xunit;

namespace PedeLocal.UnitTests.Security;

/// <summary>Direct coverage of docs section 77's QA-de-segurança checklist.</summary>
public class OwnershipSecurityTests
{
    private static OrderService BuildSut(OrderTestFixture fx, Guid userId) =>
        new(fx.Db, new FakeCurrentUserService(userId), new EstablishmentAccessGuard(fx.Db, new FakeCurrentUserService(userId)),
            new HaversineDistanceService(), TestTokenService.Create(), TestDeliveryCodeCipher.Create(), new NoOpRealtimeNotifier(), new NoOpNotificationService());

    [Fact]
    public async Task CustomerCannotAccessAnotherCustomersOrder()
    {
        var fx = await OrderTestFixture.CreateAsync();
        var ownerSut = BuildSut(fx, fx.CustomerUserId);
        var order = await ownerSut.CreateAsync(
            new CreateOrderRequest(fx.Establishment.Id, fx.CustomerAddress.Id, "Pix", null,
                [new CreateOrderItemRequest(fx.Product.Id, 1, [])], null),
            CancellationToken.None);

        var strangerUserId = Guid.NewGuid();
        var strangerSut = BuildSut(fx, strangerUserId);

        await Assert.ThrowsAsync<ForbiddenException>(() => strangerSut.GetForCustomerAsync(order.Id, CancellationToken.None));
    }

    [Fact]
    public async Task MerchantCannotAccessAnotherMerchantsOrder()
    {
        var fxA = await OrderTestFixture.CreateAsync();
        var customerSutA = BuildSut(fxA, fxA.CustomerUserId);
        var orderA = await customerSutA.CreateAsync(
            new CreateOrderRequest(fxA.Establishment.Id, fxA.CustomerAddress.Id, "Pix", null,
                [new CreateOrderItemRequest(fxA.Product.Id, 1, [])], null),
            CancellationToken.None);

        // Different establishment (different fixture = different DB), same staff-lookup code path.
        var fxB = await OrderTestFixture.CreateAsync();
        var staffSutB = new OrderService(fxB.Db, new FakeCurrentUserService(fxB.StaffUserId),
            new EstablishmentAccessGuard(fxB.Db, new FakeCurrentUserService(fxB.StaffUserId)),
            new HaversineDistanceService(), TestTokenService.Create(), TestDeliveryCodeCipher.Create(), new NoOpRealtimeNotifier(), new NoOpNotificationService());

        // Merchant B has no staff membership on establishment A at all -> forbidden before the
        // order is even looked up.
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            staffSutB.GetForMerchantAsync(fxA.Establishment.Id, orderA.Id, CancellationToken.None));
    }

    [Fact]
    public async Task SuspendedEstablishment_CannotReceiveNewOrders()
    {
        var fx = await OrderTestFixture.CreateAsync();
        fx.Establishment.CommercialStatus = Domain.Establishments.CommercialStatus.SuspendedPayment;
        await fx.Db.SaveChangesAsync();

        var sut = BuildSut(fx, fx.CustomerUserId);

        await Assert.ThrowsAsync<ConflictException>(() => sut.CreateAsync(
            new CreateOrderRequest(fx.Establishment.Id, fx.CustomerAddress.Id, "Pix", null,
                [new CreateOrderItemRequest(fx.Product.Id, 1, [])], null),
            CancellationToken.None));
    }
}
