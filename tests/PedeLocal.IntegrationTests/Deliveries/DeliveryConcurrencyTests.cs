using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PedeLocal.Contracts.Deliveries;
using PedeLocal.Contracts.Orders;
using PedeLocal.IntegrationTests.Infrastructure;
using PedeLocal.Infrastructure.Persistence;
using Xunit;

namespace PedeLocal.IntegrationTests.Deliveries;

/// <summary>The section 40-41 race, exercised through real concurrent HTTP requests against the
/// actual ASP.NET Core pipeline (not just the Application-layer unit test) — two drivers, one
/// delivery, at most one 200.</summary>
public class DeliveryConcurrencyTests : IClassFixture<PedeLocalWebApplicationFactory>
{
    private readonly PedeLocalWebApplicationFactory _factory;

    public DeliveryConcurrencyTests(PedeLocalWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task TwoDrivers_AcceptSameDeliverySimultaneously_OnlyOneSucceeds()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var (driverAEmail, _, _) = await IntegrationDataSeeder.SeedApprovedDriverAsync(_factory, scenario.CityId);
        var (driverBEmail, _, _) = await IntegrationDataSeeder.SeedApprovedDriverAsync(_factory, scenario.CityId);

        var customerClient = _factory.CreateClient();
        await customerClient.AsAuthenticatedAsync(scenario.CustomerEmail, IntegrationDataSeeder.Password);
        var order = await (await customerClient.PostAsJsonAsync("/api/orders", new CreateOrderRequest(
            scenario.EstablishmentId, scenario.CustomerAddressId, "Pix", null,
            [new CreateOrderItemRequest(scenario.ProductId, 1, [])], null)))
            .Content.ReadFromJsonAsync<OrderResponse>();

        var merchantClient = _factory.CreateClient();
        await merchantClient.AsAuthenticatedAsync(scenario.MerchantEmail, IntegrationDataSeeder.Password);
        await merchantClient.PostAsync($"/api/merchant/establishments/{scenario.EstablishmentId}/orders/{order!.Id}/accept", null);
        await merchantClient.PostAsync($"/api/merchant/establishments/{scenario.EstablishmentId}/orders/{order.Id}/preparing", null);
        await merchantClient.PostAsync($"/api/merchant/establishments/{scenario.EstablishmentId}/orders/{order.Id}/ready", null);

        Guid deliveryId;
        using (var scope = _factory.CreateDataScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PedeLocalDbContext>();
            deliveryId = (await db.Deliveries.SingleAsync(d => d.OrderId == order.Id)).Id;
        }

        var clientA = _factory.CreateClient();
        await clientA.AsAuthenticatedAsync(driverAEmail, IntegrationDataSeeder.Password);
        var clientB = _factory.CreateClient();
        await clientB.AsAuthenticatedAsync(driverBEmail, IntegrationDataSeeder.Password);

        var acceptUrl = $"/api/driver/deliveries/{deliveryId}/accept";
        var (responseA, responseB) = await TaskEx.WhenAllPair(
            clientA.PostAsync(acceptUrl, null),
            clientB.PostAsync(acceptUrl, null));

        var statuses = new[] { responseA.StatusCode, responseB.StatusCode };
        Assert.Contains(HttpStatusCode.OK, statuses);
        Assert.Contains(HttpStatusCode.Conflict, statuses);
        Assert.DoesNotContain(statuses, s => s != HttpStatusCode.OK && s != HttpStatusCode.Conflict);

        // Never two winners: exactly one driver ends up assigned.
        using var verifyScope = _factory.CreateDataScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<PedeLocalDbContext>();
        var finalDelivery = await verifyDb.Deliveries.SingleAsync(d => d.Id == deliveryId);
        Assert.NotNull(finalDelivery.DriverId);
        Assert.Equal("Assigned", finalDelivery.Status.ToString());
    }
}

file static class TaskEx
{
    public static async Task<(T1, T2)> WhenAllPair<T1, T2>(Task<T1> t1, Task<T2> t2)
    {
        await Task.WhenAll(t1, t2);
        return (t1.Result, t2.Result);
    }
}
