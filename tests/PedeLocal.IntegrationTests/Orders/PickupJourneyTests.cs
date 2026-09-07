using System.Net;
using System.Net.Http.Json;
using PedeLocal.Contracts.Orders;
using PedeLocal.IntegrationTests.Infrastructure;
using Xunit;

namespace PedeLocal.IntegrationTests.Orders;

/// <summary>PICKUP end-to-end over real HTTP: no delivery address, no delivery fee, no driver leg
/// — the merchant closes the loop directly via `complete-pickup`. See docs/DECISIONS.md #17.</summary>
public class PickupJourneyTests : IClassFixture<PedeLocalWebApplicationFactory>
{
    private readonly PedeLocalWebApplicationFactory _factory;

    public PickupJourneyTests(PedeLocalWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task FullJourney_CreatePickup_AcceptPrepareReady_CompletePickup()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);

        var customerClient = _factory.CreateClient();
        await customerClient.AsAuthenticatedAsync(scenario.CustomerEmail, IntegrationDataSeeder.Password);

        var request = new CreateOrderRequest(
            scenario.EstablishmentId, null, "Cash", null,
            [new CreateOrderItemRequest(scenario.ProductId, 1, [])],
            $"integration-pickup-{Guid.NewGuid():N}", "Pickup");

        var previewResponse = await customerClient.PostAsJsonAsync("/api/orders/preview", request);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<OrderPreviewResponse>();
        Assert.NotNull(preview);
        Assert.Equal("Pickup", preview!.Type);
        Assert.Equal(0m, preview.DeliveryFee);
        Assert.Null(preview.DistanceKm);

        var createResponse = await customerClient.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);
        Assert.Equal("Pickup", order!.Type);
        Assert.Null(order.AddressId);
        Assert.Equal(0m, order.DeliveryFee);
        Assert.Equal(order.Subtotal, order.Total);

        var merchantClient = _factory.CreateClient();
        await merchantClient.AsAuthenticatedAsync(scenario.MerchantEmail, IntegrationDataSeeder.Password);

        await merchantClient.PostAsync($"/api/merchant/establishments/{scenario.EstablishmentId}/orders/{order.Id}/accept", null);
        await merchantClient.PostAsync($"/api/merchant/establishments/{scenario.EstablishmentId}/orders/{order.Id}/preparing", null);

        var readyResponse = await merchantClient.PostAsync(
            $"/api/merchant/establishments/{scenario.EstablishmentId}/orders/{order.Id}/ready", null);
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);
        Assert.Equal("Ready", (await readyResponse.Content.ReadFromJsonAsync<OrderResponse>())!.Status);

        // A pickup order never creates a Delivery — there is no code to retrieve.
        var codeResponse = await customerClient.GetAsync($"/api/orders/{order.Id}/delivery-code");
        Assert.Equal(HttpStatusCode.NotFound, codeResponse.StatusCode);

        var completeResponse = await merchantClient.PostAsync(
            $"/api/merchant/establishments/{scenario.EstablishmentId}/orders/{order.Id}/complete-pickup", null);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        var completed = await completeResponse.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.Equal("PickupCompleted", completed!.Status);

        // Idempotent.
        var completeAgainResponse = await merchantClient.PostAsync(
            $"/api/merchant/establishments/{scenario.EstablishmentId}/orders/{order.Id}/complete-pickup", null);
        Assert.Equal(HttpStatusCode.OK, completeAgainResponse.StatusCode);

        var finalOrderResponse = await customerClient.GetAsync($"/api/orders/{order.Id}");
        var finalOrder = await finalOrderResponse.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.Equal("PickupCompleted", finalOrder!.Status);
    }

    [Fact]
    public async Task Create_Delivery_WithoutAddressId_ReturnsBadRequest()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var customerClient = _factory.CreateClient();
        await customerClient.AsAuthenticatedAsync(scenario.CustomerEmail, IntegrationDataSeeder.Password);

        var request = new CreateOrderRequest(
            scenario.EstablishmentId, null, "Pix", null,
            [new CreateOrderItemRequest(scenario.ProductId, 1, [])], null, "Delivery");

        var response = await customerClient.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompletePickup_OnDeliveryOrder_ReturnsConflict()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var customerClient = _factory.CreateClient();
        await customerClient.AsAuthenticatedAsync(scenario.CustomerEmail, IntegrationDataSeeder.Password);

        var request = new CreateOrderRequest(
            scenario.EstablishmentId, scenario.CustomerAddressId, "Pix", null,
            [new CreateOrderItemRequest(scenario.ProductId, 1, [])], null);
        var createResponse = await customerClient.PostAsJsonAsync("/api/orders", request);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();

        var merchantClient = _factory.CreateClient();
        await merchantClient.AsAuthenticatedAsync(scenario.MerchantEmail, IntegrationDataSeeder.Password);
        await merchantClient.PostAsync($"/api/merchant/establishments/{scenario.EstablishmentId}/orders/{order!.Id}/accept", null);
        await merchantClient.PostAsync($"/api/merchant/establishments/{scenario.EstablishmentId}/orders/{order.Id}/preparing", null);
        await merchantClient.PostAsync($"/api/merchant/establishments/{scenario.EstablishmentId}/orders/{order.Id}/ready", null);

        var response = await merchantClient.PostAsync(
            $"/api/merchant/establishments/{scenario.EstablishmentId}/orders/{order.Id}/complete-pickup", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
