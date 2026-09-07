namespace PedeLocal.Domain.Pricing;

public enum DeliveryFeeMode
{
    Fixed,
    PerKm
}

public class DeliveryPricingSettings
{
    public Guid EstablishmentId { get; set; }

    public DeliveryFeeMode UrbanMode { get; set; } = DeliveryFeeMode.Fixed;
    public decimal UrbanFixedFee { get; set; }
    public decimal UrbanPricePerKm { get; set; }
    public decimal UrbanMinFee { get; set; }

    public DeliveryFeeMode RuralMode { get; set; } = DeliveryFeeMode.PerKm;
    public decimal RuralFixedFee { get; set; }
    public decimal RuralPricePerKm { get; set; }
    public decimal RuralMinFee { get; set; }

    public decimal MaxDistanceKm { get; set; } = 15;
}
