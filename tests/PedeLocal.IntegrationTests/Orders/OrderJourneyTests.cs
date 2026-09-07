using System.Net;
using System.Net.Http.Json;
using PedeLocal.Contracts.Deliveries;
using PedeLocal.Contracts.Orders;
using PedeLocal.IntegrationTests.Infrastructure;
using Xunit;

namespace PedeLocal.IntegrationTests.Orders;

/// <summary>End-to-end happy path over real HTTP: customer creates an order, the merchant walks
/// it through accept → preparing → ready, a driver accepts the resulting delivery and completes
/// it with the confirmation code. Exercises the full Program.cs pipeline (auth, DI, middleware,
/// JSON) for every one of these steps — docs section 78's "conceptual E2E" list.</summary>
public class OrderJourneyTests : IClassFixture<PedeLocalWebApplicationFactory>
{
    private readonly PedeLocalWebApplicationFactory _factory;

    public OrderJourneyTests(PedeLocalWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task FullJourney_CreateAcceptPrepareReadyAcceptPickupOutForDeliveryConfirm()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var (driverEmail, _, _) = await IntegrationDataSeeder.SeedApprovedDriverAsync(_factory, scenario.CityId);

        // --- Customer creates the order ---
        var customerClient = _factory.CreateClient();
        await customerClient.AsAuthenticatedAsync(scenario.CustomerEmail, IntegrationDataSeeder.Password);

        var createRequest = new CreateOrderRequest(
            scenario.EstablishmentId, scenario.CustomerAddressId, "Pix", null,
            [new CreateOrderItemRequest(scenario.ProductId, 2, [new CreateOrderItemOptionSelection(scenario.ProductOptionId)])],
            $"integration-{Guid.NewGuid():N}");

        // --- Preview must match what creating the same request actually charges ---
        var previewResponse = await customerClient.PostAsJsonAsync("/api/orders/preview", createRequest);
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        var preview = await previewResponse.Content.ReadFromJsonAsync<OrderPreviewResponse>();
        Assert.NotNull(preview);
        Assert.Equal(48m, preview!.Subtotal); // (20 unit + 4 option) * 2

        var createResponse = await customerClient.PostAsJsonAsync("/api/orders", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);
        Assert.Equal("Pending", order!.Status);
        Assert.Equal(48m, order.Subtotal); // (20 unit + 4 option) * 2
        Assert.Equal(preview.Subtotal, order.Subtotal);
        Assert.Equal(preview.DeliveryFee, order.DeliveryFee);
        Assert.Equal(preview.Total, order.Total);

        // --- Merchant walks the order through accept -> preparing -> ready ---
        var merchantClient = _factory.CreateClient();
        await merchantClient.AsAuthenticatedAsync(scenario.MerchantEmail, IntegrationDataSeeder.Password);

        var acceptResponse = await merchantClient.PostAsync(
            $"/api/merchant/establishments/{scenario.EstablishmentId}/orders/{order.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
        Assert.Equal("Accepted", (await acceptResponse.Content.ReadFromJsonAsync<OrderResponse>())!.Status);

        var preparingResponse = await merchantClient.PostAsync(
            $"/api/merchant/establishments/{scenario.EstablishmentId}/orders/{order.Id}/preparing", null);
        Assert.Equal("Preparing", (await preparingResponse.Content.ReadFromJsonAsync<OrderResponse>())!.Status);

        var readyResponse = await merchantClient.PostAsync(
            $"/api/merchant/establishments/{scenario.EstablishmentId}/orders/{order.Id}/ready", null);
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);
        Assert.Equal("WaitingDriver", (await readyResponse.Content.ReadFromJsonAsync<OrderResponse>())!.Status);

        // --- Only the order's own customer can read the delivery confirmation code ---
        var codeResponse = await customerClient.GetAsync($"/api/orders/{order.Id}/delivery-code");
        Assert.Equal(HttpStatusCode.OK, codeResponse.StatusCode);
        var codePayload = await codeResponse.Content.ReadFromJsonAsync<DeliveryCodeResponse>();
        Assert.NotNull(codePayload);
        Assert.Equal(6, codePayload!.Code.Length);

        var otherCustomerClient = _factory.CreateClient();
        var otherScenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        await otherCustomerClient.AsAuthenticatedAsync(otherScenario.CustomerEmail, IntegrationDataSeeder.Password);
        var forbiddenCodeResponse = await otherCustomerClient.GetAsync($"/api/orders/{order.Id}/delivery-code");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCodeResponse.StatusCode);

        // --- Driver sees the delivery, accepts, picks up, heads out ---
        var driverClient = _factory.CreateClient();
        await driverClient.AsAuthenticatedAsync(driverEmail, IntegrationDataSeeder.Password);

        var availableResponse = await driverClient.GetAsync("/api/driver/deliveries/available");
        var available = await availableResponse.Content.ReadFromJsonAsync<List<DeliveryResponse>>();
        var delivery = Assert.Single(available!.Where(d => d.OrderId == order.Id));

        var driverAcceptResponse = await driverClient.PostAsync($"/api/driver/deliveries/{delivery.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, driverAcceptResponse.StatusCode);
        Assert.Equal("Assigned", (await driverAcceptResponse.Content.ReadFromJsonAsync<DeliveryResponse>())!.Status);

        // --- Only the driver assigned to this delivery can see its details ---
        var detailsResponse = await driverClient.GetAsync($"/api/driver/deliveries/{delivery.Id}/details");
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        var details = await detailsResponse.Content.ReadFromJsonAsync<DeliveryDetailResponse>();
        Assert.NotNull(details);
        Assert.Equal(order.Id, details!.Order.Id);
        Assert.Single(details.Order.Items);
        Assert.Equal("Pix", details.Order.PaymentMethod);
        Assert.NotNull(details.DeliveryAddress.Street);

        var (otherDriverEmail, _, _) = await IntegrationDataSeeder.SeedApprovedDriverAsync(_factory, scenario.CityId);
        var otherDriverClient = _factory.CreateClient();
        await otherDriverClient.AsAuthenticatedAsync(otherDriverEmail, IntegrationDataSeeder.Password);
        var forbiddenDetailsResponse = await otherDriverClient.GetAsync($"/api/driver/deliveries/{delivery.Id}/details");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenDetailsResponse.StatusCode);

        var pickupResponse = await driverClient.PostAsync($"/api/driver/deliveries/{delivery.Id}/pickup", null);
        Assert.Equal("PickedUp", (await pickupResponse.Content.ReadFromJsonAsync<DeliveryResponse>())!.Status);

        var outForDeliveryResponse = await driverClient.PostAsync($"/api/driver/deliveries/{delivery.Id}/out-for-delivery", null);
        Assert.Equal("OutForDelivery", (await outForDeliveryResponse.Content.ReadFromJsonAsync<DeliveryResponse>())!.Status);

        var wrongCodeResponse = await driverClient.PostAsJsonAsync(
            $"/api/driver/deliveries/{delivery.Id}/confirm", new ConfirmDeliveryRequest("000000"));
        Assert.Equal(HttpStatusCode.BadRequest, wrongCodeResponse.StatusCode);

        // The driver confirms with the exact code the customer retrieved above — proves the
        // customer-facing decrypted copy and the hash the driver is checked against agree.
        var confirmResponse = await driverClient.PostAsJsonAsync(
            $"/api/driver/deliveries/{delivery.Id}/confirm", new ConfirmDeliveryRequest(codePayload.Code));
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        Assert.Equal("Completed", (await confirmResponse.Content.ReadFromJsonAsync<DeliveryResponse>())!.Status);

        // --- Once confirmed, the code is no longer retrievable ---
        var usedCodeResponse = await customerClient.GetAsync($"/api/orders/{order.Id}/delivery-code");
        Assert.Equal(HttpStatusCode.Conflict, usedCodeResponse.StatusCode);

        // --- Final state, verified back through the customer's own view of the order ---
        var finalOrderResponse = await customerClient.GetAsync($"/api/orders/{order.Id}");
        var finalOrder = await finalOrderResponse.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.Equal("Delivered", finalOrder!.Status);
    }
}
