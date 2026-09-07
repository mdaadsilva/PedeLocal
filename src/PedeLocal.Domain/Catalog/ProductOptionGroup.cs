using PedeLocal.Domain.Common;

namespace PedeLocal.Domain.Catalog;

public class ProductOptionGroup : BaseEntity
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Required { get; set; }
    public int Min { get; set; }
    public int Max { get; set; }

    public ICollection<ProductOption> Options { get; set; } = new List<ProductOption>();
}

public class ProductOption : BaseEntity
{
    public Guid ProductOptionGroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PriceDelta { get; set; }
    public bool IsAvailable { get; set; } = true;
}
