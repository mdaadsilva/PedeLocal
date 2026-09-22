using PedeLocal.Application.Pricing;
using PedeLocal.Domain.Catalog;
using PedeLocal.Domain.Deliveries;
using PedeLocal.Domain.Drivers;
using PedeLocal.Domain.Finance;
using PedeLocal.Domain.Inventory;
using PedeLocal.Domain.Orders;
using PedeLocal.Infrastructure.Persistence.Seed;

namespace PedeLocal.Infrastructure.Persistence.Seed.Demo;

public static partial class DemoDataPlanner
{
    private static readonly string[] OrderNotes =
    [
        "Sem cebola, por favor.", "Capricha no molho!", "Entregar no portão dos fundos.",
        "Troco conforme informado.", "Tocar a campainha duas vezes.", "Sem gelo, por favor.",
        "Ponto da carne: bem passado.", "Deixar com o porteiro."
    ];

    /// <summary>The lifecycle stages a DELIVERY order walks through, in order. Deliberately has NO
    /// separate "Ready" element: OrderService.MarkReadyAsync records a transient Ready
    /// OrderStatusHistory row (via `save:false`) immediately before overwriting Status with
    /// WaitingDriver in the very same DB call — real Delivery orders are never observably "resting"
    /// at Ready, so this array treats reaching WaitingDriver as the single walkable stage and emits
    /// BOTH history rows together (see the WaitingDriver branch in <see cref="WalkOrderLifecycle"/>).</summary>
    private static readonly OrderStatus[] DeliveryStages =
    [
        OrderStatus.Pending, OrderStatus.Accepted, OrderStatus.Preparing,
        OrderStatus.WaitingDriver, OrderStatus.DriverAssigned, OrderStatus.PickedUp,
        OrderStatus.OutForDelivery, OrderStatus.Delivered
    ];

    private static readonly OrderStatus[] PickupStages =
    [
        OrderStatus.Pending, OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.PickupCompleted
    ];

    private sealed record OrderSlot(EstablishmentPlanInfo Info, DateTime TimestampUtc, bool IsToday, int? ForcedStageIndex);

    private static void PlanOrders(Ctx ctx)
    {
        var groupsByCity = ctx.EstablishmentInfos.GroupBy(e => e.CityIndex);
        foreach (var cityGroup in groupsByCity)
        {
            var slots = new List<OrderSlot>();
            foreach (var info in cityGroup)
            {
                slots.AddRange(BuildOrderSlots(ctx, info));
            }
            slots.Sort((a, b) => a.TimestampUtc.CompareTo(b.TimestampUtc));

            foreach (var slot in slots)
            {
                GenerateOrder(ctx, slot);
            }
        }
    }

    private static List<OrderSlot> BuildOrderSlots(Ctx ctx, EstablishmentPlanInfo info)
    {
        var slots = new List<OrderSlot>();
        var sizeMultiplier = info.Size switch
        {
            DemoFixtureData.EstablishmentSize.Small => 0.6m,
            DemoFixtureData.EstablishmentSize.Medium => 1.0m,
            _ => 1.8m
        };

        var firstDay = ctx.Today.AddDays(-(ctx.Options.OrderHistoryDays - 1));
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(ctx.NowUtc, ctx.LocalZone);

        for (var day = firstDay; day <= ctx.Today; day = day.AddDays(1))
        {
            var dayMultiplier = day.DayOfWeek switch
            {
                DayOfWeek.Friday => 1.4m,
                DayOfWeek.Saturday => 1.6m,
                _ => 1.0m
            };
            var expected = ctx.Options.BaseOrdersPerEstablishmentPerDay * sizeMultiplier * dayMultiplier;
            var factor = 0.7 + ctx.Rng.NextDouble() * 0.6;
            var count = Math.Max(0, (int)Math.Round((double)expected * factor));
            var isToday = day == ctx.Today;

            for (var i = 0; i < count; i++)
            {
                var timestamp = SampleOrderTime(ctx, day, isToday, nowLocal);
                if (timestamp is null)
                {
                    continue; // today, but no valid time slot remains before "now".
                }
                slots.Add(new OrderSlot(info, timestamp.Value, isToday, ForcedStageIndex: null));
            }

            if (info.IsShowcase && isToday)
            {
                slots.AddRange(BuildShowcaseCuratedSlots(ctx, info, day, nowLocal));
            }
        }

        return slots;
    }

    /// <summary>Guarantees the 3 showcase (merchant.<cidade>@pedelocal.dev) establishments show at
    /// least one order at every meaningful stage TODAY, so the merchant dashboard never looks empty
    /// during a demo — on top of (not instead of) the naturally-sampled orders for the day.</summary>
    private static List<OrderSlot> BuildShowcaseCuratedSlots(Ctx ctx, EstablishmentPlanInfo info, DateOnly day, DateTime nowLocal)
    {
        var stages = info.SupportsDelivery ? DeliveryStages : PickupStages;
        var maxStageIndex = stages.Length - 1;
        var result = new List<OrderSlot>();
        var startHour = 9;
        var slotIndex = 0;

        for (var stageIndex = 0; stageIndex <= maxStageIndex; stageIndex++)
        {
            var candidateLocal = day.ToDateTime(new TimeOnly(Math.Min(startHour + slotIndex, 21), (slotIndex * 17) % 60));
            if (candidateLocal >= nowLocal)
            {
                break; // don't schedule a "curated" order in the future.
            }
            var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(candidateLocal, DateTimeKind.Unspecified), ctx.LocalZone);
            result.Add(new OrderSlot(info, utc, IsToday: true, ForcedStageIndex: stageIndex));
            slotIndex++;
        }

        // One deliberately-cancelled order too, if there's still room before "now".
        var cancelLocal = day.ToDateTime(new TimeOnly(Math.Min(startHour + slotIndex, 21), 30));
        if (cancelLocal < nowLocal)
        {
            var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(cancelLocal, DateTimeKind.Unspecified), ctx.LocalZone);
            result.Add(new OrderSlot(info, utc, IsToday: true, ForcedStageIndex: -1));
        }

        return result;
    }

    private static DateTime? SampleOrderTime(Ctx ctx, DateOnly day, bool isToday, DateTime nowLocal)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var (startHour, startMinute, endHour, endMinute) = WeightedPick(ctx.Rng,
                ((8, 0, 11, 30), 2),   // manhã
                ((11, 30, 14, 0), 4),  // almoço (pico)
                ((14, 0, 17, 30), 2),  // tarde
                ((17, 30, 19, 0), 3),  // fim de tarde (pico)
                ((19, 0, 22, 30), 4)); // noite (pico)

            var startMinutesOfDay = startHour * 60 + startMinute;
            var endMinutesOfDay = endHour * 60 + endMinute;
            var minuteOfDay = ctx.Rng.Next(startMinutesOfDay, endMinutesOfDay);
            var candidateLocal = day.ToDateTime(new TimeOnly(minuteOfDay / 60, minuteOfDay % 60));

            if (isToday && candidateLocal >= nowLocal)
            {
                continue; // resample — never schedule a "past" order in the future.
            }

            return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(candidateLocal, DateTimeKind.Unspecified), ctx.LocalZone);
        }

        return null;
    }

    private static void GenerateOrder(Ctx ctx, OrderSlot slot)
    {
        var info = slot.Info;
        var customers = ctx.CustomersByCity[info.CityIndex];
        if (customers.Count == 0)
        {
            return;
        }
        var customer = PickOne(ctx.Rng, customers);

        var orderableCatalog = info.Catalog.Where(p => p.Product.IsAvailable && (!p.IsControlled || p.RemainingStock > 0)).ToList();
        if (orderableCatalog.Count == 0)
        {
            return; // establishment temporarily has nothing sellable — a realistic outcome of stock depletion, not an error.
        }

        var itemCount = Math.Min(orderableCatalog.Count, WeightedPick(ctx.Rng, (1, 35), (2, 35), (3, 20), (4, 10)));
        var chosen = new List<CatalogProductInfo>();
        var pool = new List<CatalogProductInfo>(orderableCatalog);
        for (var i = 0; i < itemCount && pool.Count > 0; i++)
        {
            var pick = PickOne(ctx.Rng, pool);
            chosen.Add(pick);
            pool.Remove(pick);
        }

        // Delivery only if the establishment supports it AND the customer's address is within its
        // configured delivery radius — otherwise Pickup, exactly like BuildQuoteAsync/DeliveryFeeCalculator
        // would really reject an out-of-range Delivery request.
        var wantsDelivery = info.SupportsDelivery && ctx.Rng.Next(100) < 65;
        decimal? distanceKm = null;
        if (wantsDelivery)
        {
            distanceKm = ctx.Haversine.CalculateDistanceKm(info.Address.Latitude, info.Address.Longitude, customer.Address.Latitude, customer.Address.Longitude);
            if (distanceKm > info.Pricing!.DeliveryRadiusKm)
            {
                wantsDelivery = false;
                distanceKm = null;
            }
        }
        var orderType = wantsDelivery ? OrderType.Delivery : OrderType.Pickup;

        decimal subtotal = 0;
        var itemPlans = new List<(Product Product, int Quantity, List<(string Name, decimal Delta)> Options, decimal LineTotal)>();
        foreach (var catalogItem in chosen)
        {
            var quantity = ctx.Rng.Next(1, 4);
            var options = new List<(string, decimal)>();
            var group = ctx.ProductOptionGroups.FirstOrDefault(g => g.ProductId == catalogItem.Product.Id);
            if (group is not null && ctx.Rng.Next(100) < 50)
            {
                var groupOptions = ctx.ProductOptions.Where(o => o.ProductOptionGroupId == group.Id).ToList();
                if (groupOptions.Count > 0)
                {
                    var chosenOption = PickOne(ctx.Rng, groupOptions);
                    options.Add((chosenOption.Name, chosenOption.PriceDelta));
                }
            }

            var optionsTotal = options.Sum(o => o.Item2);
            var lineTotal = (catalogItem.Product.Price + optionsTotal) * quantity;
            subtotal += lineTotal;
            itemPlans.Add((catalogItem.Product, quantity, options, lineTotal));
        }

        decimal deliveryFee = 0;
        string pricingSnapshotJson = "{}";
        if (orderType == OrderType.Delivery)
        {
            var quote = DeliveryFeeCalculator.Calculate(info.Pricing!, distanceKm!.Value);
            deliveryFee = quote.Fee;
            pricingSnapshotJson = quote.PricingRuleSnapshotJson;
        }
        var total = subtotal + deliveryFee;

        var paymentMethod = WeightedPick(ctx.Rng,
            (PaymentMethod.Pix, 40), (PaymentMethod.Cash, 20), (PaymentMethod.DebitOnDelivery, 20), (PaymentMethod.CreditOnDelivery, 20));
        decimal? changeForAmount = null;
        if (paymentMethod == PaymentMethod.Cash && ctx.Rng.Next(100) < 50)
        {
            changeForAmount = total + ctx.Rng.Next(5, 31);
        }
        var notes = ctx.Rng.Next(100) < 20 ? PickOne(ctx.Rng, OrderNotes) : null;

        var orderKey = ctx.NextCounter();
        var order = new Order
        {
            Id = DemoIdFactory.For(ctx.Profile, "order", orderKey),
            CreatedAtUtc = slot.TimestampUtc,
            UpdatedAtUtc = slot.TimestampUtc,
            CustomerUserId = customer.User.Id,
            EstablishmentId = info.Establishment.Id,
            CityId = DemoPlaceholders.City(ctx.Profile, info.CityIndex),
            AddressId = orderType == OrderType.Delivery ? customer.Address.Id : null,
            Status = OrderStatus.Pending,
            Type = orderType,
            PaymentMethod = paymentMethod,
            PaymentStatus = PaymentStatus.Pending,
            ChangeForAmount = changeForAmount,
            Notes = notes,
            Subtotal = subtotal,
            DeliveryFee = deliveryFee,
            Total = total,
            CalculatedDistanceKm = distanceKm,
            PricingRuleSnapshotJson = pricingSnapshotJson,
            IdempotencyKey = null,
            Version = 0
        };
        ctx.Orders.Add(order);

        foreach (var (product, quantity, options, lineTotal) in itemPlans)
        {
            var itemId = DemoIdFactory.For(ctx.Profile, "order-item", $"{orderKey}:{product.Id}");
            ctx.OrderItems.Add(new OrderItem
            {
                Id = itemId,
                OrderId = order.Id,
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = quantity,
                LineTotal = lineTotal
            });
            foreach (var (optName, delta) in options)
            {
                ctx.OrderItemOptions.Add(new OrderItemOption
                {
                    Id = DemoIdFactory.For(ctx.Profile, "order-item-option", $"{itemId}:{optName}"),
                    OrderItemId = itemId,
                    OptionName = optName,
                    PriceDelta = delta
                });
            }
        }

        AddHistory(ctx, order, OrderStatus.Pending, slot.TimestampUtc);

        WalkOrderLifecycle(ctx, slot, order, chosen);
    }

    private static void AddHistory(Ctx ctx, Order order, OrderStatus status, DateTime atUtc) =>
        ctx.OrderStatusHistories.Add(new OrderStatusHistory
        {
            Id = DemoIdFactory.For(ctx.Profile, "order-history", $"{order.Id}:{status}"),
            OrderId = order.Id,
            Status = status,
            ChangedAtUtc = atUtc,
            ChangedByUserId = null
        });

    private static void WalkOrderLifecycle(Ctx ctx, OrderSlot slot, Order order, List<CatalogProductInfo> chosenProducts)
    {
        var info = slot.Info;
        var stages = order.Type == OrderType.Delivery ? DeliveryStages : PickupStages;
        var lastCancellableStageIndex = 3; // WaitingDriver (Delivery) / Ready (Pickup) — both index 3, matches OrderService.MerchantCancellableStatuses.

        var cursorUtc = order.CreatedAtUtc;
        var stockDeducted = false;
        Delivery? delivery = null;

        int targetStageIndex;
        var willCancelAt = -1;

        if (slot.ForcedStageIndex is { } forced)
        {
            if (forced < 0)
            {
                willCancelAt = ctx.Rng.Next(0, lastCancellableStageIndex + 1);
                targetStageIndex = willCancelAt; // walk up to (and including) the cancel point, then cancel.
            }
            else
            {
                targetStageIndex = Math.Min(forced, stages.Length - 1);
            }
        }
        else if (!slot.IsToday)
        {
            // Historical: mostly a full success story, sometimes cancelled early.
            if (ctx.Rng.Next(100) < 12)
            {
                willCancelAt = ctx.Rng.Next(0, lastCancellableStageIndex + 1);
                targetStageIndex = willCancelAt;
            }
            else
            {
                targetStageIndex = stages.Length - 1;
            }
        }
        else
        {
            // Today, natural: walk forward with a continue-chance at each stage, small cancel chance.
            if (ctx.Rng.Next(100) < 8)
            {
                willCancelAt = ctx.Rng.Next(0, lastCancellableStageIndex + 1);
                targetStageIndex = willCancelAt;
            }
            else
            {
                var stop = 0;
                for (var s = 1; s < stages.Length; s++)
                {
                    if (ctx.Rng.Next(100) < 70)
                    {
                        stop = s;
                    }
                    else
                    {
                        break;
                    }
                }
                targetStageIndex = stop;
            }
        }

        for (var stageIndex = 1; stageIndex < stages.Length; stageIndex++)
        {
            if (willCancelAt == stageIndex - 1)
            {
                break; // cancel BEFORE applying this stage — order stays at the previous one, then gets Cancelled below.
            }
            if (stageIndex > targetStageIndex)
            {
                break;
            }

            var status = stages[stageIndex];
            cursorUtc = cursorUtc.AddMinutes(GapMinutesFor(ctx, status));
            if (cursorUtc > ctx.NowUtc)
            {
                cursorUtc = ctx.NowUtc; // never timestamp a step in the future.
            }

            if (status == OrderStatus.Accepted)
            {
                DeductStockForOrder(ctx, info, chosenProducts, order, cursorUtc);
                stockDeducted = true;
            }

            if (status == OrderStatus.WaitingDriver)
            {
                delivery = CreateDeliveryAndConfirmation(ctx, info, order, cursorUtc);
                AddHistory(ctx, order, OrderStatus.Ready, cursorUtc.AddMinutes(-1)); // transient — see DeliveryStages' comment.
                order.Version++; // real MarkReadyAsync bumps Version once for the transient Ready transition too.
            }
            else if (status == OrderStatus.DriverAssigned)
            {
                var driverInfo = PickFreeDriver(ctx, info.CityIndex);
                if (driverInfo is null)
                {
                    break; // stay at WaitingDriver — no driver available right now.
                }
                driverInfo.Busy = true;
                driverInfo.Driver.Status = DriverStatus.Busy;
                delivery!.DriverId = driverInfo.Driver.Id;
                delivery.Status = DeliveryStatus.Assigned;
                delivery.AssignedAtUtc = cursorUtc;
                delivery.Version++;
            }
            else if (status == OrderStatus.PickedUp)
            {
                delivery!.Status = DeliveryStatus.PickedUp;
                delivery.PickedUpAtUtc = cursorUtc;
                delivery.Version++;
            }
            else if (status == OrderStatus.OutForDelivery)
            {
                delivery!.Status = DeliveryStatus.OutForDelivery;
                delivery.OutForDeliveryAtUtc = cursorUtc;
                delivery.Version++;
            }
            else if (status == OrderStatus.Delivered)
            {
                delivery!.Status = DeliveryStatus.Completed;
                delivery.CompletedAtUtc = cursorUtc;
                delivery.Version++;
                CompleteDeliveryConfirmation(ctx, delivery.Id, cursorUtc);
                FreeDriver(ctx, info.CityIndex, delivery.DriverId!.Value);
                ctx.DriverReceivables.Add(new DriverReceivable
                {
                    Id = DemoIdFactory.For(ctx.Profile, "receivable", delivery.Id.ToString()),
                    DeliveryId = delivery.Id,
                    DriverId = delivery.DriverId!.Value,
                    EstablishmentId = info.Establishment.Id,
                    Amount = order.DeliveryFee,
                    Status = ReceivableStatus.Pending
                });
                if (order.PaymentMethod == PaymentMethod.Pix)
                {
                    order.PaymentStatus = PaymentStatus.Paid;
                }
            }
            else if (status == OrderStatus.PickupCompleted)
            {
                if (order.PaymentMethod == PaymentMethod.Pix)
                {
                    order.PaymentStatus = PaymentStatus.Paid;
                }
            }

            AddHistory(ctx, order, status, cursorUtc);
            order.Status = status;
            order.Version++;
        }

        if (willCancelAt >= 0)
        {
            cursorUtc = cursorUtc.AddMinutes(GapMinutesFor(ctx, OrderStatus.Cancelled));
            if (cursorUtc > ctx.NowUtc)
            {
                cursorUtc = ctx.NowUtc;
            }
            if (stockDeducted)
            {
                RestoreStockForOrder(ctx, info, chosenProducts, order, cursorUtc);
            }
            if (delivery is not null)
            {
                delivery.Status = DeliveryStatus.Cancelled;
                delivery.Version++;
                if (delivery.DriverId is { } driverId)
                {
                    FreeDriver(ctx, info.CityIndex, driverId);
                }
            }
            order.Status = OrderStatus.Cancelled;
            order.Version++;
            AddHistory(ctx, order, OrderStatus.Cancelled, cursorUtc);
        }
    }

    private static int GapMinutesFor(Ctx ctx, OrderStatus status) => status switch
    {
        OrderStatus.Accepted => ctx.Rng.Next(2, 7),
        OrderStatus.Preparing => ctx.Rng.Next(3, 11),
        OrderStatus.Ready => ctx.Rng.Next(8, 21),
        OrderStatus.WaitingDriver => ctx.Rng.Next(8, 21), // time to prepare + become ready, folded into this single walkable stage.
        OrderStatus.DriverAssigned => ctx.Rng.Next(1, 9),
        OrderStatus.PickedUp => ctx.Rng.Next(1, 6),
        OrderStatus.OutForDelivery => ctx.Rng.Next(0, 4),
        OrderStatus.Delivered => ctx.Rng.Next(8, 26),
        OrderStatus.PickupCompleted => ctx.Rng.Next(5, 41),
        OrderStatus.Cancelled => ctx.Rng.Next(1, 6),
        _ => 1
    };

    private static void DeductStockForOrder(Ctx ctx, EstablishmentPlanInfo info, List<CatalogProductInfo> chosenProducts, Order order, DateTime atUtc)
    {
        foreach (var item in chosenProducts)
        {
            if (!item.IsControlled)
            {
                continue;
            }
            var orderItem = ctx.OrderItems.First(oi => oi.OrderId == order.Id && oi.ProductId == item.Product.Id);
            var previous = item.RemainingStock ?? 0;
            var updated = Math.Max(0, previous - orderItem.Quantity);
            item.RemainingStock = updated;
            item.Product.StockQuantity = updated;
            item.Product.Version++;

            ctx.InventoryMovements.Add(new InventoryMovement
            {
                Id = DemoIdFactory.For(ctx.Profile, "movement", $"{order.Id}:{item.Product.Id}:sale"),
                ProductId = item.Product.Id,
                Type = InventoryMovementType.Sale,
                Quantity = orderItem.Quantity,
                PreviousQuantity = previous,
                NewQuantity = updated,
                OrderId = order.Id,
                Reason = "Baixa automática por aceite de pedido.",
                CreatedAtUtc = atUtc
            });
        }
        _ = info;
    }

    private static void RestoreStockForOrder(Ctx ctx, EstablishmentPlanInfo info, List<CatalogProductInfo> chosenProducts, Order order, DateTime atUtc)
    {
        foreach (var item in chosenProducts)
        {
            if (!item.IsControlled)
            {
                continue;
            }
            var orderItem = ctx.OrderItems.First(oi => oi.OrderId == order.Id && oi.ProductId == item.Product.Id);
            var previous = item.RemainingStock ?? 0;
            var updated = previous + orderItem.Quantity;
            item.RemainingStock = updated;
            item.Product.StockQuantity = updated;
            item.Product.Version++;

            ctx.InventoryMovements.Add(new InventoryMovement
            {
                Id = DemoIdFactory.For(ctx.Profile, "movement", $"{order.Id}:{item.Product.Id}:restore"),
                ProductId = item.Product.Id,
                Type = InventoryMovementType.CancellationRestore,
                Quantity = orderItem.Quantity,
                PreviousQuantity = previous,
                NewQuantity = updated,
                OrderId = order.Id,
                Reason = "Restauração de estoque por cancelamento de pedido.",
                CreatedAtUtc = atUtc
            });
        }
        _ = info;
    }

    private static Delivery CreateDeliveryAndConfirmation(Ctx ctx, EstablishmentPlanInfo info, Order order, DateTime atUtc)
    {
        var delivery = new Delivery
        {
            Id = DemoIdFactory.For(ctx.Profile, "delivery", order.Id.ToString()),
            CreatedAtUtc = atUtc,
            UpdatedAtUtc = atUtc,
            OrderId = order.Id,
            EstablishmentId = info.Establishment.Id,
            CityId = DemoPlaceholders.City(ctx.Profile, info.CityIndex),
            Status = DeliveryStatus.Available,
            Version = 0
        };
        ctx.Deliveries.Add(delivery);

        var code = ctx.Rng.Next(100000, 1000000).ToString();
        ctx.PendingConfirmations.Add(new PendingDeliveryConfirmation(
            delivery.Id, code, atUtc.AddHours(24), Attempts: 0, MaxAttempts: 5, ConfirmedAtUtc: null));

        return delivery;
    }

    private static void CompleteDeliveryConfirmation(Ctx ctx, Guid deliveryId, DateTime confirmedAtUtc)
    {
        var index = ctx.PendingConfirmations.FindIndex(c => c.DeliveryId == deliveryId);
        if (index >= 0)
        {
            ctx.PendingConfirmations[index] = ctx.PendingConfirmations[index] with { ConfirmedAtUtc = confirmedAtUtc };
        }
    }

    private static DriverInfo? PickFreeDriver(Ctx ctx, int cityIndex)
    {
        var candidates = ctx.DriversByCity[cityIndex]
            .Where(d => !d.Busy && d.Driver.OnboardingStatus == DriverOnboardingStatus.Approved && d.Driver.Status != DriverStatus.Blocked)
            .ToList();
        return candidates.Count == 0 ? null : PickOne(ctx.Rng, candidates);
    }

    private static void FreeDriver(Ctx ctx, int cityIndex, Guid driverId)
    {
        var driverInfo = ctx.DriversByCity[cityIndex].FirstOrDefault(d => d.Driver.Id == driverId);
        if (driverInfo is null)
        {
            return;
        }
        driverInfo.Busy = false;
        driverInfo.Driver.Status = DriverStatus.Available;
    }

    private static void BuildCredentials(Ctx ctx)
    {
        ctx.Credentials.Insert(0, new DemoCredentialRow("SUPER_ADMIN", "Plataforma", DevelopmentDataSeeder.SuperAdminEmail, null));
    }
}
