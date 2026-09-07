using PedeLocal.Domain.Identity;
using PedeLocal.Infrastructure.Identity;
using PedeLocal.Infrastructure.Persistence.Seed;
using PedeLocal.UnitTests.TestSupport;
using Xunit;

namespace PedeLocal.UnitTests.Seed;

public class DevelopmentDataSeederTests
{
    [Fact]
    public async Task SeedAsync_CreatesCitiesUsersAndExampleEstablishment()
    {
        var db = InMemoryDbContextFactory.Create();
        var hasher = new BCryptPasswordHasher();

        await DevelopmentDataSeeder.SeedAsync(db, hasher, CancellationToken.None);

        Assert.Equal(3, db.Cities.Count());
        Assert.Contains(db.Cities, c => c.Name == "Guaimbê");
        Assert.Contains(db.Cities, c => c.Name == "Júlio Mesquita");
        Assert.Contains(db.Cities, c => c.Name == "Getulina");

        Assert.Single(db.Establishments);
        Assert.Single(db.Products);
        Assert.Single(db.Drivers);
        Assert.NotNull(db.Users.SingleOrDefault(u => u.Email == DevelopmentDataSeeder.SuperAdminEmail));
        Assert.NotNull(db.Users.SingleOrDefault(u => u.Email == DevelopmentDataSeeder.CustomerEmail));
        Assert.NotNull(db.Users.SingleOrDefault(u => u.Email == DevelopmentDataSeeder.DriverEmail));
        Assert.NotNull(db.Users.SingleOrDefault(u => u.Email == DevelopmentDataSeeder.MerchantAdminEmail));

        var superAdmin = db.Users.Single(u => u.Email == DevelopmentDataSeeder.SuperAdminEmail);
        Assert.True(hasher.Verify(DevelopmentDataSeeder.DevPassword, superAdmin.PasswordHash));
    }

    [Fact]
    public async Task SeedAsync_RunTwice_IsIdempotent()
    {
        var db = InMemoryDbContextFactory.Create();
        var hasher = new BCryptPasswordHasher();

        await DevelopmentDataSeeder.SeedAsync(db, hasher, CancellationToken.None);
        await DevelopmentDataSeeder.SeedAsync(db, hasher, CancellationToken.None);

        Assert.Equal(3, db.Cities.Count());
        Assert.Single(db.Establishments);
    }
}
