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

    public bool CanReceiveNewOrders =>
        OperationalStatus == OperationalStatus.Open &&
        CommercialStatus is CommercialStatus.Active or CommercialStatus.OverdueGrace;
}
