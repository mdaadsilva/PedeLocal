using Microsoft.Extensions.Logging;
using PedeLocal.Application.Common.Interfaces;

namespace PedeLocal.Infrastructure.Notifications;

/// <summary>Default IPushSender until a Firebase project is configured. Logs instead of sending —
/// swap for a FirebaseAdmin-based sender once real service-account credentials exist (docs
/// section 57 / docs/DECISIONS.md). Business logic never needs to change when that happens,
/// only the DI registration in Infrastructure/DependencyInjection.cs.</summary>
public class NoOpPushSender(ILogger<NoOpPushSender> logger) : IPushSender
{
    public Task SendAsync(string? deviceToken, string title, string body, IDictionary<string, string>? data, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceToken))
        {
            logger.LogDebug("Push skipped (no FCM token registered): {Title}", title);
        }
        else
        {
            logger.LogInformation("Push (FCM not configured, logging only) to {Token}: {Title} — {Body}", deviceToken, title, body);
        }

        return Task.CompletedTask;
    }
}
