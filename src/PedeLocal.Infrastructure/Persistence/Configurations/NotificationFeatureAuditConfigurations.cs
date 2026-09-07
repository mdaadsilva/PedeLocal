using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PedeLocal.Domain.Audit;
using PedeLocal.Domain.Features;
using PedeLocal.Domain.Notifications;

namespace PedeLocal.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Body).IsRequired();
        builder.HasIndex(n => n.UserId);
    }
}

public class EstablishmentFeatureConfiguration : IEntityTypeConfiguration<EstablishmentFeature>
{
    public void Configure(EntityTypeBuilder<EstablishmentFeature> builder)
    {
        builder.ToTable("establishment_features");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.FeatureKey).HasMaxLength(64).IsRequired();
        builder.HasIndex(f => new { f.EstablishmentId, f.FeatureKey }).IsUnique();
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).HasMaxLength(64).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(64).IsRequired();
        builder.HasIndex(a => a.ActorUserId);
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}
