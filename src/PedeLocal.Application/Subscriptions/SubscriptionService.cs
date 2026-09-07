using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Contracts.Subscriptions;
using PedeLocal.Domain.Audit;
using PedeLocal.Domain.Establishments;
using PedeLocal.Domain.Subscriptions;

namespace PedeLocal.Application.Subscriptions;

/// <summary>Super-Admin management of establishment subscriptions/payments. See docs section 48-52.</summary>
public class SubscriptionService(IAppDbContext db, ICurrentUserService currentUser)
{
    public async Task<EstablishmentSubscriptionResponse> GetAsync(Guid establishmentId, CancellationToken ct)
    {
        var subscription = await db.EstablishmentSubscriptions.SingleOrDefaultAsync(s => s.EstablishmentId == establishmentId, ct)
            ?? throw new NotFoundException("SUBSCRIPTION_NOT_FOUND", "Assinatura não configurada para este estabelecimento.");
        return ToResponse(subscription);
    }

    public async Task<EstablishmentSubscriptionResponse> UpsertAsync(Guid establishmentId, UpsertSubscriptionRequest request, CancellationToken ct)
    {
        if (!await db.Establishments.AnyAsync(e => e.Id == establishmentId, ct))
        {
            throw new NotFoundException("ESTABLISHMENT_NOT_FOUND", "Estabelecimento não encontrado.");
        }

        var subscription = await db.EstablishmentSubscriptions.SingleOrDefaultAsync(s => s.EstablishmentId == establishmentId, ct);
        if (subscription is null)
        {
            subscription = new EstablishmentSubscription { EstablishmentId = establishmentId };
            db.EstablishmentSubscriptions.Add(subscription);
        }

        subscription.MonthlyAmount = request.MonthlyAmount;
        subscription.BillingDay = request.BillingDay;
        subscription.DueDate = request.DueDate;
        subscription.GraceDays = request.GraceDays;

        await db.SaveChangesAsync(ct);
        return ToResponse(subscription);
    }

    public async Task<EstablishmentPaymentResponse> RegisterPaymentAsync(Guid establishmentId, RegisterPaymentRequest request, CancellationToken ct)
    {
        var subscription = await db.EstablishmentSubscriptions.SingleOrDefaultAsync(s => s.EstablishmentId == establishmentId, ct)
            ?? throw new NotFoundException("SUBSCRIPTION_NOT_FOUND", "Assinatura não configurada para este estabelecimento.");

        var establishment = await db.Establishments.SingleOrDefaultAsync(e => e.Id == establishmentId, ct)
            ?? throw new NotFoundException("ESTABLISHMENT_NOT_FOUND", "Estabelecimento não encontrado.");

        var wasSuspended = subscription.PaymentStatus == SubscriptionPaymentStatus.SuspendedPayment;

        var payment = new EstablishmentPayment
        {
            EstablishmentId = establishmentId,
            SubscriptionId = subscription.Id,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            PaidAtUtc = request.PaidAtUtc,
            RegisteredByUserId = currentUser.UserId ?? throw new UnauthorizedAppException("UNAUTHENTICATED", "Usuário não autenticado."),
            Notes = request.Notes,
            ReceiptUrl = request.ReceiptUrl
        };
        db.EstablishmentPayments.Add(payment);

        subscription.PaymentStatus = SubscriptionPaymentStatus.Active;
        subscription.DueDate = subscription.DueDate.AddMonths(1);

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = currentUser.UserId, Action = AuditActions.PaymentRegistered,
            EntityType = nameof(Establishment), EntityId = establishmentId
        });

        if (establishment.CommercialStatus is CommercialStatus.SuspendedPayment or CommercialStatus.OverdueGrace)
        {
            establishment.CommercialStatus = CommercialStatus.Active;
            if (wasSuspended)
            {
                db.AuditLogs.Add(new AuditLog
                {
                    ActorUserId = currentUser.UserId, Action = AuditActions.EstablishmentReactivated,
                    EntityType = nameof(Establishment), EntityId = establishmentId
                });
            }
        }

        await db.SaveChangesAsync(ct);
        return ToPaymentResponse(payment);
    }

    public async Task<List<EstablishmentPaymentResponse>> GetPaymentsAsync(Guid establishmentId, CancellationToken ct) =>
        await db.EstablishmentPayments
            .Where(p => p.EstablishmentId == establishmentId)
            .OrderByDescending(p => p.PaidAtUtc)
            .Select(p => ToPaymentResponse(p))
            .ToListAsync(ct);

    private static EstablishmentSubscriptionResponse ToResponse(EstablishmentSubscription s) => new(
        s.Id, s.EstablishmentId, s.MonthlyAmount, s.BillingDay, s.DueDate, s.GraceDays, s.PaymentStatus.ToString());

    private static EstablishmentPaymentResponse ToPaymentResponse(EstablishmentPayment p) => new(
        p.Id, p.EstablishmentId, p.SubscriptionId, p.Amount, p.PaymentMethod, p.PaidAtUtc, p.RegisteredByUserId, p.Notes, p.ReceiptUrl);
}
