using PedeLocal.Application.Common.Interfaces;

namespace PedeLocal.UnitTests.TestSupport;

public class NoOpRealtimeNotifier : IRealtimeNotifier
{
    public Task OrderCreatedAsync(Guid establishmentId, Guid orderId, CancellationToken ct = default) => Task.CompletedTask;
    public Task OrderStatusChangedAsync(Guid customerUserId, Guid establishmentId, Guid orderId, string status, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeliveryAvailableAsync(Guid cityId, Guid deliveryId, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeliveryAcceptedAsync(Guid cityId, Guid establishmentId, Guid deliveryId, Guid driverId, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeliveryCompletedAsync(Guid customerUserId, Guid establishmentId, Guid orderId, CancellationToken ct = default) => Task.CompletedTask;
    public Task EstablishmentSuspendedAsync(Guid establishmentId, CancellationToken ct = default) => Task.CompletedTask;
}
