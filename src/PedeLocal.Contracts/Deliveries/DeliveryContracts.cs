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
