using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Contracts.Admin;
using PedeLocal.Contracts.Common;
using PedeLocal.Domain.Audit;
using PedeLocal.Domain.Features;
using PedeLocal.Domain.Identity;

namespace PedeLocal.Application.Admin;

/// <summary>Read-mostly aggregation endpoints for the Super Admin (docs section 54).</summary>
public class AdminService(IAppDbContext db, ICurrentUserService currentUser)
{
    public async Task<PagedResult<CustomerResponse>> GetCustomersAsync(PageRequest request, CancellationToken ct)
    {
        var query =
            from u in db.Users
            join ur in db.UserRoles on u.Id equals ur.UserId
            join r in db.Roles on ur.RoleId equals r.Id
            where r.Name == RoleNames.Customer
            orderby u.CreatedAtUtc descending
            select new CustomerResponse(u.Id, u.Name, u.Email, u.PhoneNumber, u.IsActive, u.CreatedAtUtc);

        return await query.ToPagedResultAsync(request, ct);
    }

    public async Task<PagedResult<AuditLogResponse>> GetAuditLogsAsync(
        string? entityType, Guid? actorUserId, PageRequest request, CancellationToken ct)
    {
        var query = db.AuditLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(a => a.EntityType == entityType);
        }
        if (actorUserId is { } id)
        {
            query = query.Where(a => a.ActorUserId == id);
        }

        return await query.OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new AuditLogResponse(a.Id, a.ActorUserId, a.Action, a.EntityType, a.EntityId, a.Ip, a.UserAgent, a.MetadataJson, a.CreatedAtUtc))
            .ToPagedResultAsync(request, ct);
    }

    public async Task<List<EstablishmentFeatureResponse>> GetFeaturesAsync(Guid establishmentId, CancellationToken ct) =>
        await db.EstablishmentFeatures
            .Where(f => f.EstablishmentId == establishmentId)
            .Select(f => ToFeatureResponse(f))
            .ToListAsync(ct);

    public async Task<EstablishmentFeatureResponse> SetFeatureAsync(Guid establishmentId, string featureKey, SetFeatureRequest request, CancellationToken ct)
    {
        var feature = await db.EstablishmentFeatures
            .SingleOrDefaultAsync(f => f.EstablishmentId == establishmentId && f.FeatureKey == featureKey, ct);

        if (feature is null)
        {
            feature = new EstablishmentFeature { EstablishmentId = establishmentId, FeatureKey = featureKey };
            db.EstablishmentFeatures.Add(feature);
        }

        feature.Enabled = request.Enabled;
        feature.SettingsJson = request.SettingsJson;

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = currentUser.UserId, Action = AuditActions.FeatureChanged,
            EntityType = nameof(Domain.Establishments.Establishment), EntityId = establishmentId,
            MetadataJson = $"{{\"featureKey\":\"{featureKey}\",\"enabled\":{request.Enabled.ToString().ToLowerInvariant()}}}"
        });

        await db.SaveChangesAsync(ct);
        return ToFeatureResponse(feature);
    }

    private static EstablishmentFeatureResponse ToFeatureResponse(EstablishmentFeature f) =>
        new(f.Id, f.EstablishmentId, f.FeatureKey, f.Enabled, f.SettingsJson);
}
