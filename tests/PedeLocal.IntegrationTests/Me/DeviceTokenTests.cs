using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PedeLocal.Contracts.Notifications;
using PedeLocal.Infrastructure.Persistence;
using PedeLocal.IntegrationTests.Infrastructure;
using Xunit;

namespace PedeLocal.IntegrationTests.Me;

/// <summary>Real HTTP coverage of PUT/DELETE /api/me/device-tokens (docs/PUSH_NOTIFICATIONS.md).</summary>
public class DeviceTokenTests : IClassFixture<PedeLocalWebApplicationFactory>
{
    private readonly PedeLocalWebApplicationFactory _factory;

    public DeviceTokenTests(PedeLocalWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_Authenticated_PersistsUnderCallersOwnUserId()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var client = _factory.CreateClient();
        await client.AsAuthenticatedAsync(scenario.CustomerEmail, IntegrationDataSeeder.Password);

        var response = await client.PutAsJsonAsync("/api/me/device-tokens", new RegisterDeviceTokenRequest("device-token-abc", "Android"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var scope = _factory.CreateDataScope();
        var db = scope.ServiceProvider.GetRequiredService<PedeLocalDbContext>();
        var token = await db.DeviceTokens.SingleAsync(t => t.Token == "device-token-abc");
        Assert.Equal(scenario.CustomerEmail, (await db.Users.SingleAsync(u => u.Id == token.UserId)).Email);
    }

    [Fact]
    public async Task Register_SameTokenAgain_UpdatesRatherThanDuplicates()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var client = _factory.CreateClient();
        await client.AsAuthenticatedAsync(scenario.CustomerEmail, IntegrationDataSeeder.Password);

        await client.PutAsJsonAsync("/api/me/device-tokens", new RegisterDeviceTokenRequest("device-token-repeat", "Android"));
        await client.PutAsJsonAsync("/api/me/device-tokens", new RegisterDeviceTokenRequest("device-token-repeat", "Ios"));

        using var scope = _factory.CreateDataScope();
        var db = scope.ServiceProvider.GetRequiredService<PedeLocalDbContext>();
        Assert.Equal(1, await db.DeviceTokens.CountAsync(t => t.Token == "device-token-repeat"));
    }

    [Fact]
    public async Task Register_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/me/device-tokens", new RegisterDeviceTokenRequest("device-token-xyz", "Android"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Remove_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/me/device-tokens")
        {
            Content = JsonContent.Create(new RemoveDeviceTokenRequest("device-token-xyz"))
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Remove_OwnToken_DeletesIt()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var client = _factory.CreateClient();
        await client.AsAuthenticatedAsync(scenario.CustomerEmail, IntegrationDataSeeder.Password);
        await client.PutAsJsonAsync("/api/me/device-tokens", new RegisterDeviceTokenRequest("device-token-to-remove", "Android"));

        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/me/device-tokens")
        {
            Content = JsonContent.Create(new RemoveDeviceTokenRequest("device-token-to-remove"))
        };
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var scope = _factory.CreateDataScope();
        var db = scope.ServiceProvider.GetRequiredService<PedeLocalDbContext>();
        Assert.False(await db.DeviceTokens.AnyAsync(t => t.Token == "device-token-to-remove"));
    }

    [Fact]
    public async Task Remove_AnotherUsersToken_DoesNotDeleteIt()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var ownerClient = _factory.CreateClient();
        await ownerClient.AsAuthenticatedAsync(scenario.CustomerEmail, IntegrationDataSeeder.Password);
        await ownerClient.PutAsJsonAsync("/api/me/device-tokens", new RegisterDeviceTokenRequest("owners-device-token", "Android"));

        var strangerClient = _factory.CreateClient();
        var strangerEmail = $"stranger-{Guid.NewGuid():N}@example.com";
        await strangerClient.PostAsJsonAsync("/api/auth/register/customer",
            new PedeLocal.Contracts.Auth.RegisterCustomerRequest("Estranho", strangerEmail, "14999990000", "Senha123"));
        await strangerClient.AsAuthenticatedAsync(strangerEmail, "Senha123");

        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/me/device-tokens")
        {
            Content = JsonContent.Create(new RemoveDeviceTokenRequest("owners-device-token"))
        };
        var response = await strangerClient.SendAsync(request);

        // Idempotent no-op, not an error — but the token must still exist, owned by its real owner.
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var scope = _factory.CreateDataScope();
        var db = scope.ServiceProvider.GetRequiredService<PedeLocalDbContext>();
        Assert.True(await db.DeviceTokens.AnyAsync(t => t.Token == "owners-device-token"));
    }
}
