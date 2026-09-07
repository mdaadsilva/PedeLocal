namespace PedeLocal.Domain.Subscriptions;

public class EstablishmentPayment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EstablishmentId { get; set; }
    public Guid SubscriptionId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public DateTime PaidAtUtc { get; set; }
    public Guid RegisteredByUserId { get; set; }
    public string? Notes { get; set; }
    public string? ReceiptUrl { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
