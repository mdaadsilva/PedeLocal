using PedeLocal.Application.Me;
using PedeLocal.Contracts.Me;
using PedeLocal.Domain.Identity;
using PedeLocal.UnitTests.TestSupport;
using Xunit;

namespace PedeLocal.UnitTests.Me;

public class MeServiceTests
{
    private static async Task<(PedeLocal.Infrastructure.Persistence.PedeLocalDbContext Db, Guid UserId)> SeedAsync()
    {
        var db = InMemoryDbContextFactory.Create();
        var customerRole = db.Roles.Single(r => r.Name == RoleNames.Customer);
        var user = new User { Name = "Maria", Email = "maria@example.com", PhoneNumber = "14999990000", PasswordHash = "hash" };
        user.UserRoles.Add(new UserRole { User = user, Role = customerRole, UserId = user.Id, RoleId = customerRole.Id });
        db.Users.Add(user);
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, TokenHash = "abc", ExpiresAtUtc = DateTime.UtcNow.AddDays(30) });
        await db.SaveChangesAsync();
        return (db, user.Id);
    }

    [Fact]
    public async Task UpdateProfile_ChangesNameAndPhone()
    {
        var (db, userId) = await SeedAsync();
        var sut = new MeService(db, new FakeCurrentUserService(userId));

        var response = await sut.UpdateProfileAsync(new UpdateProfileRequest("Maria Silva", "14999991111"), CancellationToken.None);

        Assert.Equal("Maria Silva", response.Name);
    }

    [Fact]
    public async Task DeleteAccount_AnonymizesAndRevokesSessions()
    {
        var (db, userId) = await SeedAsync();
        var sut = new MeService(db, new FakeCurrentUserService(userId));

        await sut.DeleteAccountAsync(CancellationToken.None);

        var user = db.Users.Single(u => u.Id == userId);
        Assert.False(user.IsActive);
        Assert.NotNull(user.DeletedAtUtc);
        Assert.NotEqual("maria@example.com", user.Email);
        Assert.All(db.RefreshTokens.Where(rt => rt.UserId == userId), rt => Assert.NotNull(rt.RevokedAtUtc));
    }

    [Fact]
    public async Task GetMyData_ReturnsCounts()
    {
        var (db, userId) = await SeedAsync();
        var sut = new MeService(db, new FakeCurrentUserService(userId));

        var data = await sut.GetMyDataAsync(CancellationToken.None);

        Assert.Equal(0, data.AddressCount);
        Assert.Equal(0, data.OrderCount);
        Assert.Contains(RoleNames.Customer, data.Roles);
    }
}
