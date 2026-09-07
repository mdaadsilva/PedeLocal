using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Application.Deliveries;
using PedeLocal.Application.Identity;
using PedeLocal.Application.Pricing;
using PedeLocal.Contracts.Common;
using PedeLocal.Contracts.Orders;
using PedeLocal.Domain.Deliveries;
using PedeLocal.Domain.Establishments;
using PedeLocal.Domain.Inventory;
using PedeLocal.Domain.Notifications;
using PedeLocal.Domain.Orders;

namespace PedeLocal.Application.Orders;

public class OrderService(
    IAppDbContext db,
    ICurrentUserService currentUser,
    EstablishmentAccessGuard accessGuard,
    IRouteDistanceService routeDistanceService,
    ITokenService tokenService,
    IRealtimeNotifier realtime,
    INotificationService notificationService)
{
    private static readonly OrderStatus[] MerchantCancellableStatuses =
    [
        OrderStatus.Pending, OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.WaitingDriver
    ];

    // ---------------------------------------------------------------
    // Customer-facing
    // ---------------------------------------------------------------

    public async Task<OrderResponse> CreateAsync(CreateOrderRequest request, CancellationToken ct)
    {
        var customerId = currentUser.UserId ?? throw new UnauthorizedAppException("UNAUTHENTICATED", "Usuário não autenticado.");

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await LoadFullOrderQuery()
                .SingleOrDefaultAsync(o => o.CustomerUserId == customerId && o.IdempotencyKey == request.IdempotencyKey, ct);
            if (existing is not null)
            {
                return ToResponse(existing);
            }
        }

        if (request.Items.Count == 0)
        {
            throw new AppException("ORDER_EMPTY", "O pedido precisa ter ao menos um item.");
        }

        if (!Enum.TryParse<PaymentMethod>(request.PaymentMethod, ignoreCase: true, out var paymentMethod))
        {
            throw new AppException("INVALID_PAYMENT_METHOD", "Forma de pagamento inválida.");
        }

        var establishment = await db.Establishments.SingleOrDefaultAsync(e => e.Id == request.EstablishmentId, ct)
            ?? throw new NotFoundException("ESTABLISHMENT_NOT_FOUND", "Estabelecimento não encontrado.");

        var settings = await db.EstablishmentSettings.SingleOrDefaultAsync(s => s.EstablishmentId == establishment.Id, ct);
        if (settings is null || !settings.AcceptsNewOrders)
        {
            throw new ConflictException("ESTABLISHMENT_NOT_ACCEPTING_ORDERS", "Este estabelecimento não está aceitando pedidos no momento.");
        }
        if (establishment.CommercialStatus is CommercialStatus.SuspendedPayment or CommercialStatus.SuspendedAdmin or CommercialStatus.Inactive)
        {
            throw new ConflictException("ESTABLISHMENT_SUSPENDED", "Este estabelecimento está suspenso.");
        }
        if (establishment.OperationalStatus != OperationalStatus.Open)
        {
            throw new ConflictException("ESTABLISHMENT_CLOSED", "Este estabelecimento está fechado no momento.");
        }

        var deliveryAddress = await db.Addresses.SingleOrDefaultAsync(a => a.Id == request.AddressId, ct)
            ?? throw new NotFoundException("ADDRESS_NOT_FOUND", "Endereço não encontrado.");
        if (deliveryAddress.UserId != customerId)
        {
            throw new ForbiddenException("ADDRESS_ACCESS_DENIED", "Você não tem acesso a este endereço.");
        }
        if (deliveryAddress.CityId != establishment.CityId)
        {
            throw new AppException("ADDRESS_CITY_MISMATCH", "O endereço de entrega precisa ser da mesma cidade do estabelecimento.");
        }

        var establishmentAddress = await db.Addresses.SingleOrDefaultAsync(a => a.Id == establishment.AddressId, ct)
            ?? throw new NotFoundException("ADDRESS_NOT_FOUND", "Endereço do estabelecimento não encontrado.");

        var pricingSettings = await db.DeliveryPricingSettings.SingleOrDefaultAsync(s => s.EstablishmentId == establishment.Id, ct)
            ?? throw new ConflictException("DELIVERY_PRICING_NOT_CONFIGURED", "Este estabelecimento ainda não configurou a taxa de entrega.");

        var (orderItems, itemOptions, subtotal) = await BuildOrderItemsAsync(establishment.Id, request.Items, ct);

        var distanceKm = routeDistanceService.CalculateDistanceKm(
            establishmentAddress.Latitude, establishmentAddress.Longitude, deliveryAddress.Latitude, deliveryAddress.Longitude);
        var feeQuote = DeliveryFeeCalculator.Calculate(pricingSettings, deliveryAddress.Type, distanceKm);

        decimal? changeForAmount = null;
        if (paymentMethod == PaymentMethod.Cash && request.ChangeForAmount is > 0)
        {
            if (request.ChangeForAmount < subtotal + feeQuote.Fee)
            {
                throw new AppException("INVALID_CHANGE_AMOUNT", "O valor para troco deve ser maior ou igual ao total do pedido.");
            }
            changeForAmount = request.ChangeForAmount;
        }

        var order = new Order
        {
            CustomerUserId = customerId,
            EstablishmentId = establishment.Id,
            CityId = establishment.CityId,
            AddressId = deliveryAddress.Id,
            Status = OrderStatus.Pending,
            PaymentMethod = paymentMethod,
            PaymentStatus = PaymentStatus.Pending,
            ChangeForAmount = changeForAmount,
            Subtotal = subtotal,
            DeliveryFee = feeQuote.Fee,
            Total = subtotal + feeQuote.Fee,
            CalculatedDistanceKm = feeQuote.DistanceKm,
            PricingRuleSnapshotJson = feeQuote.PricingRuleSnapshotJson,
            IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey
        };
        db.Orders.Add(order);

        foreach (var item in orderItems)
        {
            item.OrderId = order.Id;
            db.OrderItems.Add(item);
        }
        foreach (var option in itemOptions)
        {
            db.OrderItemOptions.Add(option);
        }

        db.OrderStatusHistories.Add(new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.Pending, ChangedByUserId = customerId });

        await db.SaveChangesAsync(ct);
        await realtime.OrderCreatedAsync(establishment.Id, order.Id, ct);
        return await GetForCustomerAsync(order.Id, ct);
    }

    public async Task<PagedResult<OrderResponse>> GetMineAsync(PageRequest request, CancellationToken ct)
    {
        var customerId = currentUser.UserId ?? throw new UnauthorizedAppException("UNAUTHENTICATED", "Usuário não autenticado.");
        return await LoadFullOrderQuery()
            .Where(o => o.CustomerUserId == customerId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Select(o => ToResponse(o))
            .ToPagedResultAsync(request, ct);
    }

    public async Task<OrderResponse> GetForCustomerAsync(Guid orderId, CancellationToken ct)
    {
        var customerId = currentUser.UserId ?? throw new UnauthorizedAppException("UNAUTHENTICATED", "Usuário não autenticado.");
        var order = await LoadFullOrderQuery().SingleOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new NotFoundException("ORDER_NOT_FOUND", "Pedido não encontrado.");

        if (order.CustomerUserId != customerId)
        {
            throw new ForbiddenException("ORDER_ACCESS_DENIED", "Você não tem acesso a este pedido.");
        }

        return ToResponse(order);
    }

    // ---------------------------------------------------------------
    // Merchant-facing
    // ---------------------------------------------------------------

    public async Task<PagedResult<OrderResponse>> GetForMerchantAsync(Guid establishmentId, string? status, PageRequest request, CancellationToken ct)
    {
        await accessGuard.EnsureAccessAsync(establishmentId, ct);

        var query = LoadFullOrderQuery().Where(o => o.EstablishmentId == establishmentId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(o => o.Status == parsedStatus);
        }

        return await query.OrderByDescending(o => o.CreatedAtUtc).Select(o => ToResponse(o)).ToPagedResultAsync(request, ct);
    }

    // ---------------------------------------------------------------
    // Admin-facing (no ownership restriction)
    // ---------------------------------------------------------------

    public async Task<PagedResult<OrderResponse>> GetForAdminAsync(Guid? establishmentId, string? status, PageRequest request, CancellationToken ct)
    {
        var query = LoadFullOrderQuery();
        if (establishmentId is { } id)
        {
            query = query.Where(o => o.EstablishmentId == id);
        }
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(o => o.Status == parsedStatus);
        }

        return await query.OrderByDescending(o => o.CreatedAtUtc).Select(o => ToResponse(o)).ToPagedResultAsync(request, ct);
    }

    public async Task<OrderResponse> GetForMerchantAsync(Guid establishmentId, Guid orderId, CancellationToken ct)
    {
        await accessGuard.EnsureAccessAsync(establishmentId, ct);
        var order = await LoadOwnedMerchantOrderAsync(establishmentId, orderId, ct);
        return ToResponse(order);
    }

    public async Task<OrderResponse> AcceptAsync(Guid establishmentId, Guid orderId, CancellationToken ct)
    {
        await accessGuard.EnsureAccessAsync(establishmentId, ct);
        var order = await LoadOwnedMerchantOrderAsync(establishmentId, orderId, ct);

        if (order.Status == OrderStatus.Accepted)
        {
            return ToResponse(order); // idempotent: already accepted, no duplicate stock deduction.
        }
        if (order.Status != OrderStatus.Pending)
        {
            throw new ConflictException("INVALID_ORDER_TRANSITION", $"Pedido não pode ser aceito a partir do status {order.Status}.");
        }

        await DeductStockAsync(order, ct);
        await TransitionAsync(order, OrderStatus.Accepted, ct);
        await notificationService.NotifyAsync(order.CustomerUserId, NotificationEventType.OrderAccepted,
            "Pedido aceito!", "O estabelecimento aceitou seu pedido e vai começar a preparar.", ct: ct);
        return ToResponse(order);
    }

    public async Task<OrderResponse> MarkPreparingAsync(Guid establishmentId, Guid orderId, CancellationToken ct)
    {
        await accessGuard.EnsureAccessAsync(establishmentId, ct);
        var order = await LoadOwnedMerchantOrderAsync(establishmentId, orderId, ct);

        if (order.Status == OrderStatus.Preparing)
        {
            return ToResponse(order);
        }
        if (order.Status != OrderStatus.Accepted)
        {
            throw new ConflictException("INVALID_ORDER_TRANSITION", $"Pedido não pode ir para preparo a partir do status {order.Status}.");
        }

        await TransitionAsync(order, OrderStatus.Preparing, ct);
        return ToResponse(order);
    }

    public async Task<OrderResponse> MarkReadyAsync(Guid establishmentId, Guid orderId, CancellationToken ct)
    {
        await accessGuard.EnsureAccessAsync(establishmentId, ct);
        var order = await LoadOwnedMerchantOrderAsync(establishmentId, orderId, ct);

        if (order.Status is OrderStatus.Ready or OrderStatus.WaitingDriver)
        {
            return ToResponse(order);
        }
        if (order.Status != OrderStatus.Preparing)
        {
            throw new ConflictException("INVALID_ORDER_TRANSITION", $"Pedido não pode ficar pronto a partir do status {order.Status}.");
        }

        await TransitionAsync(order, OrderStatus.Ready, ct, save: false);

        var deliveryExists = await db.Deliveries.AnyAsync(d => d.OrderId == order.Id, ct);
        Delivery? newDelivery = null;
        if (!deliveryExists)
        {
            newDelivery = new Delivery
            {
                OrderId = order.Id,
                EstablishmentId = order.EstablishmentId,
                CityId = order.CityId,
                Status = DeliveryStatus.Available
            };
            db.Deliveries.Add(newDelivery);

            var code = DeliveryCodeGenerator.GenerateCode();
            db.DeliveryConfirmations.Add(new DeliveryConfirmation
            {
                DeliveryId = newDelivery.Id,
                CodeHash = tokenService.HashToken(code),
                ExpiresAtUtc = DateTime.UtcNow.AddHours(24),
                MaxAttempts = 5
            });
            // The plaintext code itself is deliberately never persisted or logged — only its hash
            // is stored (see DeliveryConfirmation). It cannot be pushed to the customer here yet;
            // this is the one gap in the section-57 event list until a "resend code" endpoint or a
            // dedicated code-delivery seam is added.
        }

        order.Status = OrderStatus.WaitingDriver;
        db.OrderStatusHistories.Add(new OrderStatusHistory { OrderId = order.Id, Status = OrderStatus.WaitingDriver, ChangedByUserId = currentUser.UserId });

        await db.SaveChangesAsync(ct);

        await realtime.OrderStatusChangedAsync(order.CustomerUserId, order.EstablishmentId, order.Id, order.Status.ToString(), ct);
        await notificationService.NotifyAsync(order.CustomerUserId, NotificationEventType.OrderReady,
            "Pedido pronto!", "Seu pedido está pronto e logo sairá para entrega.", ct: ct);
        if (newDelivery is not null)
        {
            await realtime.DeliveryAvailableAsync(order.CityId, newDelivery.Id, ct);
        }

        return ToResponse(order);
    }

    public async Task<OrderResponse> CancelAsync(Guid establishmentId, Guid orderId, CancelOrderRequest request, CancellationToken ct)
    {
        await accessGuard.EnsureAccessAsync(establishmentId, ct);
        var order = await LoadOwnedMerchantOrderAsync(establishmentId, orderId, ct);

        if (order.Status == OrderStatus.Cancelled)
        {
            return ToResponse(order); // idempotent.
        }
        if (!MerchantCancellableStatuses.Contains(order.Status))
        {
            throw new ConflictException("INVALID_ORDER_TRANSITION", $"Pedido não pode ser cancelado a partir do status {order.Status}.");
        }

        if (order.Status != OrderStatus.Pending)
        {
            await RestoreStockAsync(order, ct);
        }

        var delivery = await db.Deliveries.SingleOrDefaultAsync(d => d.OrderId == order.Id, ct);
        if (delivery is not null && delivery.Status != DeliveryStatus.Completed)
        {
            delivery.Status = DeliveryStatus.Cancelled;
            if (delivery.DriverId is { } driverId)
            {
                var driver = await db.Drivers.SingleOrDefaultAsync(d => d.Id == driverId, ct);
                if (driver is not null)
                {
                    driver.Status = Domain.Drivers.DriverStatus.Available;
                }
            }
        }

        order.Status = OrderStatus.Cancelled;
        db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = order.Id, Status = OrderStatus.Cancelled, ChangedByUserId = currentUser.UserId, Notes = request.Reason
        });
        db.AuditLogs.Add(new Domain.Audit.AuditLog
        {
            ActorUserId = currentUser.UserId, Action = Domain.Audit.AuditActions.OrderCancelled,
            EntityType = nameof(Order), EntityId = order.Id, MetadataJson = request.Reason is null ? null : $"{{\"reason\":\"{request.Reason}\"}}"
        });

        await db.SaveChangesAsync(ct);
        await realtime.OrderStatusChangedAsync(order.CustomerUserId, order.EstablishmentId, order.Id, order.Status.ToString(), ct);
        return ToResponse(order);
    }

    // ---------------------------------------------------------------
    // Internals
    // ---------------------------------------------------------------

    private async Task<(List<OrderItem> Items, List<OrderItemOption> Options, decimal Subtotal)> BuildOrderItemsAsync(
        Guid establishmentId, List<CreateOrderItemRequest> requestedItems, CancellationToken ct)
    {
        var productIds = requestedItems.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id) && p.EstablishmentId == establishmentId)
            .Include(p => p.OptionGroups).ThenInclude(g => g.Options)
            .ToDictionaryAsync(p => p.Id, ct);

        var totalQuantityByProduct = requestedItems
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        var orderItems = new List<OrderItem>();
        var orderItemOptions = new List<OrderItemOption>();
        decimal subtotal = 0;

        foreach (var requested in requestedItems)
        {
            if (requested.Quantity <= 0)
            {
                throw new AppException("INVALID_ITEM_QUANTITY", "A quantidade do item deve ser maior que zero.");
            }

            if (!products.TryGetValue(requested.ProductId, out var product))
            {
                throw new NotFoundException("PRODUCT_NOT_FOUND", "Produto não encontrado neste estabelecimento.");
            }
            if (!product.IsAvailable)
            {
                throw new ConflictException("PRODUCT_UNAVAILABLE", $"O produto '{product.Name}' não está disponível.");
            }
            if (product.StockMode == Domain.Catalog.StockMode.Controlled &&
                (product.StockQuantity ?? 0) < totalQuantityByProduct[product.Id])
            {
                throw new ConflictException("INSUFFICIENT_STOCK", $"Estoque insuficiente para '{product.Name}'.");
            }

            var unitPrice = product.PromotionalPrice is > 0 && product.PromotionalPrice < product.Price
                ? product.PromotionalPrice.Value
                : product.Price;

            var item = new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = unitPrice,
                Quantity = requested.Quantity
            };

            var selectedOptionIds = requested.Options.Select(o => o.ProductOptionId).ToList();
            var allProductOptions = product.OptionGroups.SelectMany(g => g.Options).ToDictionary(o => o.Id);

            foreach (var group in product.OptionGroups)
            {
                var selectedInGroup = group.Options.Where(o => selectedOptionIds.Contains(o.Id)).ToList();
                if (selectedInGroup.Count < group.Min || selectedInGroup.Count > group.Max)
                {
                    throw new AppException("INVALID_OPTION_SELECTION",
                        $"Selecione entre {group.Min} e {group.Max} opção(ões) para '{group.Name}'.");
                }
            }

            decimal optionsTotal = 0;
            foreach (var optionId in selectedOptionIds)
            {
                if (!allProductOptions.TryGetValue(optionId, out var option) || !option.IsAvailable)
                {
                    throw new AppException("INVALID_OPTION_SELECTION", "Uma das opções selecionadas é inválida.");
                }

                orderItemOptions.Add(new OrderItemOption { OrderItemId = item.Id, OptionName = option.Name, PriceDelta = option.PriceDelta });
                optionsTotal += option.PriceDelta;
            }

            item.LineTotal = (unitPrice + optionsTotal) * requested.Quantity;
            subtotal += item.LineTotal;
            orderItems.Add(item);
        }

        return (orderItems, orderItemOptions, subtotal);
    }

    private async Task DeductStockAsync(Order order, CancellationToken ct)
    {
        foreach (var item in order.Items)
        {
            var product = await db.Products.SingleOrDefaultAsync(p => p.Id == item.ProductId, ct);
            if (product is null || product.StockMode != Domain.Catalog.StockMode.Controlled)
            {
                continue;
            }

            var previous = product.StockQuantity ?? 0;
            var updated = previous - item.Quantity;
            product.StockQuantity = updated;

            db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = product.Id,
                Type = InventoryMovementType.Sale,
                Quantity = item.Quantity,
                PreviousQuantity = previous,
                NewQuantity = updated,
                OrderId = order.Id,
                Reason = "Baixa automática por aceite de pedido."
            });
        }
    }

    private async Task RestoreStockAsync(Order order, CancellationToken ct)
    {
        foreach (var item in order.Items)
        {
            var product = await db.Products.SingleOrDefaultAsync(p => p.Id == item.ProductId, ct);
            if (product is null || product.StockMode != Domain.Catalog.StockMode.Controlled)
            {
                continue;
            }

            var previous = product.StockQuantity ?? 0;
            var updated = previous + item.Quantity;
            product.StockQuantity = updated;

            db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = product.Id,
                Type = InventoryMovementType.CancellationRestore,
                Quantity = item.Quantity,
                PreviousQuantity = previous,
                NewQuantity = updated,
                OrderId = order.Id,
                Reason = "Restauração de estoque por cancelamento de pedido."
            });
        }
    }

    private async Task TransitionAsync(Order order, OrderStatus newStatus, CancellationToken ct, bool save = true)
    {
        order.Status = newStatus;
        db.OrderStatusHistories.Add(new OrderStatusHistory { OrderId = order.Id, Status = newStatus, ChangedByUserId = currentUser.UserId });
        if (save)
        {
            await db.SaveChangesAsync(ct);
            await realtime.OrderStatusChangedAsync(order.CustomerUserId, order.EstablishmentId, order.Id, newStatus.ToString(), ct);
        }
    }

    private async Task<Order> LoadOwnedMerchantOrderAsync(Guid establishmentId, Guid orderId, CancellationToken ct) =>
        await LoadFullOrderQuery().SingleOrDefaultAsync(o => o.Id == orderId && o.EstablishmentId == establishmentId, ct)
        ?? throw new NotFoundException("ORDER_NOT_FOUND", "Pedido não encontrado.");

    private IQueryable<Order> LoadFullOrderQuery() =>
        db.Orders
            .Include(o => o.Items).ThenInclude(i => i.Options)
            .Include(o => o.StatusHistory);

    private static OrderResponse ToResponse(Order o) => new(
        o.Id, o.CustomerUserId, o.EstablishmentId, o.CityId, o.AddressId, o.Status.ToString(),
        o.PaymentMethod.ToString(), o.PaymentStatus.ToString(), o.ChangeForAmount,
        o.Subtotal, o.DeliveryFee, o.Total, o.CalculatedDistanceKm, o.CreatedAtUtc,
        o.Items.Select(i => new OrderItemResponse(
            i.Id, i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.LineTotal,
            i.Options.Select(op => new OrderItemOptionResponse(op.Id, op.OptionName, op.PriceDelta)).ToList()
        )).ToList(),
        o.StatusHistory.OrderBy(h => h.ChangedAtUtc)
            .Select(h => new OrderStatusHistoryResponse(h.Status.ToString(), h.ChangedAtUtc, h.Notes)).ToList());
}
