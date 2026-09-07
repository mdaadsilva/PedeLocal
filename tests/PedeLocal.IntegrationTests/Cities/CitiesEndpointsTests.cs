using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PedeLocal.Contracts.Cities;
using PedeLocal.Domain.Cities;
using PedeLocal.IntegrationTests.Infrastructure;
using PedeLocal.Infrastructure.Persistence;
using Xunit;

namespace PedeLocal.IntegrationTests.Cities;

public class CitiesEndpointsTests : IClassFixture<PedeLocalWebApplicationFactory>
{
    private readonly PedeLocalWebApplicationFactory _factory;

    public CitiesEndpointsTests(PedeLocalWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetCities_ReturnsOnlyActiveCities()
    {
        using (var scope = _factory.CreateDataScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PedeLocalDbContext>();
            db.Cities.AddRange(
                new City { Name = "Guaimbê", State = "SP", IsActive = true },
                new City { Name = "Cidade Inativa", State = "SP", IsActive = false });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/cities");
        response.EnsureSuccessStatusCode();

        var cities = await response.Content.ReadFromJsonAsync<List<CityResponse>>();
        Assert.NotNull(cities);
        Assert.Contains(cities!, c => c.Name == "Guaimbê");
        Assert.DoesNotContain(cities!, c => c.Name == "Cidade Inativa");
    }

    [Fact]
    public async Task GetCities_IsPublic_NoAuthRequired()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/cities");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
