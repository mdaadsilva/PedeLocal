using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Identity;
using PedeLocal.Domain.Addresses;
using PedeLocal.Domain.Catalog;
using PedeLocal.Domain.Cities;
using PedeLocal.Domain.Drivers;
using PedeLocal.Domain.Establishments;
using PedeLocal.Domain.Identity;
using PedeLocal.Domain.Pricing;
using PedeLocal.Domain.Subscriptions;

namespace PedeLocal.Infrastructure.Persistence.Seed;

/// <summary>Dev-only sample data (docs section 74): the 3 launch cities, a Super Admin, and one
/// example establishment/customer/driver so a fresh environment is immediately testable. Must
/// NEVER run outside Development — see the guard in Program.cs and the doc's own warning
/// ("nunca seed de credenciais fracas em produção").</summary>
public static class DevelopmentDataSeeder
{
    public const string SuperAdminEmail = "admin@pedelocal.dev";
    public const string MerchantAdminEmail = "loja@pedelocal.dev";
    public const string CustomerEmail = "cliente@pedelocal.dev";
    public const string DriverEmail = "entregador@pedelocal.dev";
    public const string DevPassword = "Senha123!";

    public static async Task SeedAsync(PedeLocalDbContext db, IPasswordHasher passwordHasher, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(u => u.Email == SuperAdminEmail, ct))
        {
            return; // already seeded.
        }

        var cities = new[]
        {
            new City { Name = "Guaimbê", State = "SP", IsActive = true },
            new City { Name = "Júlio Mesquita", State = "SP", IsActive = true },
            new City { Name = "Getulina", State = "SP", IsActive = true }
        };
        db.Cities.AddRange(cities);
        var guaimbe = cities[0];

        var superAdminRole = await db.Roles.SingleAsync(r => r.Name == RoleNames.SuperAdmin, ct);
        var customerRole = await db.Roles.SingleAsync(r => r.Name == RoleNames.Customer, ct);
        var driverRole = await db.Roles.SingleAsync(r => r.Name == RoleNames.Driver, ct);
        var establishmentAdminRole = await db.Roles.SingleAsync(r => r.Name == RoleNames.EstablishmentAdmin, ct);

        var superAdmin = CreateUser("Super Admin", SuperAdminEmail, "14999900001", passwordHasher);
        AssignRole(superAdmin, superAdminRole);
        db.Users.Add(superAdmin);

        var customer = CreateUser("Cliente Exemplo", CustomerEmail, "14999900002", passwordHasher);
        AssignRole(customer, customerRole);
        db.Users.Add(customer);

        var driverUser = CreateUser("Entregador Exemplo", DriverEmail, "14999900003", passwordHasher);
        AssignRole(driverUser, driverRole);
        db.Users.Add(driverUser);
        db.Drivers.Add(new Driver
        {
            UserId = driverUser.Id,
            CityId = guaimbe.Id,
            Cpf = "11122233344",
            VehicleType = VehicleType.Motorcycle,
            PixKeyType = PixKeyType.Cpf,
            PixKey = "11122233344",
            ApprovalStatus = DriverApprovalStatus.Approved,
            Status = DriverStatus.Offline,
            ApprovedAtUtc = DateTime.UtcNow
        });

        var category = await db.EstablishmentCategories.FirstOrDefaultAsync(c => c.Name == "Lanchonete", ct);

        var establishmentAddress = new Address
        {
            CityId = guaimbe.Id, Type = AddressType.Urban, Street = "Rua Principal", Number = "100",
            District = "Centro", ZipCode = "17370000", Latitude = -21.850000m, Longitude = -49.900000m
        };
        db.Addresses.Add(establishmentAddress);

        var establishment = new Establishment
        {
            CityId = guaimbe.Id, Name = "Lanchonete Exemplo", Slug = "lanchonete-exemplo",
            Document = "00000000000191", Phone = "14999900004", Email = "contato@lanchoneteexemplo.dev",
            CategoryId = category?.Id ?? Guid.NewGuid(), AddressId = establishmentAddress.Id,
            OperationalStatus = OperationalStatus.Open, CommercialStatus = CommercialStatus.Active
        };
        db.Establishments.Add(establishment);

        var merchantAdmin = CreateUser("Admin da Loja Exemplo", MerchantAdminEmail, "14999900005", passwordHasher);
        AssignRole(merchantAdmin, establishmentAdminRole);
        db.Users.Add(merchantAdmin);
        db.EstablishmentUsers.Add(new EstablishmentUser
        {
            EstablishmentId = establishment.Id, UserId = merchantAdmin.Id, StaffRole = RoleNames.EstablishmentAdmin
        });

        db.EstablishmentSettings.Add(new EstablishmentSettings
        {
            EstablishmentId = establishment.Id, IsVisible = true, AcceptsNewOrders = true
        });

        db.DeliveryPricingSettings.Add(new DeliveryPricingSettings
        {
            EstablishmentId = establishment.Id,
            UrbanMode = DeliveryFeeMode.PerKm, UrbanPricePerKm = 2m, UrbanMinFee = 5m,
            RuralMode = DeliveryFeeMode.PerKm, RuralPricePerKm = 3m, RuralMinFee = 15m,
            MaxDistanceKm = 20
        });

        db.EstablishmentSubscriptions.Add(new EstablishmentSubscription
        {
            EstablishmentId = establishment.Id, MonthlyAmount = 99.90m, BillingDay = 10,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)), GraceDays = 5
        });

        var menuCategory = new MenuCategory { EstablishmentId = establishment.Id, Name = "Lanches", SortOrder = 1 };
        db.MenuCategories.Add(menuCategory);
        db.Products.Add(new Product
        {
            EstablishmentId = establishment.Id, MenuCategoryId = menuCategory.Id, Name = "X-Burger",
            Description = "Pão, hambúrguer, queijo e salada.", Price = 20m,
            StockMode = StockMode.Uncontrolled, IsAvailable = true
        });

        await db.SaveChangesAsync(ct);
    }

    private static User CreateUser(string name, string email, string phone, IPasswordHasher hasher) => new()
    {
        Name = name, Email = email, PhoneNumber = phone, PasswordHash = hasher.Hash(DevPassword), IsActive = true
    };

    private static void AssignRole(User user, Role role) =>
        user.UserRoles.Add(new UserRole { User = user, Role = role, UserId = user.Id, RoleId = role.Id });
}
