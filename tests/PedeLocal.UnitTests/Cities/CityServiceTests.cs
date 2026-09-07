using PedeLocal.Application.Cities;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Contracts.Cities;
using PedeLocal.UnitTests.TestSupport;
using Xunit;

namespace PedeLocal.UnitTests.Cities;

public class CityServiceTests
{
    [Fact]
    public async Task Create_ThenGetActive_ReturnsIt()
    {
        using var db = InMemoryDbContextFactory.Create();
        var sut = new CityService(db);

        var created = await sut.CreateAsync(new CreateCityRequest("Guaimbê", "sp"), CancellationToken.None);
        Assert.Equal("SP", created.State);

        var active = await sut.GetActiveAsync(CancellationToken.None);
        Assert.Contains(active, c => c.Id == created.Id);
    }

    [Fact]
    public async Task SetStatus_Inactive_RemovesFromActiveList()
    {
        using var db = InMemoryDbContextFactory.Create();
        var sut = new CityService(db);
        var created = await sut.CreateAsync(new CreateCityRequest("Getulina", "SP"), CancellationToken.None);

        await sut.SetStatusAsync(created.Id, new UpdateCityStatusRequest(false), CancellationToken.None);

        var active = await sut.GetActiveAsync(CancellationToken.None);
        Assert.DoesNotContain(active, c => c.Id == created.Id);
    }

    [Fact]
    public async Task Update_UnknownId_Throws()
    {
        using var db = InMemoryDbContextFactory.Create();
        var sut = new CityService(db);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateAsync(Guid.NewGuid(), new UpdateCityRequest("X", "SP"), CancellationToken.None));
    }
}
