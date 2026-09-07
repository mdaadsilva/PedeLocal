namespace PedeLocal.Domain.Finance;

public enum PaymentBatchStatus
{
    Pending,
    PaymentReported,
    Received
}

public class DriverPaymentBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DriverId { get; set; }
    public Guid EstablishmentId { get; set; }
    public decimal TotalAmount { get; set; }
    public PaymentBatchStatus Status { get; set; } = PaymentBatchStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? PaymentReportedAtUtc { get; set; }
    public DateTime? ReceivedAtUtc { get; set; }

    public ICollection<DriverReceivable> Receivables { get; set; } = new List<DriverReceivable>();
}
