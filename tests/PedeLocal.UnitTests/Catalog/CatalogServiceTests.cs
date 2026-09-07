using PedeLocal.Application.Catalog;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Contracts.Catalog;
using PedeLocal.Domain.Establishments;
using PedeLocal.Domain.Identity;
using PedeLocal.Domain.Inventory;
using PedeLocal.Infrastructure.Persistence;
using PedeLocal.UnitTests.TestSupport;
using Xunit;

namespace PedeLocal.UnitTests.Catalog;

public class CatalogServiceTests
{
    private static async Task<(PedeLocalDbContext Db, Guid EstablishmentId, Guid StaffUserId, Guid CategoryId)> SeedAsync()
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
        var category = new PedeLocal.Domain.Catalog.MenuCategory { EstablishmentId = establishment.Id, Name = "Lanches" };
        db.MenuCategories.Add(category);
        await db.SaveChangesAsync();

        return (db, establishment.Id, staffUserId, category.Id);
    }

    private static CreateProductRequest BuildProductRequest(Guid categoryId) => new(
        categoryId, "X-Bacon", null, null, 20m, null, "Controlled", 10,
        [new ProductOptionGroupRequest("Adicionais", false, 0, 3, [new ProductOptionRequest("Bacon extra", 4m, true)])]);

    [Fact]
    public async Task CreateProduct_AsStaff_Succeeds_AndCreatesInitialStockMovement()
    {
        var (db, establishmentId, staffUserId, categoryId) = await SeedAsync();
        var sut = new CatalogService(db, new FakeCurrentUserService(staffUserId), new PedeLocal.Application.Common.EstablishmentAccessGuard(db, new FakeCurrentUserService(staffUserId)));

        var response = await sut.CreateProductAsync(establishmentId, BuildProductRequest(categoryId), CancellationToken.None);

        Assert.Equal(10, response.StockQuantity);
        Assert.Single(response.OptionGroups);
        Assert.Single(response.OptionGroups[0].Options);

        var movement = Assert.Single(db.InventoryMovements);
        Assert.Equal(InventoryMovementType.Entry, movement.Type);
        Assert.Equal(10, movement.NewQuantity);
    }

    [Fact]
    public async Task CreateProduct_AsNonStaff_Throws()
    {
        var (db, establishmentId, _, categoryId) = await SeedAsync();
        var user = new FakeCurrentUserService(Guid.NewGuid());
        var sut = new CatalogService(db, user, new PedeLocal.Application.Common.EstablishmentAccessGuard(db, user));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            sut.CreateProductAsync(establishmentId, BuildProductRequest(categoryId), CancellationToken.None));
    }

    [Fact]
    public async Task CreateProduct_AsSuperAdmin_Succeeds_EvenWithoutStaffMembership()
    {
        var (db, establishmentId, _, categoryId) = await SeedAsync();
        var user = new FakeCurrentUserService(Guid.NewGuid(), RoleNames.SuperAdmin);
        var sut = new CatalogService(db, user, new PedeLocal.Application.Common.EstablishmentAccessGuard(db, user));

        var response = await sut.CreateProductAsync(establishmentId, BuildProductRequest(categoryId), CancellationToken.None);

        Assert.Equal("X-Bacon", response.Name);
    }

    [Fact]
    public async Task UpdateProduct_ReplacesOptionGroups()
    {
        var (db, establishmentId, staffUserId, categoryId) = await SeedAsync();
        var sut = new CatalogService(db, new FakeCurrentUserService(staffUserId), new PedeLocal.Application.Common.EstablishmentAccessGuard(db, new FakeCurrentUserService(staffUserId)));
        var created = await sut.CreateProductAsync(establishmentId, BuildProductRequest(categoryId), CancellationToken.None);

        var updateRequest = new UpdateProductRequest(categoryId, "X-Bacon Especial", null, null, 25m, null,
            [new ProductOptionGroupRequest("Tamanho", true, 1, 1, [new ProductOptionRequest("Grande", 5m, true)])]);
        var updated = await sut.UpdateProductAsync(establishmentId, created.Id, updateRequest, CancellationToken.None);

        var group = Assert.Single(updated.OptionGroups);
        Assert.Equal("Tamanho", group.Name);
    }

    [Fact]
    public async Task GetPublicProducts_HidesUnavailableProducts()
    {
        var (db, establishmentId, staffUserId, categoryId) = await SeedAsync();
        var establishment = await db.Establishments.FindAsync(establishmentId);
        db.EstablishmentSettings.Add(new EstablishmentSettings { EstablishmentId = establishmentId, IsVisible = true, AcceptsNewOrders = true });
        await db.SaveChangesAsync();

        var sut = new CatalogService(db, new FakeCurrentUserService(staffUserId), new PedeLocal.Application.Common.EstablishmentAccessGuard(db, new FakeCurrentUserService(staffUserId)));
        var created = await sut.CreateProductAsync(establishmentId, BuildProductRequest(categoryId), CancellationToken.None);
        await sut.SetProductAvailabilityAsync(establishmentId, created.Id, new UpdateProductAvailabilityRequest(false), CancellationToken.None);

        var publicProducts = await sut.GetPublicProductsAsync(establishmentId, CancellationToken.None);

        Assert.Empty(publicProducts);
    }
}
