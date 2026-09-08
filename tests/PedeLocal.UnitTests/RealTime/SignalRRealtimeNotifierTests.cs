using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PedeLocal.Infrastructure.RealTime;
using Xunit;

namespace PedeLocal.UnitTests.RealTime;

public class SignalRRealtimeNotifierTests
{
    [Fact]
    public async Task DeliveryAccepted_SendsFullPayloadToEstablishment_AndDriverlessPayloadToCityDrivers()
    {
        var hub = new FakeHubContext();
        var sut = new SignalRRealtimeNotifier(hub, NullLogger<SignalRRealtimeNotifier>.Instance);

        var cityId = Guid.NewGuid();
        var establishmentId = Guid.NewGuid();
        var deliveryId = Guid.NewGuid();
        var driverId = Guid.NewGuid();

        await sut.DeliveryAcceptedAsync(cityId, establishmentId, deliveryId, driverId, CancellationToken.None);

        Assert.Equal(2, hub.Sent.Count);

        var toEstablishment = Assert.Single(hub.Sent, s => s.Group == HubGroups.Establishment(establishmentId));
        Assert.Equal("DeliveryAccepted", toEstablishment.Event);
        Assert.Contains(driverId.ToString(), JsonSerializer.Serialize(toEstablishment.Payload));

        var toCityDrivers = Assert.Single(hub.Sent, s => s.Group == HubGroups.CityDrivers(cityId));
        Assert.Equal("DeliveryAccepted", toCityDrivers.Event);
        var cityPayloadJson = JsonSerializer.Serialize(toCityDrivers.Payload);
        Assert.Contains(deliveryId.ToString(), cityPayloadJson);
        // Other drivers only learn the delivery is gone, never who took it.
        Assert.DoesNotContain(driverId.ToString(), cityPayloadJson);
    }

    [Fact]
    public async Task OrderStatusChanged_SendsToBothCustomerAndEstablishmentGroups()
    {
        var hub = new FakeHubContext();
        var sut = new SignalRRealtimeNotifier(hub, NullLogger<SignalRRealtimeNotifier>.Instance);
        var customerId = Guid.NewGuid();
        var establishmentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await sut.OrderStatusChangedAsync(customerId, establishmentId, orderId, "Preparing", CancellationToken.None);

        Assert.Equal(2, hub.Sent.Count);
        Assert.Contains(hub.Sent, s => s.Group == HubGroups.Customer(customerId) && s.Event == "OrderStatusChanged");
        Assert.Contains(hub.Sent, s => s.Group == HubGroups.Establishment(establishmentId) && s.Event == "OrderStatusChanged");
    }

    [Fact]
    public async Task HubSendFailure_IsSwallowed_DoesNotThrow_AndOtherGroupStillReceivesIt()
    {
        var establishmentId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        // Simulate the establishment group's connection failing while the customer group works.
        var hub = new FakeHubContext(throwingGroups: [HubGroups.Establishment(establishmentId)]);
        var sut = new SignalRRealtimeNotifier(hub, NullLogger<SignalRRealtimeNotifier>.Instance);

        var exception = await Record.ExceptionAsync(() =>
            sut.OrderStatusChangedAsync(customerId, establishmentId, orderId, "Accepted", CancellationToken.None));

        Assert.Null(exception);
        Assert.Single(hub.Sent); // only the customer group's send actually landed.
        Assert.Equal(HubGroups.Customer(customerId), hub.Sent[0].Group);
    }
}
