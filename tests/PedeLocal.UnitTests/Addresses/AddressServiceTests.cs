using PedeLocal.Application.Addresses;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Contracts.Addresses;
using PedeLocal.Domain.Cities;
using PedeLocal.Infrastructure.Persistence;
using PedeLocal.UnitTests.TestSupport;
using Xunit;

namespace PedeLocal.UnitTests.Addresses;

public class AddressServiceTests
{
    private static async Task<(PedeLocalDbContext Db, AddressService Sut, Guid CityId, Guid UserId)> SeedAsync()
    {
        var db = InMemoryDbContextFactory.Create();
        var city = new City { Name = "Guaimbê", State = "SP", IsActive = true };
        db.Cities.Add(city);
        await db.SaveChangesAsync();

        var userId = Guid.NewGuid();
        return (db, new AddressService(db, new FakeCurrentUserService(userId)), city.Id, userId);
    }

    [Fact]
    public async Task Create_Urban_Succeeds()
    {
        var (_, sut, cityId, _) = await SeedAsync();

        var response = await sut.CreateAsync(new CreateAddressRequest(
            cityId, "Urban", "Casa", "Rua A", "100", "Centro", null, null, "17370000",
            null, null, null, -21.8m, -49.9m, null), CancellationToken.None);

        Assert.Equal("Urban", response.Type);
    }

    [Fact]
    public async Task Create_Rural_StoresReferenceAndPropertyFields()
    {
        var (_, sut, cityId, _) = await SeedAsync();

        var response = await sut.CreateAsync(new CreateAddressRequest(
            cityId, "Rural", "Sítio", null, null, null, null, "Perto da igreja", null,
            "Sítio Boa Vista", "Estrada Municipal", "Km 5", -21.8m, -49.9m, null), CancellationToken.None);

        Assert.Equal("Rural", response.Type);
        Assert.Equal("Perto da igreja", response.Reference);
    }

    [Fact]
    public async Task Update_AddressOwnedByAnotherUser_Throws()
    {
        var (db, sut, cityId, _) = await SeedAsync();
        var created = await sut.CreateAsync(new CreateAddressRequest(
            cityId, "Urban", null, "Rua A", "100", "Centro", null, null, null,
            null, null, null, -21.8m, -49.9m, null), CancellationToken.None);

        var otherUserSut = new AddressService(db, new FakeCurrentUserService(Guid.NewGuid()));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            otherUserSut.UpdateAsync(created.Id, new UpdateAddressRequest(
                "Urban", null, "Rua B", "200", "Centro", null, null, null, null, null, null, -21.8m, -49.9m, null),
                CancellationToken.None));
    }

    [Fact]
    public async Task Delete_RemovesAddress()
    {
        var (db, sut, cityId, _) = await SeedAsync();
        var created = await sut.CreateAsync(new CreateAddressRequest(
            cityId, "Urban", null, "Rua A", "100", "Centro", null, null, null,
            null, null, null, -21.8m, -49.9m, null), CancellationToken.None);

        await sut.DeleteAsync(created.Id, CancellationToken.None);

        Assert.Empty(await sut.GetMineAsync(CancellationToken.None));
    }
}
