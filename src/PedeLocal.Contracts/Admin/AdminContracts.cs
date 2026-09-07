namespace PedeLocal.Contracts.Admin;

public record CustomerResponse(Guid Id, string Name, string Email, string PhoneNumber, bool IsActive, DateTime CreatedAtUtc);

public record AuditLogResponse(
    Guid Id, Guid? ActorUserId, string Action, string EntityType, Guid? EntityId,
    string? Ip, string? UserAgent, string? MetadataJson, DateTime CreatedAtUtc);

public record AdminDeliveryResponse(
    Guid Id, Guid OrderId, Guid EstablishmentId, Guid CityId, Guid? DriverId, string Status, DateTime CreatedAtUtc);

public record EstablishmentFeatureResponse(Guid Id, Guid EstablishmentId, string FeatureKey, bool Enabled, string? SettingsJson);

public record SetFeatureRequest(bool Enabled, string? SettingsJson);
