using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PedeLocal.Application.Identity;
using PedeLocal.Domain.Addresses;
using PedeLocal.Domain.Catalog;
using PedeLocal.Domain.Cities;
using PedeLocal.Domain.Drivers;
using PedeLocal.Domain.Establishments;
using PedeLocal.Domain.Identity;
using PedeLocal.Domain.Pricing;
using PedeLocal.Infrastructure.Persistence;

namespace PedeLocal.IntegrationTests.Infrastructure;

/// <summary>Seeds prerequisite data directly through the DbContext (bypassing HTTP) so each test
/// doesn't have to walk the whole admin-creates-everything flow just to get to the scenario it
/// actually wants to exercise. Every test still authenticates and performs its actual assertions
/// through real HTTP calls — only the "given this data already exists" setup is short-circuited.
/// Passwords are always <see cref="Password"/> so tests can log in via the real /api/auth/login
/// endpoint afterwards.</summary>
public static class IntegrationDataSeeder
{
    public const string Password = "Senha123!";

    public record Scenario(
        Guid CityId,
        Guid EstablishmentId,
        Guid ProductId,
        Guid ProductOptionId,
        string CustomerEmail,
        Guid CustomerAddressId,
        string MerchantEmail,
        Guid MerchantUserId);

    public static async Task<Scenario> SeedOrderableEstablishmentAsync(PedeLocalWebApplicationFactory factory)
    {
        using var scope = factory.CreateDataScope();
        var db = scope.ServiceProvider.GetRequiredService<PedeLocalDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

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
            CityId = city.Id, Name = "Lanchonete Integração", Slug = $"lanchonete-integracao-{Guid.NewGuid():N}",
            Document = "1", Phone = "1", Email = "loja@integracao.dev", CategoryId = Guid.NewGuid(),
            AddressId = establishmentAddress.Id, OperationalStatus = OperationalStatus.Open, CommercialStatus = CommercialStatus.Active
        };
        db.Establishments.Add(establishment);
        db.EstablishmentSettings.Add(new EstablishmentSettings { EstablishmentId = establishment.Id, IsVisible = true, AcceptsNewOrders = true });
        db.DeliveryPricingSettings.Add(new DeliveryPricingSettings
        {
            EstablishmentId = establishment.Id,
            UrbanMode = DeliveryFeeMode.PerKm, UrbanPricePerKm = 3m, UrbanMinFee = 5m,
            RuralMode = DeliveryFeeMode.PerKm, RuralPricePerKm = 3m, RuralMinFee = 15m,
            MaxDistanceKm = 50
        });

        var category = new MenuCategory { EstablishmentId = establishment.Id, Name = "Lanches" };
        db.MenuCategories.Add(category);

        var optionGroup = new ProductOptionGroup { Name = "Adicionais", Required = false, Min = 0, Max = 2 };
        var option = new ProductOption { Name = "Bacon", PriceDelta = 4m, IsAvailable = true };
        var product = new Product
        {
            EstablishmentId = establishment.Id, MenuCategoryId = category.Id, Name = "X-Burger",
            Price = 20m, StockMode = StockMode.Uncontrolled, IsAvailable = true
        };
        db.Products.Add(product);
        optionGroup.ProductId = product.Id;
        option.ProductOptionGroupId = optionGroup.Id;
        db.ProductOptionGroups.Add(optionGroup);
        db.ProductOptions.Add(option);

        var establishmentAdminRole = await db.Roles.SingleAsync(r => r.Name == RoleNames.EstablishmentAdmin);
        var merchantEmail = $"merchant-{Guid.NewGuid():N}@integracao.dev";
        var merchantUser = new User { Name = "Admin Loja", Email = merchantEmail, PhoneNumber = "1", PasswordHash = hasher.Hash(Password) };
        merchantUser.UserRoles.Add(new UserRole { User = merchantUser, Role = establishmentAdminRole, UserId = merchantUser.Id, RoleId = establishmentAdminRole.Id });
        db.Users.Add(merchantUser);
        db.EstablishmentUsers.Add(new EstablishmentUser { EstablishmentId = establishment.Id, UserId = merchantUser.Id, StaffRole = RoleNames.EstablishmentAdmin });

        var customerRole = await db.Roles.SingleAsync(r => r.Name == RoleNames.Customer);
        var customerEmail = $"customer-{Guid.NewGuid():N}@integracao.dev";
        var customerUser = new User { Name = "Cliente Integração", Email = customerEmail, PhoneNumber = "1", PasswordHash = hasher.Hash(Password) };
        customerUser.UserRoles.Add(new UserRole { User = customerUser, Role = customerRole, UserId = customerUser.Id, RoleId = customerRole.Id });
        db.Users.Add(customerUser);

        var customerAddress = new Address
        {
            CityId = city.Id, UserId = customerUser.Id, Type = AddressType.Urban,
            Street = "Rua do Cliente", Number = "50", District = "Centro",
            Latitude = -21.860m, Longitude = -49.900m
        };
        db.Addresses.Add(customerAddress);

        await db.SaveChangesAsync();

        return new Scenario(city.Id, establishment.Id, product.Id, option.Id, customerEmail, customerAddress.Id, merchantEmail, merchantUser.Id);
    }

    /// <summary>Adds an Approved, Available driver in the given city, ready to accept deliveries.</summary>
    public static async Task<(string Email, Guid UserId, Guid DriverId)> SeedApprovedDriverAsync(PedeLocalWebApplicationFactory factory, Guid cityId)
    {
        using var scope = factory.CreateDataScope();
        var db = scope.ServiceProvider.GetRequiredService<PedeLocalDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var driverRole = await db.Roles.SingleAsync(r => r.Name == RoleNames.Driver);
        var email = $"driver-{Guid.NewGuid():N}@integracao.dev";
        var user = new User { Name = "Entregador Integração", Email = email, PhoneNumber = "1", PasswordHash = hasher.Hash(Password) };
        user.UserRoles.Add(new UserRole { User = user, Role = driverRole, UserId = user.Id, RoleId = driverRole.Id });
        db.Users.Add(user);

        var driver = new Driver
        {
            UserId = user.Id, CityId = cityId, Cpf = Guid.NewGuid().ToString("N")[..11], VehicleType = VehicleType.Motorcycle,
            PixKeyType = PixKeyType.Cpf, PixKey = "x", ApprovalStatus = DriverApprovalStatus.Approved, Status = DriverStatus.Available
        };
        db.Drivers.Add(driver);

        await db.SaveChangesAsync();
        return (email, user.Id, driver.Id);
    }

    public static async Task SetEstablishmentCommercialStatusAsync(PedeLocalWebApplicationFactory factory, Guid establishmentId, CommercialStatus status)
    {
        using var scope = factory.CreateDataScope();
        var db = scope.ServiceProvider.GetRequiredService<PedeLocalDbContext>();
        var establishment = await db.Establishments.SingleAsync(e => e.Id == establishmentId);
        establishment.CommercialStatus = status;
        await db.SaveChangesAsync();
    }
}
