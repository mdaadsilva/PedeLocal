using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Domain.Pricing;

namespace PedeLocal.Application.Pricing;

public record DeliveryFeeQuote(decimal Fee, decimal DistanceKm, string PricingRuleSnapshotJson);

/// <summary>Delivery fee is a flat, establishment-configured amount — distance no longer affects
/// price. Distance is only used to decide whether DELIVERY is offered at all: beyond
/// <see cref="DeliveryPricingSettings.DeliveryRadiusKm"/> the customer is OUTSIDE_DELIVERY_AREA and
/// must choose PICKUP where available.</summary>
public static class DeliveryFeeCalculator
{
    public static DeliveryFeeQuote Calculate(DeliveryPricingSettings settings, decimal distanceKm)
    {
        if (distanceKm > settings.DeliveryRadiusKm)
        {
            throw new AppException("OUTSIDE_DELIVERY_AREA", "Endereço fora da área de entrega deste estabelecimento.");
        }

        var snapshot = System.Text.Json.JsonSerializer.Serialize(new
        {
            deliveryFee = settings.DeliveryFee,
            distanceKm,
            deliveryRadiusKm = settings.DeliveryRadiusKm
        });

        return new DeliveryFeeQuote(settings.DeliveryFee, distanceKm, snapshot);
    }
}
