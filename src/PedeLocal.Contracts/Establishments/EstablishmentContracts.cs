namespace PedeLocal.Contracts.Establishments;

public record CreateEstablishmentRequest(
    Guid CityId,
    string Name,
    string Document,
    string Phone,
    string Email,
    Guid CategoryId,
    // Address
    string Street,
    string Number,
    string District,
    string? Complement,
    string? ZipCode,
    decimal Latitude,
    decimal Longitude,
    // Admin user
    string AdminName,
    string AdminEmail,
    string AdminPhone,
    string AdminPassword);

public record UpdateEstablishmentRequest(
    string Name,
    string Phone,
    string Email,
    Guid CategoryId,
    string Street,
    string Number,
    string District,
    string? Complement,
    string? ZipCode,
    decimal Latitude,
    decimal Longitude);

public record UpdateEstablishmentStatusRequest(string? OperationalStatus, string? CommercialStatus);

public record EstablishmentResponse(
    Guid Id,
    Guid CityId,
    string Name,
    string Slug,
    string Document,
    string Phone,
    string Email,
    Guid CategoryId,
    Guid AddressId,
    string OperationalStatus,
    string CommercialStatus,
    DateTime CreatedAtUtc,
    string? PauseReason,
    DateTime? PausedAtUtc,
    DateTime? PausedUntilUtc);

public record EstablishmentCategoryResponse(Guid Id, string Name);
