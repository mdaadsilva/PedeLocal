using PedeLocal.Domain.Common;

namespace PedeLocal.Domain.Catalog;

public enum StockMode
{
    Uncontrolled,
    Controlled
}

public class Product : BaseEntity
{
    public Guid EstablishmentId { get; set; }
    public Guid MenuCategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public decimal? PromotionalPrice { get; set; }
    public StockMode StockMode { get; set; } = StockMode.Uncontrolled;
    public int? StockQuantity { get; set; }
    public bool IsAvailable { get; set; } = true;

    /// <summary>Optimistic concurrency token, manually incremented on every StockQuantity change
    /// (same pattern as Delivery.Version) — guards against a lost-update race when two orders for
    /// the same Controlled-stock product are accepted concurrently (docs/SECURITY_AUDIT.md).</summary>
    public int Version { get; set; }

    public ICollection<ProductOptionGroup> OptionGroups { get; set; } = new List<ProductOptionGroup>();
}
