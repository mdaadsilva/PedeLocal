using PedeLocal.Application.Common;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Inventory;
using PedeLocal.Contracts.Inventory;
using PedeLocal.Domain.Catalog;
using PedeLocal.Domain.Establishments;
using PedeLocal.Domain.Identity;
using PedeLocal.UnitTests.TestSupport;
using Xunit;

namespace PedeLocal.UnitTests.Inventory;

public class InventoryServiceTests
{
    private static async Task<(InventoryService Sut, Guid EstablishmentId, Guid ProductId)> SeedAsync(int initialStock = 10)
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
            Price = 10, StockMode = StockMode.Controlled, StockQuantity = initialStock, IsAvailable = true
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var user = new FakeCurrentUserService(staffUserId);
        var sut = new InventoryService(db, user, new EstablishmentAccessGuard(db, user));
        return (sut, establishment.Id, product.Id);
    }

    [Fact]
    public async Task Entry_IncreasesStock()
    {
        var (sut, establishmentId, productId) = await SeedAsync(10);

        var response = await sut.AdjustStockAsync(establishmentId, productId, new AdjustStockRequest("Entry", 5, "reposição"), CancellationToken.None);

        Assert.Equal(15, response.NewQuantity);
    }

    [Fact]
    public async Task Loss_DecreasesStock()
    {
        var (sut, establishmentId, productId) = await SeedAsync(10);

        var response = await sut.AdjustStockAsync(establishmentId, productId, new AdjustStockRequest("Loss", 3, "quebra"), CancellationToken.None);

        Assert.Equal(7, response.NewQuantity);
    }

    [Fact]
    public async Task Loss_MoreThanAvailable_Throws()
    {
        var (sut, establishmentId, productId) = await SeedAsync(2);

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.AdjustStockAsync(establishmentId, productId, new AdjustStockRequest("Loss", 5, null), CancellationToken.None));
    }

    [Fact]
    public async Task Adjustment_SetsAbsoluteQuantity()
    {
        var (sut, establishmentId, productId) = await SeedAsync(10);

        var response = await sut.AdjustStockAsync(establishmentId, productId, new AdjustStockRequest("Adjustment", 42, "contagem"), CancellationToken.None);

        Assert.Equal(42, response.NewQuantity);
    }

    [Fact]
    public async Task SaleType_IsRejected_AsManualAdjustment()
    {
        var (sut, establishmentId, productId) = await SeedAsync(10);

        await Assert.ThrowsAsync<AppException>(() =>
            sut.AdjustStockAsync(establishmentId, productId, new AdjustStockRequest("Sale", 1, null), CancellationToken.None));
    }
}
