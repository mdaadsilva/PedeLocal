using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PedeLocal.Domain.Catalog;

namespace PedeLocal.Infrastructure.Persistence.Configurations;

public class MenuCategoryConfiguration : IEntityTypeConfiguration<MenuCategory>
{
    public void Configure(EntityTypeBuilder<MenuCategory> builder)
    {
        builder.ToTable("menu_categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(c => c.EstablishmentId);
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Price).HasColumnType("numeric(12,2)");
        builder.Property(p => p.PromotionalPrice).HasColumnType("numeric(12,2)");
        builder.Property(p => p.StockMode).HasConversion<string>().HasMaxLength(16);
        builder.HasIndex(p => p.EstablishmentId);
        builder.HasIndex(p => p.MenuCategoryId);

        builder.HasMany(p => p.OptionGroups)
            .WithOne()
            .HasForeignKey(g => g.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductOptionGroupConfiguration : IEntityTypeConfiguration<ProductOptionGroup>
{
    public void Configure(EntityTypeBuilder<ProductOptionGroup> builder)
    {
        builder.ToTable("product_option_groups");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Name).HasMaxLength(100).IsRequired();

        builder.HasMany(g => g.Options)
            .WithOne()
            .HasForeignKey(o => o.ProductOptionGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProductOptionConfiguration : IEntityTypeConfiguration<ProductOption>
{
    public void Configure(EntityTypeBuilder<ProductOption> builder)
    {
        builder.ToTable("product_options");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Name).HasMaxLength(100).IsRequired();
        builder.Property(o => o.PriceDelta).HasColumnType("numeric(12,2)");
    }
}
