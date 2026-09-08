using PedeLocal.Application.Common.Interfaces;

namespace PedeLocal.UnitTests.TestSupport;

public record CapturedDeliveryAccepted(Guid CityId, Guid EstablishmentId, Guid DeliveryId, Guid DriverId);
public record CapturedDeliveryAvailable(Guid CityId, Guid DeliveryId);

public class CapturingRealtimeNotifier : IRealtimeNotifier
{
    public List<CapturedDeliveryAccepted> DeliveryAcceptedCalls { get; } = [];
    public List<CapturedDeliveryAvailable> DeliveryAvailableCalls { get; } = [];

    public Task OrderCreatedAsync(Guid establishmentId, Guid orderId, CancellationToken ct = default) => Task.CompletedTask;
    public Task OrderStatusChangedAsync(Guid customerUserId, Guid establishmentId, Guid orderId, string status, CancellationToken ct = default) => Task.CompletedTask;

    public Task DeliveryAvailableAsync(Guid cityId, Guid deliveryId, CancellationToken ct = default)
    {
        DeliveryAvailableCalls.Add(new CapturedDeliveryAvailable(cityId, deliveryId));
        return Task.CompletedTask;
    }

    public Task DeliveryAcceptedAsync(Guid cityId, Guid establishmentId, Guid deliveryId, Guid driverId, CancellationToken ct = default)
    {
        DeliveryAcceptedCalls.Add(new CapturedDeliveryAccepted(cityId, establishmentId, deliveryId, driverId));
        return Task.CompletedTask;
    }

    public Task DeliveryCompletedAsync(Guid customerUserId, Guid establishmentId, Guid orderId, CancellationToken ct = default) => Task.CompletedTask;
    public Task EstablishmentSuspendedAsync(Guid establishmentId, CancellationToken ct = default) => Task.CompletedTask;
}
