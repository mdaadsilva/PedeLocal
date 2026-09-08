namespace PedeLocal.Application.Common.Interfaces;

/// <summary>Thin abstraction over the actual push provider (Firebase Cloud Messaging). Kept
/// separate from INotificationService so the FCM wiring can be swapped in later without
/// touching any business logic — see docs/PUSH_NOTIFICATIONS.md.</summary>
public interface IPushSender
{
    /// <summary>Sends a single push to a single device token. Throws
    /// <see cref="InvalidPushTokenException"/> when the provider reports the token itself is
    /// invalid/unregistered/expired (the caller should deactivate it) — any other exception means a
    /// transient/provider-side failure (network, quota, outage), which the caller should log and
    /// otherwise ignore without touching the token.</summary>
    Task SendAsync(string deviceToken, string title, string body, IDictionary<string, string>? data, CancellationToken ct = default);
}

/// <summary>Signals that the push provider rejected a specific device token as permanently invalid
/// (e.g. FCM's UNREGISTERED / InvalidArgument for a malformed or uninstalled-app token) — distinct
/// from a transient send failure, so the caller knows to deactivate the token instead of just
/// logging and retrying next time.</summary>
public class InvalidPushTokenException(string message) : Exception(message);
