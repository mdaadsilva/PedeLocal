using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PedeLocal.Domain.Inventory;
using PedeLocal.Domain.Pricing;

namespace PedeLocal.Infrastructure.Persistence.Configurations;

public class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("inventory_movements");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Type).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(m => m.ProductId);
        builder.HasIndex(m => m.OrderId);
    }
}

public class DeliveryPricingSettingsConfiguration : IEntityTypeConfiguration<DeliveryPricingSettings>
{
    public void Configure(EntityTypeBuilder<DeliveryPricingSettings> builder)
    {
        builder.ToTable("delivery_pricing_settings");
        builder.HasKey(s => s.EstablishmentId);
        builder.Property(s => s.UrbanMode).HasConversion<string>().HasMaxLength(16);
        builder.Property(s => s.RuralMode).HasConversion<string>().HasMaxLength(16);

        foreach (var name in new[]
                 {
                     nameof(DeliveryPricingSettings.UrbanFixedFee), nameof(DeliveryPricingSettings.UrbanPricePerKm),
                     nameof(DeliveryPricingSettings.UrbanMinFee), nameof(DeliveryPricingSettings.RuralFixedFee),
                     nameof(DeliveryPricingSettings.RuralPricePerKm), nameof(DeliveryPricingSettings.RuralMinFee),
                     nameof(DeliveryPricingSettings.MaxDistanceKm)
                 })
        {
            builder.Property(name).HasColumnType("numeric(12,2)");
        }
    }
}
