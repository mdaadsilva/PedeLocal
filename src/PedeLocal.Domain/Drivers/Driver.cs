using PedeLocal.Domain.Common;

namespace PedeLocal.Domain.Drivers;

public enum VehicleType
{
    Bike,
    Motorcycle,
    Car
}

public enum PixKeyType
{
    Cpf,
    Email,
    Phone,
    Random
}

public enum DriverApprovalStatus
{
    PendingApproval,
    Approved,
    Rejected
}

public enum DriverStatus
{
    Offline,
    Available,
    Busy,
    Blocked
}

public class Driver : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid CityId { get; set; }

    public string Cpf { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; }
    public PixKeyType PixKeyType { get; set; }
    public string PixKey { get; set; } = string.Empty;

    public DriverApprovalStatus ApprovalStatus { get; set; } = DriverApprovalStatus.PendingApproval;
    public DriverStatus Status { get; set; } = DriverStatus.Offline;

    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? ApprovedByUserId { get; set; }

    public bool CanWork => ApprovalStatus == DriverApprovalStatus.Approved && Status != DriverStatus.Blocked;
}
