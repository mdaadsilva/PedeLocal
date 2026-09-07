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
    string ApprovalStatus,
    string Status,
    DateTime? ApprovedAtUtc);

public record RejectDriverRequest(string? Reason);
