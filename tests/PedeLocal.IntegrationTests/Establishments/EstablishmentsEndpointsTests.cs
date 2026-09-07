using System.Net.Http.Json;
using PedeLocal.Contracts.Common;
using PedeLocal.Contracts.Establishments;
using PedeLocal.IntegrationTests.Infrastructure;
using Xunit;

namespace PedeLocal.IntegrationTests.Establishments;

public class EstablishmentsEndpointsTests : IClassFixture<PedeLocalWebApplicationFactory>
{
    private readonly PedeLocalWebApplicationFactory _factory;

    public EstablishmentsEndpointsTests(PedeLocalWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetEstablishments_FilteredByCity_ReturnsVisibleActiveOnes()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);

        var client = _factory.CreateClient();
        await client.AsAuthenticatedAsync(scenario.CustomerEmail, IntegrationDataSeeder.Password);

        var response = await client.GetAsync($"/api/establishments?cityId={scenario.CityId}");
        response.EnsureSuccessStatusCode();

        var page = await response.Content.ReadFromJsonAsync<PagedResult<EstablishmentResponse>>();
        Assert.NotNull(page);
        Assert.Contains(page!.Items, e => e.Id == scenario.EstablishmentId);
    }

    [Fact]
    public async Task GetEstablishmentProducts_ReturnsSeededProduct()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);

        var client = _factory.CreateClient();
        await client.AsAuthenticatedAsync(scenario.CustomerEmail, IntegrationDataSeeder.Password);

        var response = await client.GetAsync($"/api/establishments/{scenario.EstablishmentId}/products");
        response.EnsureSuccessStatusCode();

        var products = await response.Content.ReadFromJsonAsync<List<PedeLocal.Contracts.Catalog.ProductResponse>>();
        Assert.NotNull(products);
        Assert.Contains(products!, p => p.Id == scenario.ProductId);
    }

    [Fact]
    public async Task GetEstablishments_WithoutAuth_Returns401()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/establishments?cityId={scenario.CityId}");

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
