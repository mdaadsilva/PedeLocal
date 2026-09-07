namespace PedeLocal.Contracts.Subscriptions;

public record EstablishmentSubscriptionResponse(
    Guid Id, Guid EstablishmentId, decimal MonthlyAmount, int BillingDay, DateOnly DueDate, int GraceDays, string PaymentStatus);

public record UpsertSubscriptionRequest(decimal MonthlyAmount, int BillingDay, DateOnly DueDate, int GraceDays);

public record RegisterPaymentRequest(decimal Amount, string PaymentMethod, DateTime PaidAtUtc, string? Notes, string? ReceiptUrl);

public record EstablishmentPaymentResponse(
    Guid Id, Guid EstablishmentId, Guid SubscriptionId, decimal Amount, string PaymentMethod,
    DateTime PaidAtUtc, Guid RegisteredByUserId, string? Notes, string? ReceiptUrl);
