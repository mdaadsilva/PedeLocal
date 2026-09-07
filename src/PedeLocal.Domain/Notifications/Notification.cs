namespace PedeLocal.Domain.Notifications;

public enum NotificationEventType
{
    OrderCreated,
    OrderAccepted,
    OrderPreparing,
    OrderReady,
    OrderOutForDelivery,
    OrderCancelled,
    DeliveryAvailable,
    DeliveryAccepted,
    OrderDelivered,
    PaymentReported,
    SubscriptionWarning
}

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public NotificationEventType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? DataJson { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SentAtUtc { get; set; }
}
