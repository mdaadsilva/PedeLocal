using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PedeLocal.Domain.Orders;

namespace PedeLocal.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(o => o.PaymentMethod).HasConversion<string>().HasMaxLength(32);
        builder.Property(o => o.PaymentStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(o => o.Subtotal).HasColumnType("numeric(12,2)");
        builder.Property(o => o.DeliveryFee).HasColumnType("numeric(12,2)");
        builder.Property(o => o.Total).HasColumnType("numeric(12,2)");
        builder.Property(o => o.ChangeForAmount).HasColumnType("numeric(12,2)");
        builder.Property(o => o.CalculatedDistanceKm).HasColumnType("numeric(9,3)");

        builder.HasIndex(o => o.CustomerUserId);
        builder.HasIndex(o => o.EstablishmentId);
        builder.HasIndex(o => o.IdempotencyKey).IsUnique();

        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.StatusHistory)
            .WithOne()
            .HasForeignKey(h => h.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.UnitPrice).HasColumnType("numeric(12,2)");
        builder.Property(i => i.LineTotal).HasColumnType("numeric(12,2)");

        builder.HasMany(i => i.Options)
            .WithOne()
            .HasForeignKey(o => o.OrderItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderItemOptionConfiguration : IEntityTypeConfiguration<OrderItemOption>
{
    public void Configure(EntityTypeBuilder<OrderItemOption> builder)
    {
        builder.ToTable("order_item_options");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.OptionName).HasMaxLength(100).IsRequired();
        builder.Property(o => o.PriceDelta).HasColumnType("numeric(12,2)");
    }
}

public class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("order_status_history");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Status).HasConversion<string>().HasMaxLength(32);
    }
}
