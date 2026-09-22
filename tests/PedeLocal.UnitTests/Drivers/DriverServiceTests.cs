using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Drivers;
using PedeLocal.Contracts.Drivers;
using PedeLocal.Domain.Audit;
using PedeLocal.Domain.Drivers;
using PedeLocal.Domain.Identity;
using PedeLocal.UnitTests.TestSupport;
using Xunit;

namespace PedeLocal.UnitTests.Drivers;

public class DriverServiceTests
{
    private static async Task<(PedeLocal.Infrastructure.Persistence.PedeLocalDbContext Db, Domain.Drivers.Driver Driver, Guid UserId)> SeedAsync(
        DriverOnboardingStatus onboardingStatus = DriverOnboardingStatus.PendingDocuments, DriverStatus status = DriverStatus.Offline,
        bool withDocuments = false, DateOnly? licenseExpiresAt = null)
    {
        var db = InMemoryDbContextFactory.Create();
        var userId = Guid.NewGuid();
        db.Users.Add(new User { Id = userId, Name = "João", Email = "joao@example.com", PhoneNumber = "1", PasswordHash = "x" });
        var driver = new Domain.Drivers.Driver
        {
            UserId = userId, CityId = Guid.NewGuid(), Cpf = "12345678901", VehicleType = VehicleType.Motorcycle,
            PixKeyType = PixKeyType.Cpf, PixKey = "12345678901", OnboardingStatus = onboardingStatus, Status = status
        };
        db.Drivers.Add(driver);

        if (withDocuments)
        {
            db.DriverLicenses.Add(new DriverLicense
            {
                DriverId = driver.Id, RegistrationNumber = "12345678901", Category = "A",
                ExpiresAt = licenseExpiresAt ?? DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1))
            });
            db.DriverVehicles.Add(new DriverVehicle
            {
                DriverId = driver.Id, Type = VehicleType.Motorcycle, Plate = "ABC1234", Model = "CG 160", Color = "Preto"
            });
        }

        await db.SaveChangesAsync();
        return (db, driver, userId);
    }

    [Fact]
    public async Task Approve_WithoutLicense_Throws()
    {
        var (db, driver, _) = await SeedAsync(DriverOnboardingStatus.UnderReview);
        var sut = new DriverService(db, new FakeCurrentUserService(Guid.NewGuid(), RoleNames.SuperAdmin));

        var ex = await Assert.ThrowsAsync<AppException>(() => sut.ApproveAsync(driver.Id, CancellationToken.None));
        Assert.Equal("DRIVER_LICENSE_REQUIRED", ex.Code);
    }

    [Fact]
    public async Task Approve_WithExpiredLicense_Throws()
    {
        var (db, driver, _) = await SeedAsync(DriverOnboardingStatus.UnderReview, withDocuments: true,
            licenseExpiresAt: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));
        var sut = new DriverService(db, new FakeCurrentUserService(Guid.NewGuid(), RoleNames.SuperAdmin));

        var ex = await Assert.ThrowsAsync<AppException>(() => sut.ApproveAsync(driver.Id, CancellationToken.None));
        Assert.Equal("DRIVER_DOCUMENT_EXPIRED", ex.Code);
    }

    [Fact]
    public async Task Approve_WithValidDocuments_SetsApprovedAndTimestamp()
    {
        var (db, driver, _) = await SeedAsync(DriverOnboardingStatus.UnderReview, withDocuments: true);
        var sut = new DriverService(db, new FakeCurrentUserService(Guid.NewGuid(), RoleNames.SuperAdmin));

        var response = await sut.ApproveAsync(driver.Id, CancellationToken.None);

        Assert.Equal("Approved", response.OnboardingStatus);
        Assert.Equal("Eligible", response.EligibilityStatus);
        Assert.NotNull(response.ReviewedAtUtc);
    }

    [Fact]
    public async Task Approve_IsIdempotent_DoesNotDuplicateAuditLog()
    {
        var (db, driver, _) = await SeedAsync(DriverOnboardingStatus.UnderReview, withDocuments: true);
        var sut = new DriverService(db, new FakeCurrentUserService(Guid.NewGuid(), RoleNames.SuperAdmin));

        await sut.ApproveAsync(driver.Id, CancellationToken.None);
        await sut.ApproveAsync(driver.Id, CancellationToken.None);

        Assert.Single(db.AuditLogs);
    }

    [Fact]
    public async Task GoOnline_WhenPendingDocuments_Throws()
    {
        var (db, driver, userId) = await SeedAsync();
        var sut = new DriverService(db, new FakeCurrentUserService(userId));

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GoOnlineAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GoOnline_WhenApprovedWithValidLicense_SetsAvailable()
    {
        var (db, driver, userId) = await SeedAsync(DriverOnboardingStatus.Approved, withDocuments: true);
        var sut = new DriverService(db, new FakeCurrentUserService(userId));

        var response = await sut.GoOnlineAsync(CancellationToken.None);

        Assert.Equal("Available", response.Status);
    }

    [Fact]
    public async Task GoOnline_WhenApprovedButLicenseExpired_Throws()
    {
        var (db, driver, userId) = await SeedAsync(DriverOnboardingStatus.Approved, withDocuments: true,
            licenseExpiresAt: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));
        var sut = new DriverService(db, new FakeCurrentUserService(userId));

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => sut.GoOnlineAsync(CancellationToken.None));
        Assert.Equal("DRIVER_DOCUMENT_EXPIRED", ex.Code);
    }

    [Fact]
    public async Task GoOnline_WhenBlocked_Throws()
    {
        var (db, driver, userId) = await SeedAsync(DriverOnboardingStatus.Approved, DriverStatus.Blocked, withDocuments: true);
        var sut = new DriverService(db, new FakeCurrentUserService(userId));

        await Assert.ThrowsAsync<ForbiddenException>(() => sut.GoOnlineAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GoOffline_WhenBusy_Throws()
    {
        var (db, driver, userId) = await SeedAsync(DriverOnboardingStatus.Approved, DriverStatus.Busy, withDocuments: true);
        var sut = new DriverService(db, new FakeCurrentUserService(userId));

        await Assert.ThrowsAsync<ConflictException>(() => sut.GoOfflineAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Reject_SetsRejectedStatusAndPersistsReasonAndAudits()
    {
        var (db, driver, _) = await SeedAsync(DriverOnboardingStatus.UnderReview, withDocuments: true);
        var sut = new DriverService(db, new FakeCurrentUserService(Guid.NewGuid(), RoleNames.SuperAdmin));

        var response = await sut.RejectAsync(driver.Id, new RejectDriverRequest("Documentos inválidos"), CancellationToken.None);

        Assert.Equal("Rejected", response.OnboardingStatus);
        Assert.Equal("Documentos inválidos", response.RejectionReason);
        Assert.Contains(db.AuditLogs, a => a.Action == AuditActions.DriverRejected);
    }

    [Fact]
    public async Task Block_ThenUnblock_TogglesStatusAndAudits()
    {
        var (db, driver, _) = await SeedAsync(DriverOnboardingStatus.Approved, withDocuments: true);
        var sut = new DriverService(db, new FakeCurrentUserService(Guid.NewGuid(), RoleNames.SuperAdmin));

        var blocked = await sut.SetBlockedAsync(driver.Id, true, CancellationToken.None);
        Assert.Equal("Blocked", blocked.Status);

        var unblocked = await sut.SetBlockedAsync(driver.Id, false, CancellationToken.None);
        Assert.Equal("Offline", unblocked.Status);

        Assert.Contains(db.AuditLogs, a => a.Action == AuditActions.DriverBlocked);
        Assert.Contains(db.AuditLogs, a => a.Action == AuditActions.DriverUnblocked);
    }

    [Fact]
    public async Task SubmitLicenseThenVehicle_MovesPendingDocumentsToUnderReview()
    {
        var (db, driver, userId) = await SeedAsync();
        var sut = new DriverService(db, new FakeCurrentUserService(userId));

        await sut.SubmitLicenseAsync(new SubmitDriverLicenseRequest("12345678901", "A",
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)), true, null), CancellationToken.None);
        var afterLicenseOnly = await sut.GetMyProfileAsync(CancellationToken.None);
        Assert.Equal("PendingDocuments", afterLicenseOnly.OnboardingStatus);

        var afterVehicle = await sut.SubmitVehicleAsync(new SubmitDriverVehicleRequest("Motorcycle", "ABC1234", "CG 160", "Preto", null), CancellationToken.None);

        Assert.Equal("UnderReview", afterVehicle.Driver.OnboardingStatus);
    }

    [Fact]
    public async Task SubmitVehicle_ChangingTypeAfterApproval_RevertsToUnderReview()
    {
        var (db, driver, userId) = await SeedAsync(DriverOnboardingStatus.Approved, withDocuments: true);
        var sut = new DriverService(db, new FakeCurrentUserService(userId));

        var response = await sut.SubmitVehicleAsync(new SubmitDriverVehicleRequest("Car", null, "Onix", "Prata", null), CancellationToken.None);

        Assert.Equal("UnderReview", response.Driver.OnboardingStatus);
    }
}
