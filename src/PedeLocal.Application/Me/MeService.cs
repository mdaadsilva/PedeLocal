using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Contracts.Common;
using PedeLocal.Contracts.Me;

namespace PedeLocal.Application.Me;

/// <summary>Self-service account endpoints, including the LGPD baseline from docs section 64.</summary>
public class MeService(IAppDbContext db, ICurrentUserService currentUser)
{
    public async Task<MyProfileResponse> GetProfileAsync(CancellationToken ct)
    {
        var user = await LoadOwnUserAsync(ct);
        var roles = await db.UserRoles.Where(ur => ur.UserId == user.Id).Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name).ToListAsync(ct);
        return new MyProfileResponse(user.Id, user.Name, user.Email, user.PhoneNumber, roles, user.CreatedAtUtc);
    }

    public async Task<MyProfileResponse> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await LoadOwnUserAsync(ct);
        user.Name = request.Name.Trim();
        user.PhoneNumber = request.Phone.Trim();
        await db.SaveChangesAsync(ct);
        return await GetProfileAsync(ct);
    }

    public async Task UpdateFcmTokenAsync(UpdateFcmTokenRequest request, CancellationToken ct)
    {
        var user = await LoadOwnUserAsync(ct);
        user.FcmToken = string.IsNullOrWhiteSpace(request.Token) ? null : request.Token.Trim();
        await db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<NotificationResponse>> GetNotificationsAsync(PageRequest request, CancellationToken ct)
    {
        var userId = RequireUserId();
        return await db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Select(n => new NotificationResponse(n.Id, n.Type.ToString(), n.Title, n.Body, n.DataJson, n.IsRead, n.CreatedAtUtc, n.SentAtUtc))
            .ToPagedResultAsync(request, ct);
    }

    public async Task MarkNotificationReadAsync(Guid notificationId, CancellationToken ct)
    {
        var userId = RequireUserId();
        var notification = await db.Notifications.SingleOrDefaultAsync(n => n.Id == notificationId, ct)
            ?? throw new NotFoundException("NOTIFICATION_NOT_FOUND", "Notificação não encontrada.");

        if (notification.UserId != userId)
        {
            throw new ForbiddenException("NOTIFICATION_ACCESS_DENIED", "Você não tem acesso a esta notificação.");
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<MyDataExportResponse> GetMyDataAsync(CancellationToken ct)
    {
        var user = await LoadOwnUserAsync(ct);
        var roles = await db.UserRoles.Where(ur => ur.UserId == user.Id).Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name).ToListAsync(ct);
        var addressCount = await db.Addresses.CountAsync(a => a.UserId == user.Id, ct);
        var orderCount = await db.Orders.CountAsync(o => o.CustomerUserId == user.Id, ct);

        return new MyDataExportResponse(user.Id, user.Name, user.Email, user.PhoneNumber, roles, user.CreatedAtUtc, addressCount, orderCount);
    }

    /// <summary>Anonymizes and deactivates the account instead of hard-deleting it: orders,
    /// audit entries, and financial records referencing this user must be retained for legitimate
    /// business/legal reasons (docs section 64 — "a exclusão deve respeitar retenções legítimas").</summary>
    public async Task DeleteAccountAsync(CancellationToken ct)
    {
        var user = await LoadOwnUserAsync(ct);

        user.Name = "Usuário removido";
        user.Email = $"deleted-{user.Id}@pedelocal.invalid";
        user.PhoneNumber = "";
        user.PasswordHash = Guid.NewGuid().ToString("N");
        user.FcmToken = null;
        user.IsActive = false;
        user.DeletedAtUtc = DateTime.UtcNow;

        var activeSessions = await db.RefreshTokens.Where(rt => rt.UserId == user.Id && rt.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var session in activeSessions)
        {
            session.RevokedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<Domain.Identity.User> LoadOwnUserAsync(CancellationToken ct)
    {
        var userId = RequireUserId();
        return await db.Users.SingleOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("USER_NOT_FOUND", "Usuário não encontrado.");
    }

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new UnauthorizedAppException("UNAUTHENTICATED", "Usuário não autenticado.");
}
