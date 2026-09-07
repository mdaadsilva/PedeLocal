using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Domain.Addresses;
using PedeLocal.Domain.Pricing;

namespace PedeLocal.Application.Pricing;

public record DeliveryFeeQuote(decimal Fee, decimal DistanceKm, string PricingRuleSnapshotJson);

/// <summary>Pure calculation of the delivery fee: fee = max(distance_km * price_per_km, min_fee)
/// for PER_KM, or a flat fee for FIXED. See docs section 29-32.</summary>
public static class DeliveryFeeCalculator
{
    public static DeliveryFeeQuote Calculate(DeliveryPricingSettings settings, AddressType addressType, decimal distanceKm)
    {
        if (distanceKm > settings.MaxDistanceKm)
        {
            throw new AppException("DELIVERY_NOT_AVAILABLE", "Distância acima do limite de entrega do estabelecimento.");
        }

        var (mode, fixedFee, pricePerKm, minFee) = addressType == AddressType.Rural
            ? (settings.RuralMode, settings.RuralFixedFee, settings.RuralPricePerKm, settings.RuralMinFee)
            : (settings.UrbanMode, settings.UrbanFixedFee, settings.UrbanPricePerKm, settings.UrbanMinFee);

        var fee = mode == DeliveryFeeMode.Fixed
            ? fixedFee
            : Math.Max(distanceKm * pricePerKm, minFee);

        var snapshot = System.Text.Json.JsonSerializer.Serialize(new
        {
            addressType = addressType.ToString(),
            mode = mode.ToString(),
            fixedFee,
            pricePerKm,
            minFee,
            distanceKm,
            maxDistanceKm = settings.MaxDistanceKm
        });

        return new DeliveryFeeQuote(fee, distanceKm, snapshot);
    }
}
