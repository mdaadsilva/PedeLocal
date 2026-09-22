namespace PedeLocal.Contracts.Drivers;

public record DriverResponse(
    Guid Id,
    Guid UserId,
    string Name,
    Guid CityId,
    string Cpf,
    string VehicleType,
    string PixKeyType,
    string PixKey,
    string OnboardingStatus,
    string Status,
    /// <summary>Computed by DriverEligibilityService — the definitive "can this driver work right
    /// now, and if not why" answer. Eligible/PendingDocuments/UnderReview/Rejected/Blocked/DocumentExpired.</summary>
    string EligibilityStatus,
    DateTime? ReviewedAtUtc,
    string? RejectionReason);

/// <summary>CNH data — never returned to CUSTOMER or MERCHANT endpoints, only to the driver
/// themselves (self-service) and SUPER_ADMIN (review/detail).</summary>
public record DriverLicenseResponse(
    string RegistrationNumber,
    string Category,
    DateOnly ExpiresAt,
    bool? HasEar,
    string? DocumentImageUrl,
    /// <summary>Negative once expired. Exposed so admin/driver clients can build the 30/15/7-day and
    /// expired alert buckets described in the onboarding spec without a dedicated push pipeline yet.</summary>
    int DaysUntilExpiration);

public record DriverVehicleResponse(
    string Type,
    string? Plate,
    string Model,
    string Color,
    string? DocumentImageUrl);

/// <summary>Full admin (and driver self-service) view — driver profile plus whatever CNH/vehicle
/// data has been submitted so far (either can be null before submission).</summary>
public record DriverDetailResponse(
    DriverResponse Driver,
    DriverLicenseResponse? License,
    DriverVehicleResponse? Vehicle);

public record RejectDriverRequest(string? Reason);

public record SubmitDriverLicenseRequest(
    string RegistrationNumber,
    string Category,
    DateOnly ExpiresAt,
    bool? HasEar,
    string? DocumentImageUrl);

public record SubmitDriverVehicleRequest(
    string Type,
    string? Plate,
    string Model,
    string Color,
    string? DocumentImageUrl);
