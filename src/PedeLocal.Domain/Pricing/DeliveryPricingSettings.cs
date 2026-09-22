namespace PedeLocal.Domain.Pricing;

/// <summary>A flat delivery fee per establishment plus a maximum delivery radius. Distance no
/// longer affects price (see docs/DECISIONS.md for why per-km pricing was retired) — it only gates
/// whether DELIVERY is offered at all for a given address (see DeliveryFeeCalculator). The fee is
/// still snapshotted onto Order.DeliveryFee/PricingRuleSnapshotJson at order time, so changing it
/// here never affects historical orders.</summary>
public class DeliveryPricingSettings
{
    public Guid EstablishmentId { get; set; }

    public decimal DeliveryFee { get; set; }

    /// <summary>Deliveries beyond this distance from the establishment are not offered — the
    /// customer can still choose PICKUP where available. Renamed from the old MaxDistanceKm, same
    /// meaning, now the establishment's only radius concept (no more per zone).</summary>
    public decimal DeliveryRadiusKm { get; set; } = 15;
}
