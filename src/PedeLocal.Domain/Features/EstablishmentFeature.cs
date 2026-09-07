namespace PedeLocal.Domain.Features;

public static class FeatureKeys
{
    public const string CustomReport = "CUSTOM_REPORT";
    public const string WholesaleMode = "WHOLESALE_MODE";
    public const string AdvancedStock = "ADVANCED_STOCK";
    public const string SpecialOrderFlow = "SPECIAL_ORDER_FLOW";
}

public class EstablishmentFeature
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EstablishmentId { get; set; }
    public string FeatureKey { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string? SettingsJson { get; set; }
}
