namespace PedeLocal.Contracts.Pricing;

public record DeliveryPricingResponse(
    Guid EstablishmentId,
    string UrbanMode,
    decimal UrbanFixedFee,
    decimal UrbanPricePerKm,
    decimal UrbanMinFee,
    string RuralMode,
    decimal RuralFixedFee,
    decimal RuralPricePerKm,
    decimal RuralMinFee,
    decimal MaxDistanceKm);

public record UpdateDeliveryPricingRequest(
    string UrbanMode,
    decimal UrbanFixedFee,
    decimal UrbanPricePerKm,
    decimal UrbanMinFee,
    string RuralMode,
    decimal RuralFixedFee,
    decimal RuralPricePerKm,
    decimal RuralMinFee,
    decimal MaxDistanceKm);
