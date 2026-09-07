using PedeLocal.Domain.Common;

namespace PedeLocal.Domain.Deliveries;

public enum DeliveryStatus
{
    Available,
    Assigned,
    PickedUp,
    OutForDelivery,
    Completed,
    Cancelled
}

public class Delivery : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid EstablishmentId { get; set; }
    public Guid CityId { get; set; }
    public Guid? DriverId { get; set; }

    public DeliveryStatus Status { get; set; } = DeliveryStatus.Available;

    public DateTime? AssignedAtUtc { get; set; }
    public DateTime? PickedUpAtUtc { get; set; }
    public DateTime? OutForDeliveryAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>Optimistic concurrency token, bumped by the app on every accepted write — guards
    /// the driver-acceptance race (docs section 40-41). App-managed (not DB-generated) so the
    /// same code path is exercised, and testable, on every EF provider including InMemory.</summary>
    public int Version { get; set; }
}
