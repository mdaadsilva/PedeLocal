using PedeLocal.Domain.Common;

namespace PedeLocal.Domain.Catalog;

public class MenuCategory : BaseEntity
{
    public Guid EstablishmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
