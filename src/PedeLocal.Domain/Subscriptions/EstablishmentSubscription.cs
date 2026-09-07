using PedeLocal.Domain.Common;

namespace PedeLocal.Domain.Subscriptions;

public enum SubscriptionPaymentStatus
{
    Active,
    OverdueGrace,
    SuspendedPayment
}

public class EstablishmentSubscription : BaseEntity
{
    public Guid EstablishmentId { get; set; }
    public decimal MonthlyAmount { get; set; }
    public int BillingDay { get; set; }
    public DateOnly DueDate { get; set; }
    public int GraceDays { get; set; } = 5;
    public SubscriptionPaymentStatus PaymentStatus { get; set; } = SubscriptionPaymentStatus.Active;

    public ICollection<EstablishmentPayment> Payments { get; set; } = new List<EstablishmentPayment>();
}
