namespace PedeLocal.Contracts.Finance;

public record DriverReceivableResponse(
    Guid Id, Guid DeliveryId, Guid DriverId, Guid EstablishmentId, decimal Amount, string Status,
    Guid? PaymentBatchId, DateTime CreatedAtUtc);

public record DriverPaymentBatchResponse(
    Guid Id, Guid DriverId, Guid EstablishmentId, decimal TotalAmount, string Status,
    DateTime CreatedAtUtc, DateTime? PaymentReportedAtUtc, DateTime? ReceivedAtUtc, List<Guid> ReceivableIds);

public record CreatePaymentBatchRequest(Guid DriverId, List<Guid> ReceivableIds);
