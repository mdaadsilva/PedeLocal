using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PedeLocal.Application.Identity;
using PedeLocal.Contracts.Deliveries;
using PedeLocal.Contracts.Orders;
using PedeLocal.IntegrationTests.Infrastructure;
using PedeLocal.Infrastructure.Persistence;
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

        var createResponse = await customerClient.PostAsJsonAsync("/api/orders", new CreateOrderRequest(
            scenario.EstablishmentId, scenario.CustomerAddressId, "Pix", null,
            [new CreateOrderItemRequest(scenario.ProductId, 2, [new CreateOrderItemOptionSelection(scenario.ProductOptionId)])],
            $"integration-{Guid.NewGuid():N}"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var order = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);
        Assert.Equal("Pending", order!.Status);
        Assert.Equal(48m, order.Subtotal); // (20 unit + 4 option) * 2

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

        // --- Driver sees the delivery, accepts, picks up, heads out ---
        var driverClient = _factory.CreateClient();
        await driverClient.AsAuthenticatedAsync(driverEmail, IntegrationDataSeeder.Password);

        var availableResponse = await driverClient.GetAsync("/api/driver/deliveries/available");
        var available = await availableResponse.Content.ReadFromJsonAsync<List<DeliveryResponse>>();
        var delivery = Assert.Single(available!.Where(d => d.OrderId == order.Id));

        var driverAcceptResponse = await driverClient.PostAsync($"/api/driver/deliveries/{delivery.Id}/accept", null);
        Assert.Equal(HttpStatusCode.OK, driverAcceptResponse.StatusCode);
        Assert.Equal("Assigned", (await driverAcceptResponse.Content.ReadFromJsonAsync<DeliveryResponse>())!.Status);

        var pickupResponse = await driverClient.PostAsync($"/api/driver/deliveries/{delivery.Id}/pickup", null);
        Assert.Equal("PickedUp", (await pickupResponse.Content.ReadFromJsonAsync<DeliveryResponse>())!.Status);

        var outForDeliveryResponse = await driverClient.PostAsync($"/api/driver/deliveries/{delivery.Id}/out-for-delivery", null);
        Assert.Equal("OutForDelivery", (await outForDeliveryResponse.Content.ReadFromJsonAsync<DeliveryResponse>())!.Status);

        // --- Confirmation code: only its hash is ever exposed via the API, so for the test we
        // overwrite it to a known value directly in the store (same technique as the unit tests). ---
        const string knownCode = "654321";
        using (var scope = _factory.CreateDataScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PedeLocalDbContext>();
            var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
            var confirmation = await db.DeliveryConfirmations.SingleAsync(c => c.DeliveryId == delivery.Id);
            confirmation.CodeHash = tokenService.HashToken(knownCode);
            await db.SaveChangesAsync();
        }

        var wrongCodeResponse = await driverClient.PostAsJsonAsync(
            $"/api/driver/deliveries/{delivery.Id}/confirm", new ConfirmDeliveryRequest("000000"));
        Assert.Equal(HttpStatusCode.BadRequest, wrongCodeResponse.StatusCode);

        var confirmResponse = await driverClient.PostAsJsonAsync(
            $"/api/driver/deliveries/{delivery.Id}/confirm", new ConfirmDeliveryRequest(knownCode));
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        Assert.Equal("Completed", (await confirmResponse.Content.ReadFromJsonAsync<DeliveryResponse>())!.Status);

        // --- Final state, verified back through the customer's own view of the order ---
        var finalOrderResponse = await customerClient.GetAsync($"/api/orders/{order.Id}");
        var finalOrder = await finalOrderResponse.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.Equal("Delivered", finalOrder!.Status);
    }
}
