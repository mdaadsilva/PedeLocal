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
        var driver = await GetOwnDriverAsync(ct);
        var name = await db.Users.Where(u => u.Id == driver.UserId).Select(u => u.Name).SingleAsync(ct);
        var license = await db.DriverLicenses.SingleOrDefaultAsync(l => l.DriverId == driver.Id, ct);
        return ToResponse(driver, name, license);
    }

    public async Task<DriverDetailResponse> GetMyDocumentsAsync(CancellationToken ct)
    {
        var driver = await GetOwnDriverAsync(ct);
        return await BuildDetailAsync(driver, ct);
    }

    public async Task<DriverDetailResponse> SubmitLicenseAsync(SubmitDriverLicenseRequest request, CancellationToken ct)
    {
        var driver = await GetOwnDriverAsync(ct);

        var license = await db.DriverLicenses.SingleOrDefaultAsync(l => l.DriverId == driver.Id, ct);
        if (license is null)
        {
            license = new DriverLicense { DriverId = driver.Id };
            db.DriverLicenses.Add(license);
        }

        license.RegistrationNumber = request.RegistrationNumber;
        license.Category = request.Category;
        license.ExpiresAt = request.ExpiresAt;
        license.HasEar = request.HasEar;
        license.DocumentImageUrl = request.DocumentImageUrl;

        var hasVehicle = await db.DriverVehicles.AnyAsync(v => v.DriverId == driver.Id, ct);
        AdvanceOnboardingAfterSubmission(driver, hasLicense: true, hasVehicle);
        await db.SaveChangesAsync(ct);
        return await BuildDetailAsync(driver, ct);
    }

    public async Task<DriverDetailResponse> SubmitVehicleAsync(SubmitDriverVehicleRequest request, CancellationToken ct)
    {
        var driver = await GetOwnDriverAsync(ct);

        if (!Enum.TryParse<VehicleType>(request.Type, ignoreCase: true, out var vehicleType))
        {
            throw new AppException("INVALID_VEHICLE_TYPE", "Tipo de veículo inválido.");
        }

        var vehicle = await db.DriverVehicles.SingleOrDefaultAsync(v => v.DriverId == driver.Id, ct);
        var typeChanged = vehicle is not null && vehicle.Type != vehicleType;
        if (vehicle is null)
        {
            vehicle = new DriverVehicle { DriverId = driver.Id };
            db.DriverVehicles.Add(vehicle);
        }

        vehicle.Type = vehicleType;
        vehicle.Plate = request.Plate;
        vehicle.Model = request.Model;
        vehicle.Color = request.Color;
        vehicle.DocumentImageUrl = request.DocumentImageUrl;
        driver.VehicleType = vehicleType;

        // Changing the vehicle type after approval materially changes delivery capability (e.g.
        // Bike -> Car), so it re-enters review rather than silently keeping the old approval.
        if (typeChanged && driver.OnboardingStatus == DriverOnboardingStatus.Approved)
        {
            driver.OnboardingStatus = DriverOnboardingStatus.UnderReview;
        }

        var hasLicense = await db.DriverLicenses.AnyAsync(l => l.DriverId == driver.Id, ct);
        AdvanceOnboardingAfterSubmission(driver, hasLicense, hasVehicle: true);
        await db.SaveChangesAsync(ct);
        return await BuildDetailAsync(driver, ct);
    }

    public async Task<DriverResponse> GoOnlineAsync(CancellationToken ct)
    {
        var driver = await GetOwnDriverAsync(ct);

        if (driver.Status == DriverStatus.Busy)
        {
            throw new ConflictException("DRIVER_ON_DELIVERY", "Finalize a entrega atual antes de alterar o status.");
        }

        var license = await db.DriverLicenses.SingleOrDefaultAsync(l => l.DriverId == driver.Id, ct);
        EnsureEligible(driver, license);

        driver.Status = DriverStatus.Available;
        await db.SaveChangesAsync(ct);
        return ToResponse(driver, null, license);
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
        return ToResponse(driver, null, null);
    }

    // --- Admin ---

    public async Task<PagedResult<DriverResponse>> GetAllAsync(string? onboardingStatus, PageRequest request, CancellationToken ct)
    {
        var query = LoadWithUserAsync();
        if (!string.IsNullOrWhiteSpace(onboardingStatus) && Enum.TryParse<DriverOnboardingStatus>(onboardingStatus, ignoreCase: true, out var parsed))
        {
            query = query.Where(x => x.Driver.OnboardingStatus == parsed);
        }

        var page = await query.OrderByDescending(x => x.Driver.CreatedAtUtc).ToPagedResultAsync(request, ct);
        var driverIds = page.Items.Select(x => x.Driver.Id).ToList();
        var licenses = await db.DriverLicenses.Where(l => driverIds.Contains(l.DriverId)).ToDictionaryAsync(l => l.DriverId, ct);

        return new PagedResult<DriverResponse>(
            page.Items.Select(x => ToResponse(x.Driver, x.Name, licenses.GetValueOrDefault(x.Driver.Id))).ToList(),
            page.Page, page.PageSize, page.TotalCount);
    }

    /// <summary>Single detail endpoint covering both "view driver details" and "view required
    /// documentation" — the CNH/vehicle data submitted so far is part of the same payload rather
    /// than a separate route, since a SUPER_ADMIN reviewing a driver always needs both together.</summary>
    public async Task<DriverDetailResponse> GetByIdAsync(Guid driverId, CancellationToken ct)
    {
        var driver = await db.Drivers.SingleOrDefaultAsync(d => d.Id == driverId, ct)
            ?? throw new NotFoundException("DRIVER_NOT_FOUND", "Entregador não encontrado.");
        return await BuildDetailAsync(driver, ct);
    }

    public async Task<DriverResponse> ApproveAsync(Guid driverId, CancellationToken ct)
    {
        var driver = await db.Drivers.SingleOrDefaultAsync(d => d.Id == driverId, ct)
            ?? throw new NotFoundException("DRIVER_NOT_FOUND", "Entregador não encontrado.");

        if (driver.OnboardingStatus != DriverOnboardingStatus.Approved)
        {
            var license = await db.DriverLicenses.SingleOrDefaultAsync(l => l.DriverId == driver.Id, ct)
                ?? throw new AppException("DRIVER_LICENSE_REQUIRED", "Entregador ainda não enviou os dados da CNH.");
            if (!await db.DriverVehicles.AnyAsync(v => v.DriverId == driver.Id, ct))
            {
                throw new AppException("DRIVER_VEHICLE_REQUIRED", "Entregador ainda não enviou os dados do veículo.");
            }
            if (license.ExpiresAt < DateOnly.FromDateTime(DateTime.UtcNow))
            {
                throw new AppException("DRIVER_DOCUMENT_EXPIRED", "A CNH enviada está vencida.");
            }

            driver.OnboardingStatus = DriverOnboardingStatus.Approved;
            driver.ReviewedAtUtc = DateTime.UtcNow;
            driver.ReviewedByUserId = currentUser.UserId;
            driver.RejectionReason = null;

            db.AuditLogs.Add(new AuditLog
            {
                ActorUserId = currentUser.UserId, Action = AuditActions.DriverApproved,
                EntityType = nameof(Domain.Drivers.Driver), EntityId = driver.Id
            });
            await db.SaveChangesAsync(ct);
        }

        var name = await db.Users.Where(u => u.Id == driver.UserId).Select(u => u.Name).SingleAsync(ct);
        var savedLicense = await db.DriverLicenses.SingleOrDefaultAsync(l => l.DriverId == driver.Id, ct);
        return ToResponse(driver, name, savedLicense);
    }

    public async Task<DriverResponse> RejectAsync(Guid driverId, RejectDriverRequest request, CancellationToken ct)
    {
        var driver = await db.Drivers.SingleOrDefaultAsync(d => d.Id == driverId, ct)
            ?? throw new NotFoundException("DRIVER_NOT_FOUND", "Entregador não encontrado.");

        driver.OnboardingStatus = DriverOnboardingStatus.Rejected;
        driver.ReviewedAtUtc = DateTime.UtcNow;
        driver.ReviewedByUserId = currentUser.UserId;
        driver.RejectionReason = request.Reason;

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = currentUser.UserId, Action = AuditActions.DriverRejected,
            EntityType = nameof(Domain.Drivers.Driver), EntityId = driver.Id,
            MetadataJson = request.Reason is null ? null : System.Text.Json.JsonSerializer.Serialize(new { reason = request.Reason })
        });
        await db.SaveChangesAsync(ct);

        var name = await db.Users.Where(u => u.Id == driver.UserId).Select(u => u.Name).SingleAsync(ct);
        var license = await db.DriverLicenses.SingleOrDefaultAsync(l => l.DriverId == driver.Id, ct);
        return ToResponse(driver, name, license);
    }

    public async Task<DriverResponse> SetBlockedAsync(Guid driverId, bool blocked, CancellationToken ct)
    {
        var driver = await db.Drivers.SingleOrDefaultAsync(d => d.Id == driverId, ct)
            ?? throw new NotFoundException("DRIVER_NOT_FOUND", "Entregador não encontrado.");

        driver.Status = blocked ? DriverStatus.Blocked : DriverStatus.Offline;

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = currentUser.UserId, Action = blocked ? AuditActions.DriverBlocked : AuditActions.DriverUnblocked,
            EntityType = nameof(Domain.Drivers.Driver), EntityId = driver.Id
        });
        await db.SaveChangesAsync(ct);

        var name = await db.Users.Where(u => u.Id == driver.UserId).Select(u => u.Name).SingleAsync(ct);
        var license = await db.DriverLicenses.SingleOrDefaultAsync(l => l.DriverId == driver.Id, ct);
        return ToResponse(driver, name, license);
    }

    /// <summary>Moves PendingDocuments/Rejected to UnderReview once both documents are on file.
    /// Takes hasLicense/hasVehicle as booleans rather than querying both from the DB here: the one
    /// just submitted in this call is only staged (Added), not yet saved, so an AnyAsync query
    /// against that same DbSet would miss it — the caller already knows that one is true.</summary>
    private static void AdvanceOnboardingAfterSubmission(Domain.Drivers.Driver driver, bool hasLicense, bool hasVehicle)
    {
        if (driver.OnboardingStatus is not (DriverOnboardingStatus.PendingDocuments or DriverOnboardingStatus.Rejected))
        {
            return;
        }

        if (hasLicense && hasVehicle)
        {
            driver.OnboardingStatus = DriverOnboardingStatus.UnderReview;
        }
    }

    private async Task<DriverDetailResponse> BuildDetailAsync(Domain.Drivers.Driver driver, CancellationToken ct)
    {
        var name = await db.Users.Where(u => u.Id == driver.UserId).Select(u => u.Name).SingleAsync(ct);
        var license = await db.DriverLicenses.SingleOrDefaultAsync(l => l.DriverId == driver.Id, ct);
        var vehicle = await db.DriverVehicles.SingleOrDefaultAsync(v => v.DriverId == driver.Id, ct);

        return new DriverDetailResponse(
            ToResponse(driver, name, license),
            license is null ? null : ToLicenseResponse(license),
            vehicle is null ? null : ToVehicleResponse(vehicle));
    }

    private static void EnsureEligible(Domain.Drivers.Driver driver, DriverLicense? license)
    {
        var eligibility = DriverEligibilityService.Evaluate(driver, license, DateTime.UtcNow);
        if (eligibility != DriverEligibilityStatus.Eligible)
        {
            throw eligibility switch
            {
                DriverEligibilityStatus.Blocked => new ForbiddenException("DRIVER_BLOCKED", "Seu acesso está bloqueado."),
                DriverEligibilityStatus.DocumentExpired => new ForbiddenException("DRIVER_DOCUMENT_EXPIRED", "Sua CNH está vencida. Atualize seus documentos."),
                _ => new ForbiddenException("DRIVER_NOT_APPROVED", "Seu cadastro ainda não foi aprovado.")
            };
        }
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

    private static DriverResponse ToResponse(Domain.Drivers.Driver d, string? name, DriverLicense? license) => new(
        d.Id, d.UserId, name ?? string.Empty, d.CityId, d.Cpf, d.VehicleType.ToString(), d.PixKeyType.ToString(),
        d.PixKey, d.OnboardingStatus.ToString(), d.Status.ToString(),
        DriverEligibilityService.Evaluate(d, license, DateTime.UtcNow).ToString(),
        d.ReviewedAtUtc, d.RejectionReason);

    private static DriverLicenseResponse ToLicenseResponse(DriverLicense l) => new(
        l.RegistrationNumber, l.Category, l.ExpiresAt, l.HasEar, l.DocumentImageUrl,
        l.ExpiresAt.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber);

    private static DriverVehicleResponse ToVehicleResponse(DriverVehicle v) => new(
        v.Type.ToString(), v.Plate, v.Model, v.Color, v.DocumentImageUrl);
}
