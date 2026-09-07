using Microsoft.AspNetCore.SignalR;
using PedeLocal.Application.Common.Interfaces;

namespace PedeLocal.Infrastructure.RealTime;

public class SignalRRealtimeNotifier(IHubContext<PedeLocalHub> hub) : IRealtimeNotifier
{
    public Task OrderCreatedAsync(Guid establishmentId, Guid orderId, CancellationToken ct = default) =>
        hub.Clients.Group(HubGroups.Establishment(establishmentId))
            .SendAsync("OrderCreated", new { orderId }, ct);

    public Task OrderStatusChangedAsync(Guid customerUserId, Guid establishmentId, Guid orderId, string status, CancellationToken ct = default) =>
        Task.WhenAll(
            hub.Clients.Group(HubGroups.Customer(customerUserId)).SendAsync("OrderStatusChanged", new { orderId, status }, ct),
            hub.Clients.Group(HubGroups.Establishment(establishmentId)).SendAsync("OrderStatusChanged", new { orderId, status }, ct));

    public Task DeliveryAvailableAsync(Guid cityId, Guid deliveryId, CancellationToken ct = default) =>
        hub.Clients.Group(HubGroups.CityDrivers(cityId)).SendAsync("DeliveryAvailable", new { deliveryId }, ct);

    public Task DeliveryAcceptedAsync(Guid establishmentId, Guid deliveryId, Guid driverId, CancellationToken ct = default) =>
        hub.Clients.Group(HubGroups.Establishment(establishmentId))
            .SendAsync("DeliveryAccepted", new { deliveryId, driverId }, ct);

    public Task DeliveryCompletedAsync(Guid customerUserId, Guid establishmentId, Guid orderId, CancellationToken ct = default) =>
        Task.WhenAll(
            hub.Clients.Group(HubGroups.Customer(customerUserId)).SendAsync("DeliveryCompleted", new { orderId }, ct),
            hub.Clients.Group(HubGroups.Establishment(establishmentId)).SendAsync("DeliveryCompleted", new { orderId }, ct));

    public Task EstablishmentSuspendedAsync(Guid establishmentId, CancellationToken ct = default) =>
        hub.Clients.Group(HubGroups.Establishment(establishmentId)).SendAsync("EstablishmentSuspended", new { establishmentId }, ct);
}
