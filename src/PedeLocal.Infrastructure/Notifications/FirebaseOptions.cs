namespace PedeLocal.Infrastructure.Notifications;

/// <summary>Bound from the `Firebase` configuration section — env vars only in every real
/// environment (`Firebase__Enabled`, `Firebase__CredentialsJson`), never appsettings*.json (see
/// docs/PUSH_NOTIFICATIONS.md). No property here is a secret by itself: `CredentialsJson` is the
/// one that is, and it must only ever come from an environment variable or a mounted secret file
/// (see `CredentialsPath`), never a committed file.</summary>
public class FirebaseOptions
{
    public const string SectionName = "Firebase";

    /// <summary>Explicit opt-in — real push is only attempted when this is true AND credentials
    /// resolve successfully. Defaults to false, so a fresh checkout with no Firebase project
    /// configured falls back to <see cref="NoOpPushSender"/> automatically, in every environment.</summary>
    public bool Enabled { get; set; }

    /// <summary>The full service-account JSON as a single string, from an environment variable or
    /// secret manager — never a file committed to the repo. Takes precedence over
    /// <see cref="CredentialsPath"/> when both are set.</summary>
    public string? CredentialsJson { get; set; }

    /// <summary>Path to a service-account JSON file mounted at runtime (e.g. a Docker/Kubernetes
    /// secret volume) — an alternative to <see cref="CredentialsJson"/> for platforms that support
    /// mounting files but not multi-line env vars. When neither is set and <see cref="Enabled"/> is
    /// true, falls back to Google's Application Default Credentials
    /// (honors the standard `GOOGLE_APPLICATION_CREDENTIALS` env var).</summary>
    public string? CredentialsPath { get; set; }
}
