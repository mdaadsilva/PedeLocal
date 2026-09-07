namespace PedeLocal.Domain.Establishments;

public class EstablishmentSettings
{
    public Guid EstablishmentId { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool AcceptsNewOrders { get; set; } = true;
}
