using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Application.Identity;
using PedeLocal.Contracts.Addresses;
using PedeLocal.Contracts.Common;
using PedeLocal.Contracts.Deliveries;
using PedeLocal.Contracts.Orders;
using PedeLocal.Domain.Addresses;
using PedeLocal.Domain.Deliveries;
using PedeLocal.Domain.Drivers;
using PedeLocal.Domain.Finance;
using PedeLocal.Domain.Notifications;
using PedeLocal.Domain.Orders;

namespace PedeLocal.Application.Deliveries;

public class DeliveryService(
    IAppDbContext db, ICurrentUserService currentUser, ITokenService tokenService,
    IRealtimeNotifier realtime, INotificationService notificationService)
{
    public async Task<List<DeliveryResponse>> GetAvailableAsync(CancellationToken ct)
    {
        var driver = await GetOwnDriverAsync(ct);

        return await db.Deliveries
            .Where(d => d.Status == DeliveryStatus.Available && d.CityId == driver.CityId)
            .OrderBy(d => d.CreatedAtUtc)
            .Select(d => ToResponse(d))
            .ToListAsync(ct);
    }

    public async Task<PagedResult<DeliveryResponse>> GetForAdminAsync(Guid? cityId, string? status, PageRequest request, CancellationToken ct)
    {
        var query = db.Deliveries.AsQueryable();
        if (cityId is { } id)
        {
            query = query.Where(d => d.CityId == id);
        }
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<DeliveryStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(d => d.Status == parsedStatus);
        }

        return await query.OrderByDescending(d => d.CreatedAtUtc).Select(d => ToResponse(d)).ToPagedResultAsync(request, ct);
    }

    public async Task<DeliveryResponse?> GetCurrentAsync(CancellationToken ct)
    {
        var driver = await GetOwnDriverAsync(ct);

        var delivery = await db.Deliveries.SingleOrDefaultAsync(d =>
            d.DriverId == driver.Id &&
            (d.Status == DeliveryStatus.Assigned || d.Status == DeliveryStatus.PickedUp || d.Status == DeliveryStatus.OutForDelivery),
            ct);

        return delivery is null ? null : ToResponse(delivery);
    }

    /// <summary>Accepts a delivery. Guards the concurrent-accept race (docs section 40-41) with a
    /// fast pre-check plus the entity's Version optimistic concurrency token: if another driver's
    /// SaveChanges committed first, ours fails with DbUpdateConcurrencyException here, which we
    /// translate into a normal 409 instead of letting two drivers both "win".</summary>
    public async Task<DeliveryResponse> AcceptAsync(Guid deliveryId, CancellationToken ct)
    {
        var driver = await GetOwnDriverAsync(ct);
        EnsureDriverCanWork(driver);
        if (driver.Status != DriverStatus.Available)
        {
            throw new ConflictException("DRIVER_ALREADY_ON_DELIVERY", "Você já está em uma entrega.");
        }

        var delivery = await db.Deliveries.SingleOrDefaultAsync(d => d.Id == deliveryId, ct)
            ?? throw new NotFoundException("DELIVERY_NOT_FOUND", "Entrega não encontrada.");

        if (delivery.DriverId is not null || delivery.Status != DeliveryStatus.Available)
        {
            throw new ConflictException("DELIVERY_ALREADY_TAKEN", "Esta entrega já foi aceita por outro entregador.");
        }

        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == delivery.OrderId, ct)
            ?? throw new NotFoundException("ORDER_NOT_FOUND", "Pedido da entrega não encontrado.");

        delivery.DriverId = driver.Id;
        delivery.Status = DeliveryStatus.Assigned;
        delivery.AssignedAtUtc = DateTime.UtcNow;
        delivery.Version++;
        driver.Status = DriverStatus.Busy;

        order.Status = OrderStatus.DriverAssigned;
        db.OrderStatusHistories.Add(new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.DriverAssigned, ChangedByUserId = currentUser.UserId });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("DELIVERY_ALREADY_TAKEN", "Esta entrega já foi aceita por outro entregador.");
        }

        await realtime.DeliveryAcceptedAsync(delivery.CityId, order.EstablishmentId, delivery.Id, driver.Id, ct);
        await realtime.OrderStatusChangedAsync(order.CustomerUserId, order.EstablishmentId, order.Id, order.Status.ToString(), ct);
        await notificationService.NotifyAsync(order.CustomerUserId, NotificationEventType.DeliveryAccepted,
            "Entregador a caminho!", "Um entregador aceitou seu pedido e vai retirá-lo em breve.",
            EventData(order.Id, delivery.Id, NotificationEventType.DeliveryAccepted), ct);

        return ToResponse(delivery);
    }

    public async Task<DeliveryResponse> PickupAsync(Guid deliveryId, CancellationToken ct)
    {
        var (delivery, order) = await LoadOwnedActiveDeliveryAsync(deliveryId, ct);

        if (delivery.Status == DeliveryStatus.PickedUp)
        {
            return ToResponse(delivery);
        }
        if (delivery.Status != DeliveryStatus.Assigned)
        {
            throw new ConflictException("INVALID_DELIVERY_TRANSITION", $"Entrega não pode ser retirada a partir do status {delivery.Status}.");
        }

        delivery.Status = DeliveryStatus.PickedUp;
        delivery.PickedUpAtUtc = DateTime.UtcNow;
        order.Status = OrderStatus.PickedUp;
        order.Version++;
        db.OrderStatusHistories.Add(new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.PickedUp, ChangedByUserId = currentUser.UserId });

        var (applied, currentDelivery, currentOrder) = await SaveDeliveryTransitionAsync(delivery, order, OrderStatus.PickedUp, ct);
        if (!applied)
        {
            return ToResponse(currentDelivery);
        }

        await realtime.OrderStatusChangedAsync(currentOrder.CustomerUserId, currentOrder.EstablishmentId, currentOrder.Id, currentOrder.Status.ToString(), ct);
        return ToResponse(currentDelivery);
    }

    public async Task<DeliveryResponse> OutForDeliveryAsync(Guid deliveryId, CancellationToken ct)
    {
        var (delivery, order) = await LoadOwnedActiveDeliveryAsync(deliveryId, ct);

        if (delivery.Status == DeliveryStatus.OutForDelivery)
        {
            return ToResponse(delivery);
        }
        if (delivery.Status != DeliveryStatus.PickedUp)
        {
            throw new ConflictException("INVALID_DELIVERY_TRANSITION", $"Entrega não pode sair para entrega a partir do status {delivery.Status}.");
        }

        delivery.Status = DeliveryStatus.OutForDelivery;
        delivery.OutForDeliveryAtUtc = DateTime.UtcNow;
        order.Status = OrderStatus.OutForDelivery;
        order.Version++;
        db.OrderStatusHistories.Add(new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.OutForDelivery, ChangedByUserId = currentUser.UserId });

        var (applied, currentDelivery, currentOrder) = await SaveDeliveryTransitionAsync(delivery, order, OrderStatus.OutForDelivery, ct);
        if (!applied)
        {
            return ToResponse(currentDelivery);
        }

        await realtime.OrderStatusChangedAsync(currentOrder.CustomerUserId, currentOrder.EstablishmentId, currentOrder.Id, currentOrder.Status.ToString(), ct);
        await notificationService.NotifyAsync(currentOrder.CustomerUserId, NotificationEventType.OrderOutForDelivery,
            "Pedido saiu para entrega!", "Seu pedido saiu para entrega e logo chega até você.",
            EventData(currentOrder.Id, delivery.Id, NotificationEventType.OrderOutForDelivery), ct);
        return ToResponse(currentDelivery);
    }

    public async Task<DeliveryResponse> ConfirmAsync(Guid deliveryId, ConfirmDeliveryRequest request, CancellationToken ct)
    {
        var (delivery, order) = await LoadOwnedActiveDeliveryAsync(deliveryId, ct);

        if (delivery.Status == DeliveryStatus.Completed)
        {
            return ToResponse(delivery); // idempotent.
        }
        if (delivery.Status != DeliveryStatus.OutForDelivery)
        {
            throw new ConflictException("INVALID_DELIVERY_TRANSITION", "A entrega precisa estar 'a caminho' para ser confirmada.");
        }

        var confirmation = await db.DeliveryConfirmations.SingleOrDefaultAsync(c => c.DeliveryId == deliveryId, ct)
            ?? throw new NotFoundException("DELIVERY_CODE_NOT_FOUND", "Código de confirmação não encontrado.");

        if (confirmation.ExpiresAtUtc < DateTime.UtcNow)
        {
            throw new ConflictException("DELIVERY_CODE_EXPIRED", "O código de confirmação expirou.");
        }
        if (confirmation.Attempts >= confirmation.MaxAttempts)
        {
            throw new ConflictException("DELIVERY_CODE_LOCKED", "Número máximo de tentativas excedido.");
        }

        if (tokenService.HashToken(request.Code) != confirmation.CodeHash)
        {
            confirmation.Attempts++;
            await db.SaveChangesAsync(ct);
            throw new AppException("INVALID_DELIVERY_CODE", "Código de confirmação inválido.");
        }

        confirmation.ConfirmedAtUtc = DateTime.UtcNow;
        delivery.Status = DeliveryStatus.Completed;
        delivery.CompletedAtUtc = DateTime.UtcNow;

        var driver = await db.Drivers.SingleAsync(d => d.Id == delivery.DriverId!.Value, ct);
        driver.Status = DriverStatus.Available;

        order.Status = OrderStatus.Delivered;
        order.Version++;
        order.PaymentStatus = order.PaymentMethod == Domain.Orders.PaymentMethod.Pix ? PaymentStatus.Paid : order.PaymentStatus;
        db.OrderStatusHistories.Add(new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.Delivered, ChangedByUserId = currentUser.UserId });

        var receivableExists = await db.DriverReceivables.AnyAsync(r => r.DeliveryId == delivery.Id, ct);
        if (!receivableExists)
        {
            db.DriverReceivables.Add(new DriverReceivable
            {
                DeliveryId = delivery.Id,
                DriverId = driver.Id,
                EstablishmentId = delivery.EstablishmentId,
                Amount = order.DeliveryFee,
                Status = ReceivableStatus.Pending
            });
        }

        var (applied, currentDelivery, currentOrder) = await SaveDeliveryTransitionAsync(delivery, order, OrderStatus.Delivered, ct);
        if (!applied)
        {
            return ToResponse(currentDelivery);
        }

        await realtime.OrderStatusChangedAsync(currentOrder.CustomerUserId, currentOrder.EstablishmentId, currentOrder.Id, currentOrder.Status.ToString(), ct);
        await realtime.DeliveryCompletedAsync(currentOrder.CustomerUserId, currentOrder.EstablishmentId, currentOrder.Id, ct);
        await notificationService.NotifyAsync(currentOrder.CustomerUserId, NotificationEventType.OrderDelivered,
            "Pedido entregue!", "Seu pedido foi entregue. Bom apetite!",
            EventData(currentOrder.Id, delivery.Id, NotificationEventType.OrderDelivered), ct);
        return ToResponse(currentDelivery);
    }

    /// <summary>Everything the assigned driver needs to execute the delivery — order items, payment
    /// method/fee, establishment and delivery address (with lat/lng). Reuses the same ownership
    /// check as every other driver action (delivery.DriverId == this driver), so a driver with no
    /// link to the delivery gets the same 403 DELIVERY_ACCESS_DENIED as pickup/confirm/etc.</summary>
    public async Task<DeliveryDetailResponse> GetDetailsAsync(Guid deliveryId, CancellationToken ct)
    {
        var (delivery, order) = await LoadOwnedActiveDeliveryAsync(deliveryId, ct);

        var fullOrder = await db.Orders
            .Include(o => o.Items).ThenInclude(i => i.Options)
            .SingleAsync(o => o.Id == order.Id, ct);

        var establishment = await db.Establishments.SingleAsync(e => e.Id == delivery.EstablishmentId, ct);
        var establishmentAddress = await db.Addresses.SingleAsync(a => a.Id == establishment.AddressId, ct);
        // Deliveries only ever exist for OrderType.Delivery orders (MarkReadyAsync skips Delivery
        // creation for Pickup), so AddressId is guaranteed set here.
        var deliveryAddress = await db.Addresses.SingleAsync(a => a.Id == fullOrder.AddressId!.Value, ct);

        return new DeliveryDetailResponse(
            delivery.Id,
            delivery.Status.ToString(),
            new DeliveryDetailOrderResponse(
                fullOrder.Id, fullOrder.PaymentMethod.ToString(), fullOrder.PaymentStatus.ToString(), fullOrder.ChangeForAmount,
                fullOrder.Subtotal, fullOrder.DeliveryFee, fullOrder.Total, fullOrder.Notes,
                fullOrder.Items.Select(i => new OrderItemResponse(
                    i.Id, i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.LineTotal,
                    i.Options.Select(o => new OrderItemOptionResponse(o.Id, o.OptionName, o.PriceDelta)).ToList()
                )).ToList()),
            new DeliveryDetailEstablishmentResponse(establishment.Id, establishment.Name, establishment.Phone, ToAddressResponse(establishmentAddress)),
            ToAddressResponse(deliveryAddress));
    }

    /// <summary>Minimal push/notification data payload for delivery-driven events — just the order
    /// and delivery ids and the event type name, never anything the customer wouldn't already see by
    /// opening the app (docs/PUSH_NOTIFICATIONS.md).</summary>
    private static Dictionary<string, string> EventData(Guid orderId, Guid deliveryId, NotificationEventType type) => new()
    {
        ["orderId"] = orderId.ToString(),
        ["deliveryId"] = deliveryId.ToString(),
        ["eventType"] = type.ToString()
    };

    private static AddressResponse ToAddressResponse(Address a) => new(
        a.Id, a.CityId, a.Type.ToString(), a.Label, a.Street, a.Number, a.District, a.Complement,
        a.Reference, a.ZipCode, a.PropertyName, a.Road, a.Kilometer, a.Latitude, a.Longitude, a.GooglePlaceId);

    private async Task<(Delivery Delivery, Order Order)> LoadOwnedActiveDeliveryAsync(Guid deliveryId, CancellationToken ct)
    {
        var driver = await GetOwnDriverAsync(ct);
        var delivery = await db.Deliveries.SingleOrDefaultAsync(d => d.Id == deliveryId, ct)
            ?? throw new NotFoundException("DELIVERY_NOT_FOUND", "Entrega não encontrada.");

        if (delivery.DriverId != driver.Id)
        {
            throw new ForbiddenException("DELIVERY_ACCESS_DENIED", "Você não é o entregador responsável por esta entrega.");
        }

        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == delivery.OrderId, ct)
            ?? throw new NotFoundException("ORDER_NOT_FOUND", "Pedido da entrega não encontrado.");

        return (delivery, order);
    }

    private async Task<Domain.Drivers.Driver> GetOwnDriverAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAppException("UNAUTHENTICATED", "Usuário não autenticado.");
        return await db.Drivers.SingleOrDefaultAsync(d => d.UserId == userId, ct)
            ?? throw new NotFoundException("DRIVER_PROFILE_NOT_FOUND", "Perfil de entregador não encontrado.");
    }

    private static void EnsureDriverCanWork(Domain.Drivers.Driver driver)
    {
        if (driver.ApprovalStatus != DriverApprovalStatus.Approved)
        {
            throw new ForbiddenException("DRIVER_NOT_APPROVED", "Seu cadastro ainda não foi aprovado.");
        }
        if (driver.Status == DriverStatus.Blocked)
        {
            throw new ForbiddenException("DRIVER_BLOCKED", "Seu acesso está bloqueado.");
        }
    }

    /// <summary>Attempts to persist a delivery-driven order transition (Pickup/OutForDelivery/Confirm),
    /// each of which mutates both Delivery and Order together. Order.Version guards the race where
    /// the same driver double-taps (or retries after a client timeout): the loser's SaveChanges fails
    /// with DbUpdateConcurrencyException. We discard the failed attempt's pending changes, re-read the
    /// real current Delivery/Order state, and — mirroring OrderService.SaveTransitionAsync's
    /// idempotency rule — treat "Order already in the expected status" as a successful no-op
    /// (Applied=false: the concurrent winner already sent realtime/notification/receivable side
    /// effects, so the caller must not repeat them) rather than surfacing an error.</summary>
    private async Task<(bool Applied, Delivery Delivery, Order Order)> SaveDeliveryTransitionAsync(
        Delivery delivery, Order order, OrderStatus expectedOrderStatus, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return (true, delivery, order);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.DiscardPendingChanges();
            var currentOrder = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == order.Id, ct)
                ?? throw new NotFoundException("ORDER_NOT_FOUND", "Pedido da entrega não encontrado.");
            var currentDelivery = await db.Deliveries.AsNoTracking().SingleOrDefaultAsync(d => d.Id == delivery.Id, ct)
                ?? throw new NotFoundException("DELIVERY_NOT_FOUND", "Entrega não encontrada.");

            if (currentOrder.Status == expectedOrderStatus)
            {
                return (false, currentDelivery, currentOrder);
            }

            throw new ConflictException("INVALID_DELIVERY_TRANSITION",
                $"Pedido não pode transicionar a partir do status {currentOrder.Status}.");
        }
    }

    private static DeliveryResponse ToResponse(Delivery d) => new(
        d.Id, d.OrderId, d.EstablishmentId, d.CityId, d.DriverId, d.Status.ToString(),
        d.AssignedAtUtc, d.PickedUpAtUtc, d.OutForDeliveryAtUtc, d.CompletedAtUtc);
}
