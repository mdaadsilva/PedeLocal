using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PedeLocal.Domain.Addresses;
using PedeLocal.Domain.Cities;

namespace PedeLocal.Infrastructure.Persistence.Configurations;

public class CityConfiguration : IEntityTypeConfiguration<City>
{
    public void Configure(EntityTypeBuilder<City> builder)
    {
        builder.ToTable("cities");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.State).HasMaxLength(2).IsRequired();
    }
}

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("addresses");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(16);
        builder.Property(a => a.Latitude).HasColumnType("numeric(9,6)");
        builder.Property(a => a.Longitude).HasColumnType("numeric(9,6)");
        builder.HasIndex(a => a.CityId);
        builder.HasIndex(a => a.UserId);
    }
}
