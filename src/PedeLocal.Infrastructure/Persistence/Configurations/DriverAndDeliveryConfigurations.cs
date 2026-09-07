using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PedeLocal.Domain.Deliveries;
using PedeLocal.Domain.Drivers;

namespace PedeLocal.Infrastructure.Persistence.Configurations;

public class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("drivers");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Cpf).HasMaxLength(14).IsRequired();
        builder.Property(d => d.PixKey).HasMaxLength(256).IsRequired();
        builder.Property(d => d.VehicleType).HasConversion<string>().HasMaxLength(16);
        builder.Property(d => d.PixKeyType).HasConversion<string>().HasMaxLength(16);
        builder.Property(d => d.ApprovalStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(16);

        builder.HasIndex(d => d.UserId).IsUnique();
        builder.HasIndex(d => d.Cpf).IsUnique();
        builder.HasIndex(d => d.CityId);
    }
}

public class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public void Configure(EntityTypeBuilder<Delivery> builder)
    {
        builder.ToTable("deliveries");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(16);

        builder.HasIndex(d => d.OrderId).IsUnique();
        builder.HasIndex(d => d.DriverId);
        builder.HasIndex(d => new { d.CityId, d.Status });

        builder.Property(d => d.Version).IsConcurrencyToken();
    }
}

public class DeliveryConfirmationConfiguration : IEntityTypeConfiguration<DeliveryConfirmation>
{
    public void Configure(EntityTypeBuilder<DeliveryConfirmation> builder)
    {
        builder.ToTable("delivery_confirmations");
        builder.HasKey(c => c.DeliveryId);
        builder.Property(c => c.CodeHash).IsRequired();
    }
}
