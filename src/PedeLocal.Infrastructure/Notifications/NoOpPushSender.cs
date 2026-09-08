using Microsoft.Extensions.Logging;
using PedeLocal.Application.Common.Interfaces;

namespace PedeLocal.Infrastructure.Notifications;

/// <summary>Default IPushSender when Firebase isn't configured (no `Firebase:Enabled`, or
/// running in Development without credentials) — logs instead of sending. Business logic never
/// needs to change when a real Firebase project is wired in, only the DI registration in
/// Infrastructure/DependencyInjection.cs. See docs/PUSH_NOTIFICATIONS.md.</summary>
public class NoOpPushSender(ILogger<NoOpPushSender> logger) : IPushSender
{
    public Task SendAsync(string deviceToken, string title, string body, IDictionary<string, string>? data, CancellationToken ct = default)
    {
        // Never log the token itself — only its length, enough to spot an obviously malformed one
        // during development without putting a real device identifier in the logs.
        logger.LogInformation("Push (Firebase not configured, logging only) to token[len={TokenLength}]: {Title}", deviceToken.Length, title);
        return Task.CompletedTask;
    }
}
