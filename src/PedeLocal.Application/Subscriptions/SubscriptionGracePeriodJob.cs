using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Domain.Audit;
using PedeLocal.Domain.Establishments;
using PedeLocal.Domain.Identity;
using PedeLocal.Domain.Notifications;
using PedeLocal.Domain.Subscriptions;

namespace PedeLocal.Application.Subscriptions;

/// <summary>Daily job: Active -> OverdueGrace once past due date, OverdueGrace -> SuspendedPayment
/// once past the grace window. Pure function of current state + today's date, so re-running it
/// (e.g. after a missed day) is safe and never double-applies an already-applied transition —
/// see docs section 53.</summary>
public class SubscriptionGracePeriodJob(IAppDbContext db, IRealtimeNotifier realtime, INotificationService notificationService)
{
    private static readonly TimeZoneInfo SaoPauloTimeZone = ResolveSaoPauloTimeZone();

    public async Task RunAsync(CancellationToken ct = default)
    {
        var today = GetTodayInSaoPaulo();

        var subscriptions = await db.EstablishmentSubscriptions.ToListAsync(ct);
        if (subscriptions.Count == 0)
        {
            return;
        }

        var establishments = await db.Establishments
            .Where(e => subscriptions.Select(s => s.EstablishmentId).Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);

        var newlySuspendedEstablishmentIds = new List<Guid>();
        var newlyWarnedEstablishmentIds = new List<Guid>();

        foreach (var subscription in subscriptions)
        {
            if (!establishments.TryGetValue(subscription.EstablishmentId, out var establishment))
            {
                continue;
            }

            if (subscription.PaymentStatus == SubscriptionPaymentStatus.Active && today > subscription.DueDate)
            {
                subscription.PaymentStatus = SubscriptionPaymentStatus.OverdueGrace;
                establishment.CommercialStatus = CommercialStatus.OverdueGrace;
                newlyWarnedEstablishmentIds.Add(establishment.Id);
            }

            if (subscription.PaymentStatus == SubscriptionPaymentStatus.OverdueGrace &&
                today > subscription.DueDate.AddDays(subscription.GraceDays))
            {
                subscription.PaymentStatus = SubscriptionPaymentStatus.SuspendedPayment;
                establishment.CommercialStatus = CommercialStatus.SuspendedPayment;
                newlySuspendedEstablishmentIds.Add(establishment.Id);

                db.AuditLogs.Add(new AuditLog
                {
                    Action = AuditActions.EstablishmentSuspended,
                    EntityType = nameof(Establishment),
                    EntityId = establishment.Id,
                    MetadataJson = $"{{\"reason\":\"subscription_grace_period_expired\",\"dueDate\":\"{subscription.DueDate:O}\"}}"
                });
            }
        }

        await db.SaveChangesAsync(ct);

        foreach (var establishmentId in newlySuspendedEstablishmentIds)
        {
            await realtime.EstablishmentSuspendedAsync(establishmentId, ct);
        }

        foreach (var establishmentId in newlyWarnedEstablishmentIds.Concat(newlySuspendedEstablishmentIds))
        {
            var adminUserId = await db.EstablishmentUsers
                .Where(eu => eu.EstablishmentId == establishmentId && eu.StaffRole == RoleNames.EstablishmentAdmin)
                .Select(eu => eu.UserId)
                .FirstOrDefaultAsync(ct);
            if (adminUserId == Guid.Empty)
            {
                continue;
            }

            var suspended = newlySuspendedEstablishmentIds.Contains(establishmentId);
            await notificationService.NotifyAsync(adminUserId, NotificationEventType.SubscriptionWarning,
                suspended ? "Assinatura suspensa" : "Assinatura vencida",
                suspended
                    ? "Sua assinatura foi suspensa por falta de pagamento. Novos pedidos estão bloqueados até a regularização."
                    : "Sua mensalidade venceu. Regularize o pagamento para evitar a suspensão da loja.",
                new Dictionary<string, string> { ["establishmentId"] = establishmentId.ToString(), ["eventType"] = nameof(NotificationEventType.SubscriptionWarning) },
                ct);
        }
    }

    private static DateOnly GetTodayInSaoPaulo() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SaoPauloTimeZone));

    private static TimeZoneInfo ResolveSaoPauloTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
    }
}
