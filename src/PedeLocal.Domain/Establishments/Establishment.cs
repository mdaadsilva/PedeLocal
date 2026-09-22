using PedeLocal.Domain.Common;

namespace PedeLocal.Domain.Establishments;

public enum OperationalStatus
{
    Open,
    ClosedManual,
    ClosedSchedule,
    Paused
}

public enum CommercialStatus
{
    Active,
    OverdueGrace,
    SuspendedPayment,
    SuspendedAdmin,
    Inactive
}

public enum EstablishmentPauseReason
{
    HighDemand,
    NoDriverAvailable,
    TemporaryClosure,
    Other
}

public class Establishment : BaseEntity
{
    public Guid CityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Document { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public Guid AddressId { get; set; }

    public OperationalStatus OperationalStatus { get; set; } = OperationalStatus.ClosedManual;
    public CommercialStatus CommercialStatus { get; set; } = CommercialStatus.Active;

    /// <summary>Set together with OperationalStatus = Paused via EstablishmentService.PauseAsync.
    /// PausedUntilUtc is optional — a pause with no expiry only ends when a merchant explicitly
    /// resumes it; one with an expiry is also auto-resumed by EstablishmentPauseExpiryJob.</summary>
    public EstablishmentPauseReason? PauseReason { get; set; }
    public DateTime? PausedAtUtc { get; set; }
    public DateTime? PausedUntilUtc { get; set; }
    public Guid? PausedByUserId { get; set; }

    /// <summary>Checked at order-creation time (OrderService.BuildQuoteAsync) — evaluates the pause
    /// expiry lazily so an order isn't blocked by a pause that has already timed out even if
    /// EstablishmentPauseExpiryJob hasn't run yet.</summary>
    public bool CanReceiveNewOrders =>
        IsOperationallyOpen() &&
        CommercialStatus is CommercialStatus.Active or CommercialStatus.OverdueGrace;

    /// <summary>True when Open, or when Paused but the pause's expiry has already passed — lets
    /// order creation (OrderService.BuildQuoteAsync) treat a timed-out pause as over immediately,
    /// without waiting for EstablishmentPauseExpiryJob's next run to flip the persisted status.</summary>
    public bool IsOperationallyOpen()
    {
        if (OperationalStatus != OperationalStatus.Paused)
        {
            return OperationalStatus == OperationalStatus.Open;
        }

        return PausedUntilUtc is { } until && until <= DateTime.UtcNow;
    }
}
