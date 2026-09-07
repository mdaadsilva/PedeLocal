namespace PedeLocal.Application.Common.Interfaces;

/// <summary>Thin abstraction over the actual push provider (Firebase Cloud Messaging). Kept
/// separate from INotificationService so the FCM wiring can be swapped in later without
/// touching any business logic — see docs section 57 and docs/DECISIONS.md.</summary>
public interface IPushSender
{
    Task SendAsync(string? deviceToken, string title, string body, IDictionary<string, string>? data, CancellationToken ct = default);
}
