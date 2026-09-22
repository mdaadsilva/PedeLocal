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
        builder.Property(d => d.OnboardingStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property(d => d.RejectionReason).HasMaxLength(500);

        builder.HasIndex(d => d.UserId).IsUnique();
        builder.HasIndex(d => d.Cpf).IsUnique();
        builder.HasIndex(d => d.CityId);
    }
}

public class DriverLicenseConfiguration : IEntityTypeConfiguration<DriverLicense>
{
    public void Configure(EntityTypeBuilder<DriverLicense> builder)
    {
        builder.ToTable("driver_licenses");
        builder.HasKey(l => l.DriverId);
        builder.Property(l => l.RegistrationNumber).HasMaxLength(32).IsRequired();
        builder.Property(l => l.Category).HasMaxLength(8).IsRequired();
        builder.Property(l => l.DocumentImageUrl).HasMaxLength(2048);
    }
}

public class DriverVehicleConfiguration : IEntityTypeConfiguration<DriverVehicle>
{
    public void Configure(EntityTypeBuilder<DriverVehicle> builder)
    {
        builder.ToTable("driver_vehicles");
        builder.HasKey(v => v.DriverId);
        builder.Property(v => v.Type).HasConversion<string>().HasMaxLength(16);
        builder.Property(v => v.Plate).HasMaxLength(16);
        builder.Property(v => v.Model).HasMaxLength(64).IsRequired();
        builder.Property(v => v.Color).HasMaxLength(32).IsRequired();
        builder.Property(v => v.DocumentImageUrl).HasMaxLength(2048);
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

public class DeliveryOccurrenceConfiguration : IEntityTypeConfiguration<DeliveryOccurrence>
{
    public void Configure(EntityTypeBuilder<DeliveryOccurrence> builder)
    {
        builder.ToTable("delivery_occurrences");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(o => o.Note).HasMaxLength(500);

        builder.HasIndex(o => o.DeliveryId);
        builder.HasIndex(o => o.DriverId);
    }
}
