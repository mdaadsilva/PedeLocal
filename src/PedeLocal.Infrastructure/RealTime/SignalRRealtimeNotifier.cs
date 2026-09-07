using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using PedeLocal.Application.Common.Interfaces;

namespace PedeLocal.Infrastructure.RealTime;

/// <summary>Transport for application/domain events (docs section 56) — SignalR is an add-on
/// notification channel, never a source of truth. Every send is best-effort: a broken connection,
/// a slow client, or any other hub failure is logged and swallowed here so it can never fail the
/// request that triggered it (e.g. creating an order must succeed even if the fan-out to
/// establishment staff fails). Clients always reconcile state via REST after reconnecting.</summary>
public class SignalRRealtimeNotifier(IHubContext<PedeLocalHub> hub, ILogger<SignalRRealtimeNotifier> logger) : IRealtimeNotifier
{
    public Task OrderCreatedAsync(Guid establishmentId, Guid orderId, CancellationToken ct = default) =>
        SendAsync(HubGroups.Establishment(establishmentId), "OrderCreated", new { orderId }, ct);

    public Task OrderStatusChangedAsync(Guid customerUserId, Guid establishmentId, Guid orderId, string status, CancellationToken ct = default) =>
        Task.WhenAll(
            SendAsync(HubGroups.Customer(customerUserId), "OrderStatusChanged", new { orderId, status }, ct),
            SendAsync(HubGroups.Establishment(establishmentId), "OrderStatusChanged", new { orderId, status }, ct));

    public Task DeliveryAvailableAsync(Guid cityId, Guid deliveryId, CancellationToken ct = default) =>
        SendAsync(HubGroups.CityDrivers(cityId), "DeliveryAvailable", new { deliveryId }, ct);

    public Task DeliveryAcceptedAsync(Guid cityId, Guid establishmentId, Guid deliveryId, Guid driverId, CancellationToken ct = default) =>
        Task.WhenAll(
            SendAsync(HubGroups.Establishment(establishmentId), "DeliveryAccepted", new { deliveryId, driverId }, ct),
            // Other drivers in the city only need to know the delivery is gone from their list —
            // never who took it (docs' "DRIVER não vinculado não recebe dados privados da entrega").
            SendAsync(HubGroups.CityDrivers(cityId), "DeliveryAccepted", new { deliveryId }, ct));

    public Task DeliveryCompletedAsync(Guid customerUserId, Guid establishmentId, Guid orderId, CancellationToken ct = default) =>
        Task.WhenAll(
            SendAsync(HubGroups.Customer(customerUserId), "DeliveryCompleted", new { orderId }, ct),
            SendAsync(HubGroups.Establishment(establishmentId), "DeliveryCompleted", new { orderId }, ct));

    public Task EstablishmentSuspendedAsync(Guid establishmentId, CancellationToken ct = default) =>
        SendAsync(HubGroups.Establishment(establishmentId), "EstablishmentSuspended", new { establishmentId }, ct);

    private async Task SendAsync(string group, string eventName, object payload, CancellationToken ct)
    {
        try
        {
            await hub.Clients.Group(group).SendAsync(eventName, payload, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Realtime send failed for event {Event} to group {Group} — continuing without it.", eventName, group);
        }
    }
}
