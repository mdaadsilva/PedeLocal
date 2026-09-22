using PedeLocal.Domain.Common;

namespace PedeLocal.Domain.Deliveries;

public enum DeliveryOccurrenceType
{
    CustomerNotFound,
    CustomerNotResponding,
    WrongAddress,
    PaymentProblem,
    VehicleProblem,
    Other
}

/// <summary>An operational incident reported by the driver during a delivery. Immutable once
/// created — there is no update/delete path, since an occurrence is a historical record of what the
/// driver reported at the time, not a mutable ticket.</summary>
public class DeliveryOccurrence : BaseEntity
{
    public Guid DeliveryId { get; set; }
    public Guid DriverId { get; set; }
    public DeliveryOccurrenceType Type { get; set; }
    public string? Note { get; set; }
}
