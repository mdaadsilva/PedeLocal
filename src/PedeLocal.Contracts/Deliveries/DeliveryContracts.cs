using PedeLocal.Contracts.Addresses;
using PedeLocal.Contracts.Orders;

namespace PedeLocal.Contracts.Deliveries;

public record DeliveryResponse(
    Guid Id,
    Guid OrderId,
    Guid EstablishmentId,
    Guid CityId,
    Guid? DriverId,
    string Status,
    DateTime? AssignedAtUtc,
    DateTime? PickedUpAtUtc,
    DateTime? OutForDeliveryAtUtc,
    DateTime? CompletedAtUtc);

public record ConfirmDeliveryRequest(string Code);

/// <summary>Returned only to the CUSTOMER who owns the order — never the hash, never to a driver.
/// See docs/DECISIONS.md #17.</summary>
public record DeliveryCodeResponse(string Code, DateTime ExpiresAtUtc);

public record DeliveryDetailOrderResponse(
    Guid Id,
    string PaymentMethod,
    string PaymentStatus,
    decimal? ChangeForAmount,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Total,
    string? Notes,
    List<OrderItemResponse> Items);

public record DeliveryDetailEstablishmentResponse(
    Guid Id,
    string Name,
    string Phone,
    AddressResponse Address);

/// <summary>Everything a driver needs to execute a delivery they're assigned to. Only reachable by
/// the driver actually assigned to this delivery — see DeliveryService.GetDetailsAsync.</summary>
public record DeliveryDetailResponse(
    Guid Id,
    string Status,
    DeliveryDetailOrderResponse Order,
    DeliveryDetailEstablishmentResponse Establishment,
    AddressResponse DeliveryAddress);

public record CreateDeliveryOccurrenceRequest(string Type, string? Note);

public record DeliveryOccurrenceResponse(
    Guid Id,
    Guid DeliveryId,
    Guid DriverId,
    string Type,
    string? Note,
    DateTime CreatedAtUtc);
