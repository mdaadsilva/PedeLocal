using PedeLocal.Application.Catalog;
using PedeLocal.Application.Common;
using PedeLocal.Application.Inventory;
using PedeLocal.Application.Orders;
using PedeLocal.Contracts.Catalog;
using PedeLocal.Contracts.Inventory;
using PedeLocal.Contracts.Orders;
using PedeLocal.Domain.Audit;
using PedeLocal.Domain.Catalog;
using PedeLocal.Domain.Establishments;
using PedeLocal.Domain.Identity;
using PedeLocal.Infrastructure.Routing;
using PedeLocal.UnitTests.TestSupport;
using Xunit;

namespace PedeLocal.UnitTests.Audit;

/// <summary>Locks in the three audit events the earlier implementation defined but never wrote
/// (found during the final audit pass — see docs/DECISIONS.md).</summary>
public class AuditTrailTests
{
    [Fact]
    public async Task AdjustStock_WritesStockAdjustedAuditLog()
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
        var product = new Product
        {
            EstablishmentId = establishment.Id, MenuCategoryId = Guid.NewGuid(), Name = "Coca-Cola 2L",
            Price = 10, StockMode = StockMode.Controlled, StockQuantity = 10, IsAvailable = true
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var user = new FakeCurrentUserService(staffUserId);
        var sut = new InventoryService(db, user, new EstablishmentAccessGuard(db, user));

        await sut.AdjustStockAsync(establishment.Id, product.Id, new AdjustStockRequest("Entry", 5, "reposição"), CancellationToken.None);

        Assert.Contains(db.AuditLogs, a => a.Action == AuditActions.StockAdjusted && a.EntityId == product.Id);
    }

    [Fact]
    public async Task UpdateProduct_PriceChange_WritesPriceChangedAuditLog()
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
        var category = new MenuCategory { EstablishmentId = establishment.Id, Name = "Lanches" };
        db.MenuCategories.Add(category);
        var product = new Product
        {
            EstablishmentId = establishment.Id, MenuCategoryId = category.Id, Name = "X-Burger",
            Price = 20, StockMode = StockMode.Uncontrolled, IsAvailable = true
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var user = new FakeCurrentUserService(staffUserId);
        var sut = new CatalogService(db, user, new EstablishmentAccessGuard(db, user));

        await sut.UpdateProductAsync(establishment.Id, product.Id,
            new UpdateProductRequest(category.Id, "X-Burger", null, null, 25m, null, []), CancellationToken.None);

        Assert.Contains(db.AuditLogs, a => a.Action == AuditActions.PriceChanged && a.EntityId == product.Id);
    }

    [Fact]
    public async Task UpdateProduct_SamePrice_DoesNotWriteAuditLog()
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
        var category = new MenuCategory { EstablishmentId = establishment.Id, Name = "Lanches" };
        db.MenuCategories.Add(category);
        var product = new Product
        {
            EstablishmentId = establishment.Id, MenuCategoryId = category.Id, Name = "X-Burger",
            Price = 20, StockMode = StockMode.Uncontrolled, IsAvailable = true
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var user = new FakeCurrentUserService(staffUserId);
        var sut = new CatalogService(db, user, new EstablishmentAccessGuard(db, user));

        await sut.UpdateProductAsync(establishment.Id, product.Id,
            new UpdateProductRequest(category.Id, "X-Burger Renomeado", null, null, 20m, null, []), CancellationToken.None);

        Assert.DoesNotContain(db.AuditLogs, a => a.Action == AuditActions.PriceChanged);
    }

    [Fact]
    public async Task CancelOrder_WritesOrderCancelledAuditLog()
    {
        var fx = await OrderTestFixture.CreateAsync();
        var customerUser = new FakeCurrentUserService(fx.CustomerUserId);
        var staffUser = new FakeCurrentUserService(fx.StaffUserId);
        var customerSut = new OrderService(fx.Db, customerUser, new EstablishmentAccessGuard(fx.Db, customerUser),
            new HaversineDistanceService(), TestTokenService.Create(), TestDeliveryCodeCipher.Create(), new NoOpRealtimeNotifier(), new NoOpNotificationService());
        var staffSut = new OrderService(fx.Db, staffUser, new EstablishmentAccessGuard(fx.Db, staffUser),
            new HaversineDistanceService(), TestTokenService.Create(), TestDeliveryCodeCipher.Create(), new NoOpRealtimeNotifier(), new NoOpNotificationService());

        var order = await customerSut.CreateAsync(
            new CreateOrderRequest(fx.Establishment.Id, fx.CustomerAddress.Id, "Pix", null,
                [new CreateOrderItemRequest(fx.Product.Id, 1, [])], null),
            CancellationToken.None);

        await staffSut.CancelAsync(fx.Establishment.Id, order.Id, new CancelOrderRequest("cliente desistiu"), CancellationToken.None);

        Assert.Contains(fx.Db.AuditLogs, a => a.Action == AuditActions.OrderCancelled && a.EntityId == order.Id);
    }
}
