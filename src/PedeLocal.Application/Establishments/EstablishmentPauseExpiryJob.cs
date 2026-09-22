using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Domain.Audit;
using PedeLocal.Domain.Establishments;

namespace PedeLocal.Application.Establishments;

/// <summary>Auto-resumes establishments whose pause has an expiry (PausedUntilUtc) that has already
/// passed. Runs every 15 minutes (Program.cs) rather than daily like SubscriptionGracePeriodJob,
/// since a pause is a short-lived operational thing ("alto movimento", "sem entregador") measured in
/// hours, not days. Order creation itself never actually depends on this job having run —
/// Establishment.IsOperationallyOpen already treats an expired pause as over immediately — this job
/// only exists so OperationalStatus and the pause fields don't sit stale in the merchant/admin
/// dashboards after the expiry passes.</summary>
public class EstablishmentPauseExpiryJob(IAppDbContext db)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expired = await db.Establishments
            .Where(e => e.OperationalStatus == OperationalStatus.Paused && e.PausedUntilUtc != null && e.PausedUntilUtc <= now)
            .ToListAsync(ct);

        if (expired.Count == 0)
        {
            return;
        }

        foreach (var establishment in expired)
        {
            establishment.OperationalStatus = OperationalStatus.Open;
            establishment.PauseReason = null;
            establishment.PausedAtUtc = null;
            establishment.PausedUntilUtc = null;
            establishment.PausedByUserId = null;

            db.AuditLogs.Add(new AuditLog
            {
                Action = AuditActions.EstablishmentResumed,
                EntityType = nameof(Establishment),
                EntityId = establishment.Id,
                MetadataJson = "{\"reason\":\"pause_expired\"}"
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
