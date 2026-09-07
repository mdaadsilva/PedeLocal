namespace PedeLocal.Contracts.Orders;

public record CreateOrderItemOptionSelection(Guid ProductOptionId);

public record CreateOrderItemRequest(Guid ProductId, int Quantity, List<CreateOrderItemOptionSelection> Options);

public record CreateOrderRequest(
    Guid EstablishmentId,
    Guid? AddressId,
    string PaymentMethod,
    decimal? ChangeForAmount,
    List<CreateOrderItemRequest> Items,
    string? IdempotencyKey,
    string OrderType = "Delivery",
    string? Notes = null);

public record OrderItemOptionResponse(Guid Id, string OptionName, decimal PriceDelta);

public record OrderItemResponse(
    Guid Id, Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal,
    List<OrderItemOptionResponse> Options);

public record OrderStatusHistoryResponse(string Status, DateTime ChangedAtUtc, string? Notes);

public record OrderResponse(
    Guid Id,
    Guid CustomerUserId,
    Guid EstablishmentId,
    Guid CityId,
    Guid? AddressId,
    string Status,
    string Type,
    string PaymentMethod,
    string PaymentStatus,
    decimal? ChangeForAmount,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Total,
    decimal? CalculatedDistanceKm,
    string? Notes,
    DateTime CreatedAtUtc,
    List<OrderItemResponse> Items,
    List<OrderStatusHistoryResponse> StatusHistory);

public record CancelOrderRequest(string? Reason);

/// <summary>Same computation `CreateAsync` runs (subtotal/items validation + delivery fee calc),
/// minus persistence — guarantees the previewed total cannot diverge from what `POST /api/orders`
/// would charge for the identical request, as long as prices/pricing-settings/the address don't
/// change between the two calls (an inherent property of any preview-then-create design, not a
/// bug). See docs/DECISIONS.md #17.</summary>
public record OrderPreviewResponse(
    Guid EstablishmentId,
    string Type,
    Guid? AddressId,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Total,
    decimal? DistanceKm,
    string PricingRuleSnapshotJson,
    List<OrderItemResponse> Items);
