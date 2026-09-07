namespace PedeLocal.Domain.Finance;

public enum ReceivableStatus
{
    Pending,
    PaymentReported,
    Received,
    Disputed
}

public class DriverReceivable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeliveryId { get; set; }
    public Guid DriverId { get; set; }
    public Guid EstablishmentId { get; set; }
    public decimal Amount { get; set; }
    public ReceivableStatus Status { get; set; } = ReceivableStatus.Pending;
    public Guid? PaymentBatchId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
