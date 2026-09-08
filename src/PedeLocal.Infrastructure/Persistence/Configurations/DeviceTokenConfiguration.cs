using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PedeLocal.Domain.Identity;
using PedeLocal.Domain.Notifications;

namespace PedeLocal.Infrastructure.Persistence.Configurations;

public class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.ToTable("device_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Token).HasMaxLength(4096).IsRequired();
        builder.Property(t => t.Platform).HasConversion<string>().HasMaxLength(16);

        // The token itself is the natural unique key (docs/PUSH_NOTIFICATIONS.md) — re-registering
        // one reassigns/reactivates the existing row rather than duplicating it.
        builder.HasIndex(t => t.Token).IsUnique();
        // Every push fan-out queries "this user's active tokens" — covers that lookup directly.
        builder.HasIndex(t => new { t.UserId, t.IsActive });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
