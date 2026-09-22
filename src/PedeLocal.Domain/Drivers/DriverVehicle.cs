namespace PedeLocal.Domain.Drivers;

/// <summary>The vehicle a driver uses for deliveries. Keyed directly on DriverId (1:1) — a driver
/// has exactly one active vehicle; switching vehicles replaces this record rather than
/// accumulating history, since delivery assignment only ever cares about the current vehicle.</summary>
public class DriverVehicle
{
    public Guid DriverId { get; set; }

    public VehicleType Type { get; set; }

    /// <summary>Nullable — bicycles commonly have no license plate in Brazil.</summary>
    public string? Plate { get; set; }

    public string Model { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;

    /// <summary>URL returned by <c>POST /api/uploads/image</c> (e.g. CRLV for motorized vehicles) —
    /// optional since a bicycle has no such document.</summary>
    public string? DocumentImageUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
