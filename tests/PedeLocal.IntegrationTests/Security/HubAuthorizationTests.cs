using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using PedeLocal.Contracts.Auth;
using PedeLocal.Contracts.Orders;
using PedeLocal.IntegrationTests.Infrastructure;
using Xunit;

namespace PedeLocal.IntegrationTests.Security;

/// <summary>Real SignalR client coverage of the hub's group authorization (docs/REALTIME_EVENTS.md):
/// the server decides group membership from the JWT alone, so these tests connect actual hub
/// clients over the test server (LongPolling — TestServer has no raw WebSocket transport) and
/// assert cross-tenant events never cross the wire, not just that REST endpoints are guarded.</summary>
public class HubAuthorizationTests : IClassFixture<PedeLocalWebApplicationFactory>
{
    private static readonly TimeSpan ShortWait = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LongWait = TimeSpan.FromSeconds(10);

    private readonly PedeLocalWebApplicationFactory _factory;

    public HubAuthorizationTests(PedeLocalWebApplicationFactory factory) => _factory = factory;

    private async Task<HubConnection> ConnectAsync(string? accessToken)
    {
        var url = _factory.Server.BaseAddress + "hubs/pedelocal" + (accessToken is null ? "" : $"?access_token={accessToken}");
        var connection = new HubConnectionBuilder()
            .WithUrl(url, options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
        await connection.StartAsync();
        return connection;
    }

    private static TaskCompletionSource<JsonElement> ListenOnce(HubConnection connection, string eventName)
    {
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<JsonElement>(eventName, payload => tcs.TrySetResult(payload));
        return tcs;
    }

    private static async Task<bool> WaitForAsync(TaskCompletionSource<JsonElement> tcs, TimeSpan timeout)
    {
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
        return completed == tcs.Task;
    }

    [Fact]
    public async Task Connection_WithoutAccessToken_IsRejected()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => ConnectAsync(accessToken: null));
    }

    [Fact]
    public async Task Connection_WithInvalidAccessToken_IsRejected()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => ConnectAsync(accessToken: "not-a-real-jwt"));
    }

    [Fact]
    public async Task Customer_DoesNotReceiveOrderStatusChanges_ForAnotherCustomersOrder()
    {
        var scenario1 = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var scenario2 = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);

        var customer1Http = _factory.CreateClient();
        var customer1Tokens = await customer1Http.LoginAsync(scenario1.CustomerEmail, IntegrationDataSeeder.Password);
        customer1Http.SetBearerToken(customer1Tokens.AccessToken);

        var customer2Http = _factory.CreateClient();
        await customer2Http.AsAuthenticatedAsync(scenario2.CustomerEmail, IntegrationDataSeeder.Password);
        var merchant2Http = _factory.CreateClient();
        await merchant2Http.AsAuthenticatedAsync(scenario2.MerchantEmail, IntegrationDataSeeder.Password);

        var order1 = await (await customer1Http.PostAsJsonAsync("/api/orders", new CreateOrderRequest(
            scenario1.EstablishmentId, scenario1.CustomerAddressId, "Pix", null,
            [new CreateOrderItemRequest(scenario1.ProductId, 1, [])], null))).Content.ReadFromJsonAsync<OrderResponse>();
        var order2 = await (await customer2Http.PostAsJsonAsync("/api/orders", new CreateOrderRequest(
            scenario2.EstablishmentId, scenario2.CustomerAddressId, "Pix", null,
            [new CreateOrderItemRequest(scenario2.ProductId, 1, [])], null))).Content.ReadFromJsonAsync<OrderResponse>();

        await using var customer1Hub = await ConnectAsync(customer1Tokens.AccessToken);
        var tcs = ListenOnce(customer1Hub, "OrderStatusChanged");

        // Another customer's order transitions — customer1 must not see this.
        await merchant2Http.PostAsync($"/api/merchant/establishments/{scenario2.EstablishmentId}/orders/{order2!.Id}/accept", null);
        Assert.False(await WaitForAsync(tcs, ShortWait));

        // Customer1's own order transitions — customer1 must see this one.
        var merchant1Http = _factory.CreateClient();
        await merchant1Http.AsAuthenticatedAsync(scenario1.MerchantEmail, IntegrationDataSeeder.Password);
        await merchant1Http.PostAsync($"/api/merchant/establishments/{scenario1.EstablishmentId}/orders/{order1!.Id}/accept", null);

        Assert.True(await WaitForAsync(tcs, LongWait));
        Assert.Equal(order1.Id, tcs.Task.Result.GetProperty("orderId").GetGuid());
    }

    [Fact]
    public async Task EstablishmentStaff_DoesNotReceiveOrderCreated_ForAnotherEstablishment()
    {
        var scenario1 = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var scenario2 = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);

        var merchant1Http = _factory.CreateClient();
        var merchant1Tokens = await merchant1Http.LoginAsync(scenario1.MerchantEmail, IntegrationDataSeeder.Password);
        merchant1Http.SetBearerToken(merchant1Tokens.AccessToken);

        var customer2Http = _factory.CreateClient();
        await customer2Http.AsAuthenticatedAsync(scenario2.CustomerEmail, IntegrationDataSeeder.Password);

        await using var merchant1Hub = await ConnectAsync(merchant1Tokens.AccessToken);
        var tcs = ListenOnce(merchant1Hub, "OrderCreated");

        // A new order at a DIFFERENT establishment — establishment1's staff must not see this.
        await customer2Http.PostAsJsonAsync("/api/orders", new CreateOrderRequest(
            scenario2.EstablishmentId, scenario2.CustomerAddressId, "Pix", null,
            [new CreateOrderItemRequest(scenario2.ProductId, 1, [])], null));
        Assert.False(await WaitForAsync(tcs, ShortWait));

        // A new order at establishment1 itself — establishment1's staff must see this one.
        var customer1Http = _factory.CreateClient();
        await customer1Http.AsAuthenticatedAsync(scenario1.CustomerEmail, IntegrationDataSeeder.Password);
        var order1 = await (await customer1Http.PostAsJsonAsync("/api/orders", new CreateOrderRequest(
            scenario1.EstablishmentId, scenario1.CustomerAddressId, "Pix", null,
            [new CreateOrderItemRequest(scenario1.ProductId, 1, [])], null))).Content.ReadFromJsonAsync<OrderResponse>();

        Assert.True(await WaitForAsync(tcs, LongWait));
        Assert.Equal(order1!.Id, tcs.Task.Result.GetProperty("orderId").GetGuid());
    }

    [Fact]
    public async Task Driver_DoesNotReceiveDeliveryAvailable_ForAnotherCity()
    {
        var scenario1 = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory); // city A
        var scenario2 = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory); // city B

        var (driverAEmail, _, _) = await IntegrationDataSeeder.SeedApprovedDriverAsync(_factory, scenario1.CityId);

        var driverAHttp = _factory.CreateClient();
        var driverATokens = await driverAHttp.LoginAsync(driverAEmail, IntegrationDataSeeder.Password);

        await using var driverAHub = await ConnectAsync(driverATokens.AccessToken);
        var tcs = ListenOnce(driverAHub, "DeliveryAvailable");

        // Walk an order in city B (a different city) all the way to READY — driverA must not see it.
        var customer2Http = _factory.CreateClient();
        await customer2Http.AsAuthenticatedAsync(scenario2.CustomerEmail, IntegrationDataSeeder.Password);
        var merchant2Http = _factory.CreateClient();
        await merchant2Http.AsAuthenticatedAsync(scenario2.MerchantEmail, IntegrationDataSeeder.Password);
        var order2 = await (await customer2Http.PostAsJsonAsync("/api/orders", new CreateOrderRequest(
            scenario2.EstablishmentId, scenario2.CustomerAddressId, "Pix", null,
            [new CreateOrderItemRequest(scenario2.ProductId, 1, [])], null))).Content.ReadFromJsonAsync<OrderResponse>();
        await merchant2Http.PostAsync($"/api/merchant/establishments/{scenario2.EstablishmentId}/orders/{order2!.Id}/accept", null);
        await merchant2Http.PostAsync($"/api/merchant/establishments/{scenario2.EstablishmentId}/orders/{order2.Id}/preparing", null);
        await merchant2Http.PostAsync($"/api/merchant/establishments/{scenario2.EstablishmentId}/orders/{order2.Id}/ready", null);
        Assert.False(await WaitForAsync(tcs, ShortWait));

        // Now the same flow in city A (driverA's own city) — driverA must see this one.
        var customer1Http = _factory.CreateClient();
        await customer1Http.AsAuthenticatedAsync(scenario1.CustomerEmail, IntegrationDataSeeder.Password);
        var merchant1Http = _factory.CreateClient();
        await merchant1Http.AsAuthenticatedAsync(scenario1.MerchantEmail, IntegrationDataSeeder.Password);
        var order1 = await (await customer1Http.PostAsJsonAsync("/api/orders", new CreateOrderRequest(
            scenario1.EstablishmentId, scenario1.CustomerAddressId, "Pix", null,
            [new CreateOrderItemRequest(scenario1.ProductId, 1, [])], null))).Content.ReadFromJsonAsync<OrderResponse>();
        await merchant1Http.PostAsync($"/api/merchant/establishments/{scenario1.EstablishmentId}/orders/{order1!.Id}/accept", null);
        await merchant1Http.PostAsync($"/api/merchant/establishments/{scenario1.EstablishmentId}/orders/{order1.Id}/preparing", null);
        await merchant1Http.PostAsync($"/api/merchant/establishments/{scenario1.EstablishmentId}/orders/{order1.Id}/ready", null);

        Assert.True(await WaitForAsync(tcs, LongWait));
    }
}
