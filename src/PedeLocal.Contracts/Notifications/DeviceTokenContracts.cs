namespace PedeLocal.Contracts.Notifications;

/// <summary>Registers (or refreshes) one device's FCM token for the authenticated user — idempotent:
/// calling it again with the same token just updates platform/timestamps/active flag, so the client
/// can call this on every app start without worrying about duplicates.</summary>
public record RegisterDeviceTokenRequest(string Token, string Platform);

/// <summary>Removes one device token from the authenticated user — call on logout/uninstall so that
/// device stops receiving push. Only ever removes a token that belongs to the caller.</summary>
public record RemoveDeviceTokenRequest(string Token);
