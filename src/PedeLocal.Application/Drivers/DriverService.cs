using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Contracts.Common;
using PedeLocal.Contracts.Drivers;
using PedeLocal.Domain.Audit;
using PedeLocal.Domain.Drivers;

namespace PedeLocal.Application.Drivers;

public class DriverService(IAppDbContext db, ICurrentUserService currentUser)
{
    // --- Driver self-service ---

    public async Task<DriverResponse> GetMyProfileAsync(CancellationToken ct)
    {
        var userId = RequireUserId();
        var matches = await LoadWithUserAsync().Where(d => d.Driver.UserId == userId).ToListAsync(ct);
        if (matches.Count == 0)
        {
            throw new NotFoundException("DRIVER_PROFILE_NOT_FOUND", "Perfil de entregador não encontrado.");
        }
        var driver = matches.Single();
        return ToResponse(driver.Driver, driver.Name);
    }

    public async Task<DriverResponse> GoOnlineAsync(CancellationToken ct)
    {
        var driver = await GetOwnDriverAsync(ct);

        if (driver.ApprovalStatus != DriverApprovalStatus.Approved)
        {
            throw new ForbiddenException("DRIVER_NOT_APPROVED", "Seu cadastro ainda não foi aprovado.");
        }
        if (driver.Status == DriverStatus.Blocked)
        {
            throw new ForbiddenException("DRIVER_BLOCKED", "Seu acesso está bloqueado.");
        }
        if (driver.Status == DriverStatus.Busy)
        {
            throw new ConflictException("DRIVER_ON_DELIVERY", "Finalize a entrega atual antes de alterar o status.");
        }

        driver.Status = DriverStatus.Available;
        await db.SaveChangesAsync(ct);
        return ToResponse(driver, null);
    }

    public async Task<DriverResponse> GoOfflineAsync(CancellationToken ct)
    {
        var driver = await GetOwnDriverAsync(ct);

        if (driver.Status == DriverStatus.Busy)
        {
            throw new ConflictException("DRIVER_ON_DELIVERY", "Finalize a entrega atual antes de alterar o status.");
        }

        driver.Status = DriverStatus.Offline;
        await db.SaveChangesAsync(ct);
        return ToResponse(driver, null);
    }

    // --- Admin ---

    public async Task<PagedResult<DriverResponse>> GetAllAsync(string? approvalStatus, PageRequest request, CancellationToken ct)
    {
        var query = LoadWithUserAsync();
        if (!string.IsNullOrWhiteSpace(approvalStatus) && Enum.TryParse<DriverApprovalStatus>(approvalStatus, ignoreCase: true, out var parsed))
        {
            query = query.Where(x => x.Driver.ApprovalStatus == parsed);
        }

        var page = await query.OrderByDescending(x => x.Driver.CreatedAtUtc).ToPagedResultAsync(request, ct);
        return new PagedResult<DriverResponse>(
            page.Items.Select(x => ToResponse(x.Driver, x.Name)).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<DriverResponse> ApproveAsync(Guid driverId, CancellationToken ct)
    {
        var driver = await db.Drivers.SingleOrDefaultAsync(d => d.Id == driverId, ct)
            ?? throw new NotFoundException("DRIVER_NOT_FOUND", "Entregador não encontrado.");

        if (driver.ApprovalStatus != DriverApprovalStatus.Approved)
        {
            driver.ApprovalStatus = DriverApprovalStatus.Approved;
            driver.ApprovedAtUtc = DateTime.UtcNow;
            driver.ApprovedByUserId = currentUser.UserId;

            db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = currentUser.UserId, Action = AuditActions.DriverApproved,
                EntityType = nameof(Domain.Drivers.Driver), EntityId = driver.Id
            });
            await db.SaveChangesAsync(ct);
        }

        var name = await db.Users.Where(u => u.Id == driver.UserId).Select(u => u.Name).SingleAsync(ct);
        return ToResponse(driver, name);
    }

    public async Task<DriverResponse> RejectAsync(Guid driverId, RejectDriverRequest request, CancellationToken ct)
    {
        var driver = await db.Drivers.SingleOrDefaultAsync(d => d.Id == driverId, ct)
            ?? throw new NotFoundException("DRIVER_NOT_FOUND", "Entregador não encontrado.");

        driver.ApprovalStatus = DriverApprovalStatus.Rejected;
        await db.SaveChangesAsync(ct);

        var name = await db.Users.Where(u => u.Id == driver.UserId).Select(u => u.Name).SingleAsync(ct);
        return ToResponse(driver, name);
    }

    public async Task<DriverResponse> SetBlockedAsync(Guid driverId, bool blocked, CancellationToken ct)
    {
        var driver = await db.Drivers.SingleOrDefaultAsync(d => d.Id == driverId, ct)
            ?? throw new NotFoundException("DRIVER_NOT_FOUND", "Entregador não encontrado.");

        driver.Status = blocked ? DriverStatus.Blocked : DriverStatus.Offline;
        await db.SaveChangesAsync(ct);

        var name = await db.Users.Where(u => u.Id == driver.UserId).Select(u => u.Name).SingleAsync(ct);
        return ToResponse(driver, name);
    }

    private async Task<Domain.Drivers.Driver> GetOwnDriverAsync(CancellationToken ct)
    {
        var userId = RequireUserId();
        return await db.Drivers.SingleOrDefaultAsync(d => d.UserId == userId, ct)
            ?? throw new NotFoundException("DRIVER_PROFILE_NOT_FOUND", "Perfil de entregador não encontrado.");
    }

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new UnauthorizedAppException("UNAUTHENTICATED", "Usuário não autenticado.");

    private IQueryable<DriverUserRow> LoadWithUserAsync() =>
        from d in db.Drivers
        join u in db.Users on d.UserId equals u.Id
        select new DriverUserRow { Driver = d, Name = u.Name };

    // EF Core cannot translate ValueTuple projections to SQL (500 on list/profile).
    private sealed class DriverUserRow
    {
        public Domain.Drivers.Driver Driver { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
    }

    private static DriverResponse ToResponse(Domain.Drivers.Driver d, string? name) => new(
        d.Id, d.UserId, name ?? string.Empty, d.CityId, d.Cpf, d.VehicleType.ToString(), d.PixKeyType.ToString(),
        d.PixKey, d.ApprovalStatus.ToString(), d.Status.ToString(), d.ApprovedAtUtc);
}
