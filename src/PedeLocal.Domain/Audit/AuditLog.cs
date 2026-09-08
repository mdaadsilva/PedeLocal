namespace PedeLocal.Domain.Audit;

public static class AuditActions
{
    public const string PriceChanged = "PRICE_CHANGED";
    public const string StockAdjusted = "STOCK_ADJUSTED";
    public const string OrderCancelled = "ORDER_CANCELLED";
    public const string DriverApproved = "DRIVER_APPROVED";
    public const string EstablishmentSuspended = "ESTABLISHMENT_SUSPENDED";
    public const string EstablishmentReactivated = "ESTABLISHMENT_REACTIVATED";
    public const string PaymentRegistered = "PAYMENT_REGISTERED";
    public const string FeatureChanged = "FEATURE_CHANGED";
    public const string RefreshTokenReuseDetected = "REFRESH_TOKEN_REUSE_DETECTED";
}

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
