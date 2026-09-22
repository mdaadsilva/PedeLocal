namespace PedeLocal.Contracts.Establishments;

/// <summary>PausedUntilUtc is optional — omit it for an indefinite pause that only ends when a
/// merchant explicitly resumes it (see EstablishmentPauseExpiryJob for the expiry case).</summary>
public record PauseEstablishmentRequest(string Reason, DateTime? PausedUntilUtc);
