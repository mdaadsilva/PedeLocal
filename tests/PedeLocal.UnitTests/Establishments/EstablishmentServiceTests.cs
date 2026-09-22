using PedeLocal.Application.Common;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Establishments;
using PedeLocal.Contracts.Common;
using PedeLocal.Contracts.Establishments;
using PedeLocal.Domain.Cities;
using PedeLocal.Domain.Establishments;
using PedeLocal.Domain.Identity;
using PedeLocal.Infrastructure.Identity;
using PedeLocal.UnitTests.TestSupport;
using Xunit;

namespace PedeLocal.UnitTests.Establishments;

public class EstablishmentServiceTests
{
    private static async Task<(EstablishmentService Sut, PedeLocal.Infrastructure.Persistence.PedeLocalDbContext Db, City City, EstablishmentCategory Category)> CreateSutAsync()
    {
        var db = InMemoryDbContextFactory.Create();
        var city = new City { Name = "Guaimbê", State = "SP", IsActive = true };
        var category = new EstablishmentCategory { Name = "Lanchonete" };
        db.Cities.Add(city);
        db.EstablishmentCategories.Add(category);
        await db.SaveChangesAsync();

        var superAdmin = new FakeCurrentUserService(Guid.NewGuid(), RoleNames.SuperAdmin);
        return (new EstablishmentService(db, new BCryptPasswordHasher(), new EstablishmentAccessGuard(db, superAdmin)), db, city, category);
    }

    private static CreateEstablishmentRequest BuildRequest(Guid cityId, Guid categoryId, string adminEmail = "admin@lanchonete.com") =>
        new(cityId, "Lanchonete do Zé", "12345678000199", "14999990000", "contato@lanchonete.com", categoryId,
            "Rua A", "100", "Centro", null, "17370000", -21.8m, -49.9m,
            "Zé", adminEmail, "14999990001", "Senha123");

    [Fact]
    public async Task Create_CreatesEstablishmentAddressAndAdminUser()
    {
        var (sut, db, city, category) = await CreateSutAsync();

        var response = await sut.CreateAsync(BuildRequest(city.Id, category.Id), CancellationToken.None);

        Assert.Equal("lanchonete-do-ze", response.Slug);
        Assert.NotEqual(Guid.Empty, response.AddressId);
        Assert.Single(db.Users);
        Assert.Single(db.EstablishmentUsers);
        Assert.Single(db.EstablishmentSettings);
    }

    [Fact]
    public async Task Create_DuplicateAdminEmail_Throws()
    {
        var (sut, _, city, category) = await CreateSutAsync();
        await sut.CreateAsync(BuildRequest(city.Id, category.Id), CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.CreateAsync(BuildRequest(city.Id, category.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Create_SameNameTwice_GeneratesDistinctSlugs()
    {
        var (sut, _, city, category) = await CreateSutAsync();
        var first = await sut.CreateAsync(BuildRequest(city.Id, category.Id, "admin1@lanchonete.com"), CancellationToken.None);
        var second = await sut.CreateAsync(BuildRequest(city.Id, category.Id, "admin2@lanchonete.com"), CancellationToken.None);

        Assert.NotEqual(first.Slug, second.Slug);
    }

    [Fact]
    public async Task SearchForCustomers_HidesSuspendedEstablishments()
    {
        var (sut, db, city, category) = await CreateSutAsync();
        var created = await sut.CreateAsync(BuildRequest(city.Id, category.Id), CancellationToken.None);
        await sut.SetStatusAsync(created.Id, new UpdateEstablishmentStatusRequest(null, "SuspendedPayment"), CancellationToken.None);

        var results = await sut.SearchForCustomersAsync(city.Id, new PageRequest(), CancellationToken.None);

        Assert.DoesNotContain(results.Items, e => e.Id == created.Id);
    }

    [Fact]
    public async Task SearchForCustomers_ShowsActiveEstablishments()
    {
        var (sut, _, city, category) = await CreateSutAsync();
        var created = await sut.CreateAsync(BuildRequest(city.Id, category.Id), CancellationToken.None);

        var results = await sut.SearchForCustomersAsync(city.Id, new PageRequest(), CancellationToken.None);

        Assert.Contains(results.Items, e => e.Id == created.Id);
    }

    // ---------------------------------------------------------------
    // Pause / resume (section 12)
    // ---------------------------------------------------------------

    [Fact]
    public async Task Pause_ByStaff_SetsOperationalStatusPausedWithReason()
    {
        var (adminSut, db, city, category) = await CreateSutAsync();
        var created = await adminSut.CreateAsync(BuildRequest(city.Id, category.Id), CancellationToken.None);
        var staffUserId = db.EstablishmentUsers.Single(eu => eu.EstablishmentId == created.Id).UserId;

        var staffSut = new EstablishmentService(db, new BCryptPasswordHasher(), new EstablishmentAccessGuard(db, new FakeCurrentUserService(staffUserId)));
        var response = await staffSut.PauseAsync(created.Id, new PauseEstablishmentRequest("HighDemand", null), staffUserId, CancellationToken.None);

        Assert.Equal("Paused", response.OperationalStatus);
        Assert.Equal("HighDemand", response.PauseReason);
        Assert.NotNull(response.PausedAtUtc);
    }

    [Fact]
    public async Task Pause_ByUnrelatedUser_Throws()
    {
        var (adminSut, db, city, category) = await CreateSutAsync();
        var created = await adminSut.CreateAsync(BuildRequest(city.Id, category.Id), CancellationToken.None);

        var strangerSut = new EstablishmentService(db, new BCryptPasswordHasher(), new EstablishmentAccessGuard(db, new FakeCurrentUserService(Guid.NewGuid())));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            strangerSut.PauseAsync(created.Id, new PauseEstablishmentRequest("Other", null), Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Resume_ClearsAllPauseFields()
    {
        var (adminSut, db, city, category) = await CreateSutAsync();
        var created = await adminSut.CreateAsync(BuildRequest(city.Id, category.Id), CancellationToken.None);
        await adminSut.PauseAsync(created.Id, new PauseEstablishmentRequest("TemporaryClosure", null), Guid.NewGuid(), CancellationToken.None);

        var resumed = await adminSut.ResumeAsync(created.Id, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal("Open", resumed.OperationalStatus);
        Assert.Null(resumed.PauseReason);
        Assert.Null(resumed.PausedAtUtc);
        Assert.Null(resumed.PausedUntilUtc);
    }

    [Fact]
    public async Task Pause_WithPastPausedUntil_Throws()
    {
        var (adminSut, _, city, category) = await CreateSutAsync();
        var created = await adminSut.CreateAsync(BuildRequest(city.Id, category.Id), CancellationToken.None);

        await Assert.ThrowsAsync<AppException>(() =>
            adminSut.PauseAsync(created.Id, new PauseEstablishmentRequest("Other", DateTime.UtcNow.AddMinutes(-5)), Guid.NewGuid(), CancellationToken.None));
    }
}
