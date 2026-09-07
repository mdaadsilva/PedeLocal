using System.Net;
using System.Net.Http.Json;
using PedeLocal.Contracts.Auth;
using PedeLocal.Contracts.Orders;
using PedeLocal.Domain.Establishments;
using PedeLocal.IntegrationTests.Infrastructure;
using Xunit;

namespace PedeLocal.IntegrationTests.Security;

/// <summary>Direct HTTP coverage of docs section 77's QA-de-segurança checklist, run through the
/// real auth/authorization middleware (not just the Application-layer unit test).</summary>
public class AuthorizationTests : IClassFixture<PedeLocalWebApplicationFactory>
{
    private readonly PedeLocalWebApplicationFactory _factory;

    public AuthorizationTests(PedeLocalWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task CustomerCannotAccessAnotherCustomersOrder()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var ownerClient = _factory.CreateClient();
        await ownerClient.AsAuthenticatedAsync(scenario.CustomerEmail, IntegrationDataSeeder.Password);

        var order = await (await ownerClient.PostAsJsonAsync("/api/orders", new CreateOrderRequest(
            scenario.EstablishmentId, scenario.CustomerAddressId, "Pix", null,
            [new CreateOrderItemRequest(scenario.ProductId, 1, [])], null)))
            .Content.ReadFromJsonAsync<OrderResponse>();

        var strangerClient = _factory.CreateClient();
        var strangerEmail = $"stranger-{Guid.NewGuid():N}@example.com";
        await strangerClient.PostAsJsonAsync("/api/auth/register/customer",
            new RegisterCustomerRequest("Estranho", strangerEmail, "14999990000", "Senha123"));
        await strangerClient.AsAuthenticatedAsync(strangerEmail, "Senha123");

        var response = await strangerClient.GetAsync($"/api/orders/{order!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NonStaffUser_CannotManageAnotherEstablishmentsOrders()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);

        var strangerClient = _factory.CreateClient();
        var strangerEmail = $"stranger-{Guid.NewGuid():N}@example.com";
        await strangerClient.PostAsJsonAsync("/api/auth/register/customer",
            new RegisterCustomerRequest("Cliente Qualquer", strangerEmail, "14999990000", "Senha123"));
        await strangerClient.AsAuthenticatedAsync(strangerEmail, "Senha123");

        var response = await strangerClient.GetAsync($"/api/merchant/establishments/{scenario.EstablishmentId}/orders");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CustomerRole_CannotCallDriverOnlyEndpoint()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var client = _factory.CreateClient();
        await client.AsAuthenticatedAsync(scenario.CustomerEmail, IntegrationDataSeeder.Password);

        var response = await client.GetAsync("/api/driver/deliveries/available");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SuspendedEstablishment_RejectsNewOrder_ExistingDataUntouched()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        await IntegrationDataSeeder.SetEstablishmentCommercialStatusAsync(_factory, scenario.EstablishmentId, CommercialStatus.SuspendedPayment);

        var client = _factory.CreateClient();
        await client.AsAuthenticatedAsync(scenario.CustomerEmail, IntegrationDataSeeder.Password);

        var response = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(
            scenario.EstablishmentId, scenario.CustomerAddressId, "Pix", null,
            [new CreateOrderItemRequest(scenario.ProductId, 1, [])], null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // The establishment itself must still exist and be queryable — suspension never deletes data.
        var establishmentResponse = await client.GetAsync($"/api/establishments/{scenario.EstablishmentId}");
        Assert.Equal(HttpStatusCode.NotFound, establishmentResponse.StatusCode); // hidden from customer search, but...
        // ...still fully present for the establishment's own staff:
        var merchantClient = _factory.CreateClient();
        await merchantClient.AsAuthenticatedAsync(scenario.MerchantEmail, IntegrationDataSeeder.Password);
        var productsResponse = await merchantClient.GetAsync($"/api/merchant/establishments/{scenario.EstablishmentId}/products");
        Assert.Equal(HttpStatusCode.OK, productsResponse.StatusCode);
    }
}
