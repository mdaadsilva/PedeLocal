using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PedeLocal.Domain.Subscriptions;

namespace PedeLocal.Infrastructure.Persistence.Configurations;

public class EstablishmentSubscriptionConfiguration : IEntityTypeConfiguration<EstablishmentSubscription>
{
    public void Configure(EntityTypeBuilder<EstablishmentSubscription> builder)
    {
        builder.ToTable("establishment_subscriptions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.MonthlyAmount).HasColumnType("numeric(12,2)");
        builder.Property(s => s.PaymentStatus).HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(s => s.EstablishmentId);

        builder.HasMany(s => s.Payments)
            .WithOne()
            .HasForeignKey(p => p.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class EstablishmentPaymentConfiguration : IEntityTypeConfiguration<EstablishmentPayment>
{
    public void Configure(EntityTypeBuilder<EstablishmentPayment> builder)
    {
        builder.ToTable("establishment_payments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Amount).HasColumnType("numeric(12,2)");
        builder.Property(p => p.PaymentMethod).HasMaxLength(64).IsRequired();
    }
}
