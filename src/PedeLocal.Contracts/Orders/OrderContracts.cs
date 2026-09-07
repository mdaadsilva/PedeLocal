namespace PedeLocal.Contracts.Orders;

public record CreateOrderItemOptionSelection(Guid ProductOptionId);

public record CreateOrderItemRequest(Guid ProductId, int Quantity, List<CreateOrderItemOptionSelection> Options);

public record CreateOrderRequest(
    Guid EstablishmentId,
    Guid AddressId,
    string PaymentMethod,
    decimal? ChangeForAmount,
    List<CreateOrderItemRequest> Items,
    string? IdempotencyKey);

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
    Guid AddressId,
    string Status,
    string PaymentMethod,
    string PaymentStatus,
    decimal? ChangeForAmount,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Total,
    decimal? CalculatedDistanceKm,
    DateTime CreatedAtUtc,
    List<OrderItemResponse> Items,
    List<OrderStatusHistoryResponse> StatusHistory);

public record CancelOrderRequest(string? Reason);
