using PedeLocal.Domain.Common;

namespace PedeLocal.Domain.Cities;

public class City : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
