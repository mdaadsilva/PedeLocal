using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PedeLocal.Domain.Finance;

namespace PedeLocal.Infrastructure.Persistence.Configurations;

public class DriverReceivableConfiguration : IEntityTypeConfiguration<DriverReceivable>
{
    public void Configure(EntityTypeBuilder<DriverReceivable> builder)
    {
        builder.ToTable("driver_receivables");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Amount).HasColumnType("numeric(12,2)");
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(r => r.DeliveryId).IsUnique();
        builder.HasIndex(r => r.DriverId);
        builder.HasIndex(r => r.PaymentBatchId);
    }
}

public class DriverPaymentBatchConfiguration : IEntityTypeConfiguration<DriverPaymentBatch>
{
    public void Configure(EntityTypeBuilder<DriverPaymentBatch> builder)
    {
        builder.ToTable("driver_payment_batches");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.TotalAmount).HasColumnType("numeric(12,2)");
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(32);

        builder.HasMany(b => b.Receivables)
            .WithOne()
            .HasForeignKey(r => r.PaymentBatchId);
    }
}
