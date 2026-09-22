using PedeLocal.Domain.Addresses;
using PedeLocal.Domain.Catalog;
using PedeLocal.Domain.Cities;
using PedeLocal.Domain.Establishments;
using PedeLocal.Domain.Identity;
using PedeLocal.Domain.Pricing;
using PedeLocal.Infrastructure.Persistence;

namespace PedeLocal.UnitTests.TestSupport;

public class OrderTestFixture
{
    public required string DatabaseName { get; init; }
    public required PedeLocalDbContext Db { get; init; }
    public required City City { get; init; }
    public required Establishment Establishment { get; init; }
    public required Guid StaffUserId { get; init; }
    public required Guid CustomerUserId { get; init; }
    public required Product Product { get; init; }
    public required ProductOptionGroup OptionGroup { get; init; }
    public required ProductOption Option { get; init; }
    public required Address CustomerAddress { get; init; }

    public static async Task<OrderTestFixture> CreateAsync(int? stockQuantity = 10, decimal productPrice = 20m)
    {
        var databaseName = Guid.NewGuid().ToString();
        var db = InMemoryDbContextFactory.CreateAdditionalContext(databaseName);
        foreach (var name in RoleNames.All)
        {
            db.Roles.Add(new Role { Name = name });
        }

        var city = new City { Name = "Guaimbê", State = "SP", IsActive = true };
        db.Cities.Add(city);

        var establishmentAddress = new Address
        {
            CityId = city.Id, Type = AddressType.Urban, Street = "Rua da Loja", Number = "1", District = "Centro",
            Latitude = -21.850m, Longitude = -49.900m
        };
        db.Addresses.Add(establishmentAddress);

        var establishment = new Establishment
        {
            CityId = city.Id, Name = "Lanchonete do Zé", Slug = "lanchonete-do-ze", Document = "1", Phone = "1",
            Email = "loja@loja.com", CategoryId = Guid.NewGuid(), AddressId = establishmentAddress.Id,
            OperationalStatus = OperationalStatus.Open, CommercialStatus = CommercialStatus.Active
        };
        db.Establishments.Add(establishment);

        var staffUserId = Guid.NewGuid();
        db.EstablishmentUsers.Add(new EstablishmentUser { EstablishmentId = establishment.Id, UserId = staffUserId, StaffRole = RoleNames.EstablishmentAdmin });
        db.EstablishmentSettings.Add(new EstablishmentSettings { EstablishmentId = establishment.Id, IsVisible = true, AcceptsNewOrders = true });
        db.DeliveryPricingSettings.Add(new DeliveryPricingSettings
        {
            EstablishmentId = establishment.Id,
            DeliveryFee = 7m,
            DeliveryRadiusKm = 50
        });

        var category = new MenuCategory { EstablishmentId = establishment.Id, Name = "Lanches" };
        db.MenuCategories.Add(category);

        var optionGroup = new ProductOptionGroup { Name = "Adicionais", Required = false, Min = 0, Max = 2 };
        var option = new ProductOption { Name = "Bacon", PriceDelta = 4m, IsAvailable = true };

        var product = new Product
        {
            EstablishmentId = establishment.Id, MenuCategoryId = category.Id, Name = "X-Burger",
            Price = productPrice, StockMode = stockQuantity.HasValue ? StockMode.Controlled : StockMode.Uncontrolled,
            StockQuantity = stockQuantity, IsAvailable = true
        };
        db.Products.Add(product);
        optionGroup.ProductId = product.Id;
        option.ProductOptionGroupId = optionGroup.Id;
        db.ProductOptionGroups.Add(optionGroup);
        db.ProductOptions.Add(option);

        var customerUserId = Guid.NewGuid();
        var customerAddress = new Address
        {
            CityId = city.Id, UserId = customerUserId, Type = AddressType.Urban,
            Street = "Rua do Cliente", Number = "50", District = "Centro",
            // ~1.1km from the establishment address for a small, predictable delivery fee.
            Latitude = -21.860m, Longitude = -49.900m
        };
        db.Addresses.Add(customerAddress);

        await db.SaveChangesAsync();

        return new OrderTestFixture
        {
            DatabaseName = databaseName, Db = db, City = city, Establishment = establishment, StaffUserId = staffUserId,
            CustomerUserId = customerUserId, Product = product, OptionGroup = optionGroup, Option = option, CustomerAddress = customerAddress
        };
    }
}
