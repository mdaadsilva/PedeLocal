using System.Net;
using System.Net.Http.Json;
using PedeLocal.Contracts.Orders;
using PedeLocal.IntegrationTests.Infrastructure;
using Xunit;

namespace PedeLocal.IntegrationTests.Security;

/// <summary>Real HTTP coverage that FluentValidation is actually wired into the request pipeline
/// (docs/SECURITY_AUDIT.md) — every *RequestValidator in the Application layer used to be
/// registered in DI but never invoked by anything, so none of these were ever enforced before the
/// global ValidationActionFilter was added.</summary>
public class InputValidationTests : IClassFixture<PedeLocalWebApplicationFactory>
{
    private readonly PedeLocalWebApplicationFactory _factory;

    public InputValidationTests(PedeLocalWebApplicationFactory factory) => _factory = factory;

    private static CreateOrderRequest BuildRequest(
        IntegrationDataSeeder.Scenario scenario, List<CreateOrderItemRequest> items, string? notes = null) =>
        new(scenario.EstablishmentId, scenario.CustomerAddressId, "Pix", null, items, null, "Delivery", notes);

    [Fact]
    public async Task CreateOrder_WithZeroQuantity_ReturnsValidationError()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var client = _factory.CreateClient();
        await client.AsAuthenticatedAsync(scenario.CustomerEmail, IntegrationDataSeeder.Password);

        var response = await client.PostAsJsonAsync("/api/orders",
            BuildRequest(scenario, [new CreateOrderItemRequest(scenario.ProductId, 0, [])]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_WithNegativeQuantity_ReturnsValidationError()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var client = _factory.CreateClient();
        await client.AsAuthenticatedAsync(scenario.CustomerEmail, IntegrationDataSeeder.Password);

        var response = await client.PostAsJsonAsync("/api/orders",
            BuildRequest(scenario, [new CreateOrderItemRequest(scenario.ProductId, -5, [])]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_WithAbsurdQuantity_ReturnsValidationError()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var client = _factory.CreateClient();
        await client.AsAuthenticatedAsync(scenario.CustomerEmail, IntegrationDataSeeder.Password);

        var response = await client.PostAsJsonAsync("/api/orders",
            BuildRequest(scenario, [new CreateOrderItemRequest(scenario.ProductId, 1_000_000, [])]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_WithTooManyDistinctItems_ReturnsValidationError()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var client = _factory.CreateClient();
        await client.AsAuthenticatedAsync(scenario.CustomerEmail, IntegrationDataSeeder.Password);

        var items = Enumerable.Range(0, 101).Select(_ => new CreateOrderItemRequest(scenario.ProductId, 1, [])).ToList();

        var response = await client.PostAsJsonAsync("/api/orders", BuildRequest(scenario, items));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_WithExcessivelyLongNotes_ReturnsValidationError()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var client = _factory.CreateClient();
        await client.AsAuthenticatedAsync(scenario.CustomerEmail, IntegrationDataSeeder.Password);

        var response = await client.PostAsJsonAsync("/api/orders",
            BuildRequest(scenario, [new CreateOrderItemRequest(scenario.ProductId, 1, [])], notes: new string('x', 5000)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_WithNoEstablishmentId_ReturnsValidationError()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var client = _factory.CreateClient();
        await client.AsAuthenticatedAsync(scenario.CustomerEmail, IntegrationDataSeeder.Password);

        var request = new CreateOrderRequest(
            Guid.Empty, scenario.CustomerAddressId, "Pix", null,
            [new CreateOrderItemRequest(scenario.ProductId, 1, [])], null);

        var response = await client.PostAsJsonAsync("/api/orders", request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_PickupWithoutAddress_StillSucceeds_ValidationDoesNotWronglyRequireIt()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var client = _factory.CreateClient();
        await client.AsAuthenticatedAsync(scenario.CustomerEmail, IntegrationDataSeeder.Password);

        var request = new CreateOrderRequest(
            scenario.EstablishmentId, null, "Cash", null,
            [new CreateOrderItemRequest(scenario.ProductId, 1, [])], null, "Pickup");

        var response = await client.PostAsJsonAsync("/api/orders", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task RegisterCustomer_WithWeakPassword_ReturnsValidationError()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register/customer",
            new PedeLocal.Contracts.Auth.RegisterCustomerRequest("Nome", $"{Guid.NewGuid():N}@example.com", "14999990000", "weak"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
