using PedeLocal.Domain.Common;

namespace PedeLocal.Domain.Notifications;

public enum DevicePlatform
{
    Unknown,
    Ios,
    Android,
    Web
}

/// <summary>A single device's FCM registration token (docs/PUSH_NOTIFICATIONS.md). A user can have
/// several active devices at once (phone + tablet, reinstalled app, etc.) — this replaces the old
/// single `User.FcmToken` column. The token string itself is the natural unique key: FCM tokens are
/// unique per app install, so registering an already-known token (even one that used to belong to a
/// different user — a shared/reset device, or someone logging out and a different account logging
/// in on the same phone) reassigns and reactivates that row instead of creating a duplicate.</summary>
public class DeviceToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; } = DevicePlatform.Unknown;
    public bool IsActive { get; set; } = true;
}
