using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PedeLocal.Application.Deliveries;
using PedeLocal.Application.Identity;
using PedeLocal.Domain.Identity;
using PedeLocal.Infrastructure.Persistence;
using PedeLocal.Infrastructure.Persistence.Seed;
using PedeLocal.Infrastructure.Persistence.Seed.Demo;
using PedeLocal.IntegrationTests.Infrastructure;

namespace PedeLocal.IntegrationTests.Demo;

/// <summary>Exercises DemoDataSeeder against a real (InMemory) DbContext through the actual app's
/// DI container — real IPasswordHasher/ITokenService/IDeliveryCodeCipher implementations, not
/// fakes. Uses the SMALL profile throughout to stay fast; DemoDataPlannerTests (UnitTests project)
/// covers the DEMO/LARGE-scale business-rule invariants against the pure planner with no
/// database.</summary>
public class DemoDataSeederTests
{
    /// <summary>The returned <see cref="IServiceScope"/> owns <c>Db</c> — the CALLER must keep it
    /// alive (e.g. `using var scope = ...Scope`) for as long as <c>Db</c> is used.</summary>
    private static async Task<(IServiceScope Scope, PedeLocalDbContext Db, IPasswordHasher Hasher, ITokenService Tokens, IDeliveryCodeCipher Cipher)> PrepareAsync(
        PedeLocalWebApplicationFactory factory)
    {
        var scope = factory.CreateDataScope();
        var db = scope.ServiceProvider.GetRequiredService<PedeLocalDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        // WebApplicationFactory runs under ASPNETCORE_ENVIRONMENT=Testing, so Program.cs's own
        // Development-only seeding never fires automatically — DevelopmentDataSeeder is invoked
        // explicitly here, exactly like the real `dotnet run` startup path does before ever reaching
        // --seed-demo (DemoDataSeeder always assumes the Super Admin it reuses already exists).
        await DevelopmentDataSeeder.SeedAsync(db, hasher);

        return (scope, db, hasher, scope.ServiceProvider.GetRequiredService<ITokenService>(), scope.ServiceProvider.GetRequiredService<IDeliveryCodeCipher>());
    }

    [Fact]
    public async Task SeedAsync_OutsideDevelopment_Throws_AndWritesNothing()
    {
        await using var factory = new PedeLocalWebApplicationFactory();
        var (scope, db, hasher, tokens, cipher) = await PrepareAsync(factory);
        using var _ = scope;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DemoDataSeeder.SeedAsync(db, hasher, tokens, cipher, DemoSeedProfile.Small, "Production"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DemoDataSeeder.SeedAsync(db, hasher, tokens, cipher, DemoSeedProfile.Small, "Staging"));

        Assert.False(await db.Establishments.AnyAsync(e => e.Slug != "lanchonete-exemplo"));
    }

    [Fact]
    public async Task ResetAsync_OutsideDevelopment_Throws()
    {
        await using var factory = new PedeLocalWebApplicationFactory();
        var (scope, db, _, _, _) = await PrepareAsync(factory);
        using var _ = scope;

        await Assert.ThrowsAsync<InvalidOperationException>(() => DemoDataSeeder.ResetAsync(db, DemoSeedProfile.Small, "Production"));
    }

    [Fact]
    public async Task SeedAsync_CalledTwice_DoesNotDuplicateAnything()
    {
        await using var factory = new PedeLocalWebApplicationFactory();
        var (scope, db, hasher, tokens, cipher) = await PrepareAsync(factory);
        using var _ = scope;

        await DemoDataSeeder.SeedAsync(db, hasher, tokens, cipher, DemoSeedProfile.Small, "Development");
        var establishmentCountAfterFirst = await db.Establishments.CountAsync();
        var orderCountAfterFirst = await db.Orders.CountAsync();
        var userCountAfterFirst = await db.Users.CountAsync();

        await DemoDataSeeder.SeedAsync(db, hasher, tokens, cipher, DemoSeedProfile.Small, "Development");

        Assert.Equal(establishmentCountAfterFirst, await db.Establishments.CountAsync());
        Assert.Equal(orderCountAfterFirst, await db.Orders.CountAsync());
        Assert.Equal(userCountAfterFirst, await db.Users.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_PredictableAccounts_AuthenticateWithTheDocumentedPassword()
    {
        await using var factory = new PedeLocalWebApplicationFactory();
        var (scope, db, hasher, tokens, cipher) = await PrepareAsync(factory);
        using var _ = scope;

        await DemoDataSeeder.SeedAsync(db, hasher, tokens, cipher, DemoSeedProfile.Small, "Development");

        var customer = await db.Users.SingleAsync(u => u.Email == "customer.guaimbe@pedelocal.dev");
        var driver = await db.Users.SingleAsync(u => u.Email == "driver.guaimbe@pedelocal.dev");
        var merchant = await db.Users.SingleAsync(u => u.Email == "merchant.guaimbe@pedelocal.dev");

        Assert.True(hasher.Verify(DevelopmentDataSeeder.DevPassword, customer.PasswordHash));
        Assert.True(hasher.Verify(DevelopmentDataSeeder.DevPassword, driver.PasswordHash));
        Assert.True(hasher.Verify(DevelopmentDataSeeder.DevPassword, merchant.PasswordHash));
    }

    [Fact]
    public async Task SeedAsync_MerchantOwner_OnlyAdministersItsOwnEstablishment()
    {
        await using var factory = new PedeLocalWebApplicationFactory();
        var (scope, db, hasher, tokens, cipher) = await PrepareAsync(factory);
        using var _ = scope;

        await DemoDataSeeder.SeedAsync(db, hasher, tokens, cipher, DemoSeedProfile.Small, "Development");

        var merchant = await db.Users.SingleAsync(u => u.Email == "merchant.guaimbe@pedelocal.dev");
        var links = await db.EstablishmentUsers.Where(eu => eu.UserId == merchant.Id).ToListAsync();

        Assert.Single(links);
        Assert.Equal(RoleNames.EstablishmentAdmin, links[0].StaffRole);
    }

    [Fact]
    public async Task SeedAsync_NeverProducesNegativeStock_AndDeliveriesStayWithinTheirCity()
    {
        await using var factory = new PedeLocalWebApplicationFactory();
        var (scope, db, hasher, tokens, cipher) = await PrepareAsync(factory);
        using var _ = scope;

        await DemoDataSeeder.SeedAsync(db, hasher, tokens, cipher, DemoSeedProfile.Small, "Development");

        var negativeStock = await db.Products
            .Where(p => p.StockMode == Domain.Catalog.StockMode.Controlled && p.StockQuantity < 0)
            .CountAsync();
        Assert.Equal(0, negativeStock);

        var driverCityById = await db.Drivers.ToDictionaryAsync(d => d.Id, d => d.CityId);
        var deliveriesWithDriver = await db.Deliveries.Where(d => d.DriverId != null).ToListAsync();
        Assert.All(deliveriesWithDriver, d => Assert.Equal(d.CityId, driverCityById[d.DriverId!.Value]));
    }

    [Fact]
    public async Task SeedAsync_NormalEstablishments_HaveASubscription_ExceptTheDocumentedException()
    {
        await using var factory = new PedeLocalWebApplicationFactory();
        var (scope, db, hasher, tokens, cipher) = await PrepareAsync(factory);
        using var _ = scope;

        await DemoDataSeeder.SeedAsync(db, hasher, tokens, cipher, DemoSeedProfile.Small, "Development");

        var establishmentIds = await db.Establishments.Where(e => e.Slug != "lanchonete-exemplo").Select(e => e.Id).ToListAsync();
        var subscribedIds = await db.EstablishmentSubscriptions.Select(s => s.EstablishmentId).ToListAsync();
        var withoutSubscription = establishmentIds.Except(subscribedIds).ToList();

        // SMALL reserves at most 1 deliberate "no subscription" establishment (see DemoDataPlanner —
        // 3 is the cap, but SMALL only has 1 city).
        Assert.True(withoutSubscription.Count <= 1);
    }

    [Fact]
    public async Task ResetAsync_RemovesOnlyDemoRows_DevelopmentDataSeederRowsSurviveIntact()
    {
        await using var factory = new PedeLocalWebApplicationFactory();
        var (scope, db, hasher, tokens, cipher) = await PrepareAsync(factory);
        using var _ = scope;

        await DemoDataSeeder.SeedAsync(db, hasher, tokens, cipher, DemoSeedProfile.Small, "Development");
        Assert.True(await db.Establishments.CountAsync() > 1);

        await DemoDataSeeder.ResetAsync(db, DemoSeedProfile.Small, "Development");

        var remainingEstablishment = await db.Establishments.SingleAsync();
        Assert.Equal("lanchonete-exemplo", remainingEstablishment.Slug);
        Assert.True(await db.Users.AnyAsync(u => u.Email == DevelopmentDataSeeder.SuperAdminEmail));
        Assert.True(await db.Users.AnyAsync(u => u.Email == DevelopmentDataSeeder.MerchantAdminEmail));
        Assert.True(await db.Users.AnyAsync(u => u.Email == DevelopmentDataSeeder.CustomerEmail));
        Assert.True(await db.Users.AnyAsync(u => u.Email == DevelopmentDataSeeder.DriverEmail));
        Assert.False(await db.Users.AnyAsync(u => u.Email == "customer.guaimbe@pedelocal.dev"));
        Assert.False(await db.Orders.AnyAsync());
        Assert.False(await db.Products.AnyAsync(p => p.Name != "X-Burger"));
    }

    [Fact]
    public async Task SeedAsync_SwitchingProfileWithoutResetting_DoesNotViolateUniqueConstraints()
    {
        // Regression test: predictable accounts (customer/driver/merchant.<cidade>@pedelocal.dev)
        // intentionally share the same literal email across every profile, and filler accounts used
        // an unqualified counter in their email/CPF — both meant a second profile seeded on top of
        // a first, without resetting, used to throw a raw Postgres unique-constraint violation
        // (Users.Email / Drivers.Cpf). Not the recommended workflow (docs/DEMO_DATA.md recommends
        // resetting before switching profiles), but it must never crash.
        await using var factory = new PedeLocalWebApplicationFactory();
        var (scope, db, hasher, tokens, cipher) = await PrepareAsync(factory);
        using var _ = scope;

        await DemoDataSeeder.SeedAsync(db, hasher, tokens, cipher, DemoSeedProfile.Small, "Development");
        var exception = await Record.ExceptionAsync(() =>
            DemoDataSeeder.SeedAsync(db, hasher, tokens, cipher, DemoSeedProfile.Demo, "Development"));

        Assert.Null(exception);
        Assert.True(await db.Establishments.CountAsync() > 5);
    }

    [Fact]
    public async Task SeedAsync_SeedsDriverLicensesAndVehicles_AndResetRemovesThem()
    {
        await using var factory = new PedeLocalWebApplicationFactory();
        var (scope, db, hasher, tokens, cipher) = await PrepareAsync(factory);
        using var _ = scope;

        await DemoDataSeeder.SeedAsync(db, hasher, tokens, cipher, DemoSeedProfile.Small, "Development");

        Assert.True(await db.DriverLicenses.AnyAsync());
        Assert.True(await db.DriverVehicles.AnyAsync());
        // Every DriverLicense/DriverVehicle row belongs to a seeded Driver — no orphans.
        var driverIds = await db.Drivers.Select(d => d.Id).ToListAsync();
        Assert.True(await db.DriverLicenses.AllAsync(l => driverIds.Contains(l.DriverId)));
        Assert.True(await db.DriverVehicles.AllAsync(v => driverIds.Contains(v.DriverId)));

        await DemoDataSeeder.ResetAsync(db, DemoSeedProfile.Small, "Development");

        // DevelopmentDataSeeder's own example driver ("Entregador Exemplo") has a license/vehicle
        // too, and — like its Establishment/Users rows — must survive a demo-data reset untouched.
        Assert.True(await db.DriverLicenses.CountAsync() <= 1);
        Assert.True(await db.DriverVehicles.CountAsync() <= 1);
    }

    [Fact]
    public async Task ResetAsync_WithoutAPriorSeed_IsANoOp()
    {
        await using var factory = new PedeLocalWebApplicationFactory();
        var (scope, db, _, _, _) = await PrepareAsync(factory);
        using var _ = scope;

        // Must not throw even though nothing demo-related was ever seeded for this profile.
        await DemoDataSeeder.ResetAsync(db, DemoSeedProfile.Small, "Development");

        Assert.True(await db.Users.AnyAsync(u => u.Email == DevelopmentDataSeeder.SuperAdminEmail));
    }
}
