namespace PedeLocal.Contracts.Pricing;

public record DeliveryPricingResponse(
    Guid EstablishmentId,
    decimal DeliveryFee,
    decimal DeliveryRadiusKm);

public record UpdateDeliveryPricingRequest(
    decimal DeliveryFee,
    decimal DeliveryRadiusKm);
