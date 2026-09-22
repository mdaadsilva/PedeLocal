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

/// <summary>The driver's vetting/compliance lifecycle — distinct from <see cref="DriverStatus"/>,
/// which is real-time presence (online/offline/busy/blocked). A simple CPF registration is not
/// authorization to work: a driver must submit CNH + vehicle documents (PendingDocuments →
/// UnderReview) and be reviewed by a SUPER_ADMIN (→ Approved/Rejected) before they can go online.
/// There is deliberately no "Suspended" value here — <see cref="DriverStatus.Blocked"/> already
/// covers "temporarily prevent an approved driver from working," so adding a second suspension
/// concept at this layer would just be a duplicate state machine. Likewise there is no
/// "DocumentExpired" value: expiry is a time-dependent fact computed by
/// <see cref="DriverEligibilityService"/> from <see cref="DriverLicense.ExpiresAt"/>, not a stored
/// workflow state — persisting it would require a background job to keep it truthful and would
/// conflict with the real vetting decision (Approved) once the driver renews their CNH.</summary>
public enum DriverOnboardingStatus
{
    PendingDocuments,
    UnderReview,
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

    public DriverOnboardingStatus OnboardingStatus { get; set; } = DriverOnboardingStatus.PendingDocuments;
    public DriverStatus Status { get; set; } = DriverStatus.Offline;

    /// <summary>Set whichever way the SUPER_ADMIN's decision went (Approved or Rejected) — covers
    /// both actions instead of duplicating Approved*/Rejected* timestamp pairs.</summary>
    public DateTime? ReviewedAtUtc { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? RejectionReason { get; set; }
}
