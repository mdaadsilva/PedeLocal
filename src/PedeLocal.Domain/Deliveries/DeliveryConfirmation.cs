namespace PedeLocal.Domain.Deliveries;

public class DeliveryConfirmation
{
    public Guid DeliveryId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    /// <summary>Reversibly encrypted copy of the plaintext code (see IDeliveryCodeCipher), stored
    /// solely so the owning customer can retrieve it on demand. Never returned to a driver — driver
    /// confirmation only ever compares against <see cref="CodeHash"/>. Nullable so rows created
    /// before this field existed degrade gracefully (retrieval endpoint 404s instead of throwing).</summary>
    public string? EncryptedCode { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public DateTime? ConfirmedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
