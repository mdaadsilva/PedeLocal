using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PedeLocal.Domain.Establishments;

namespace PedeLocal.Infrastructure.Persistence.Configurations;

public class EstablishmentCategoryConfiguration : IEntityTypeConfiguration<EstablishmentCategory>
{
    public void Configure(EntityTypeBuilder<EstablishmentCategory> builder)
    {
        builder.ToTable("establishment_categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
    }
}

public class EstablishmentConfiguration : IEntityTypeConfiguration<Establishment>
{
    public void Configure(EntityTypeBuilder<Establishment> builder)
    {
        builder.ToTable("establishments");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Slug).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Document).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Phone).HasMaxLength(32).IsRequired();
        builder.Property(e => e.Email).HasMaxLength(256).IsRequired();
        builder.Property(e => e.OperationalStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.CommercialStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.PauseReason).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(e => e.Slug).IsUnique();
        builder.HasIndex(e => e.CityId);
    }
}

public class EstablishmentUserConfiguration : IEntityTypeConfiguration<EstablishmentUser>
{
    public void Configure(EntityTypeBuilder<EstablishmentUser> builder)
    {
        builder.ToTable("establishment_users");
        builder.HasKey(eu => new { eu.EstablishmentId, eu.UserId });
        builder.Property(eu => eu.StaffRole).HasMaxLength(64).IsRequired();
    }
}

public class EstablishmentSettingsConfiguration : IEntityTypeConfiguration<EstablishmentSettings>
{
    public void Configure(EntityTypeBuilder<EstablishmentSettings> builder)
    {
        builder.ToTable("establishment_settings");
        builder.HasKey(s => s.EstablishmentId);
    }
}
