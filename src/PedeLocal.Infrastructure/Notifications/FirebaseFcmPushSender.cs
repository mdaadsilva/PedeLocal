using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;
using PedeLocal.Application.Common.Interfaces;

namespace PedeLocal.Infrastructure.Notifications;

/// <summary>Real push delivery via the Firebase Admin SDK. Thin by design — its only job is
/// translating our provider-agnostic <see cref="IPushSender"/> call into an FCM `Message` and
/// mapping FCM's own error taxonomy onto the two outcomes the rest of the app cares about: "this
/// token is permanently invalid" (<see cref="InvalidPushTokenException"/>, caller should deactivate
/// it) vs. "this attempt failed for some other reason" (any other exception, caller should just log
/// and move on). See docs/PUSH_NOTIFICATIONS.md.</summary>
public class FirebaseFcmPushSender(FirebaseMessaging messaging, ILogger<FirebaseFcmPushSender> logger) : IPushSender
{
    public async Task SendAsync(string deviceToken, string title, string body, IDictionary<string, string>? data, CancellationToken ct = default)
    {
        var message = new Message
        {
            // Message.Token is flagged obsolete by the SDK in favor of Fid (Firebase Installation
            // ID) — a different identifier entirely, tied to the Firebase Installations API, not
            // the FCM registration token our clients register via PUT /api/me/device-tokens. There
            // is no drop-in replacement here; Token remains the correct field for this integration
            // (see docs/DECISIONS.md).
#pragma warning disable CS0618
            Token = deviceToken,
#pragma warning restore CS0618
            Notification = new FirebaseAdmin.Messaging.Notification { Title = title, Body = body },
            Data = data?.ToDictionary(kv => kv.Key, kv => kv.Value)
        };

        try
        {
            await messaging.SendAsync(message, ct);
        }
        catch (FirebaseMessagingException ex) when (ex.MessagingErrorCode is MessagingErrorCode.Unregistered or MessagingErrorCode.InvalidArgument)
        {
            // Never log the token value itself — only which error code FCM reported.
            logger.LogInformation("FCM reported {ErrorCode} for a device token — treating as invalid.", ex.MessagingErrorCode);
            throw new InvalidPushTokenException($"FCM rejected the device token: {ex.MessagingErrorCode}.");
        }
    }
}
