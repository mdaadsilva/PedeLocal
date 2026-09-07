using System.Net;
using PedeLocal.IntegrationTests.Infrastructure;
using Xunit;

namespace PedeLocal.IntegrationTests;

public class SmokeTests : IClassFixture<PedeLocalWebApplicationFactory>, IDisposable
{
    private readonly PedeLocalWebApplicationFactory _factory;

    public SmokeTests(PedeLocalWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetCities_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/cities");
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public void Dispose() => GC.SuppressFinalize(this);
}
