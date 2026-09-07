namespace PedeLocal.Application.Common.Interfaces;

/// <summary>Push notifications over SignalR (docs section 56). Application services call this;
/// the concrete implementation (Infrastructure) fans out to the right hub groups.</summary>
public interface IRealtimeNotifier
{
    Task OrderCreatedAsync(Guid establishmentId, Guid orderId, CancellationToken ct = default);
    Task OrderStatusChangedAsync(Guid customerUserId, Guid establishmentId, Guid orderId, string status, CancellationToken ct = default);
    Task DeliveryAvailableAsync(Guid cityId, Guid deliveryId, CancellationToken ct = default);

    /// <summary>Notifies the establishment (full payload, including <paramref name="driverId"/>) and,
    /// separately, every other driver still online in <paramref name="cityId"/> (deliveryId only — no
    /// driver identity) so their available-deliveries list drops this one. First-accept-wins is still
    /// enforced at the database layer (Delivery.Version); this is purely a UI-freshness signal.</summary>
    Task DeliveryAcceptedAsync(Guid cityId, Guid establishmentId, Guid deliveryId, Guid driverId, CancellationToken ct = default);
    Task DeliveryCompletedAsync(Guid customerUserId, Guid establishmentId, Guid orderId, CancellationToken ct = default);
    Task EstablishmentSuspendedAsync(Guid establishmentId, CancellationToken ct = default);
}
