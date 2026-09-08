namespace PedeLocal.Contracts.Me;

public record MyProfileResponse(Guid Id, string Name, string Email, string PhoneNumber, List<string> Roles, DateTime CreatedAtUtc);

public record UpdateProfileRequest(string Name, string Phone);

public record NotificationResponse(Guid Id, string Type, string Title, string Body, string? DataJson, bool IsRead, DateTime CreatedAtUtc, DateTime? SentAtUtc);

public record MyDataExportResponse(
    Guid UserId, string Name, string Email, string PhoneNumber, List<string> Roles,
    DateTime CreatedAtUtc, int AddressCount, int OrderCount);
