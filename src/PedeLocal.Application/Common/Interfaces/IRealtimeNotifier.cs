namespace PedeLocal.Application.Common.Interfaces;

/// <summary>Push notifications over SignalR (docs section 56). Application services call this;
/// the concrete implementation (Infrastructure) fans out to the right hub groups.</summary>
public interface IRealtimeNotifier
{
    Task OrderCreatedAsync(Guid establishmentId, Guid orderId, CancellationToken ct = default);
    Task OrderStatusChangedAsync(Guid customerUserId, Guid establishmentId, Guid orderId, string status, CancellationToken ct = default);
    Task DeliveryAvailableAsync(Guid cityId, Guid deliveryId, CancellationToken ct = default);
    Task DeliveryAcceptedAsync(Guid establishmentId, Guid deliveryId, Guid driverId, CancellationToken ct = default);
    Task DeliveryCompletedAsync(Guid customerUserId, Guid establishmentId, Guid orderId, CancellationToken ct = default);
    Task EstablishmentSuspendedAsync(Guid establishmentId, CancellationToken ct = default);
}
