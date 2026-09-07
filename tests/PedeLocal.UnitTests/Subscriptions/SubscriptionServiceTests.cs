using PedeLocal.Application.Subscriptions;
using PedeLocal.Contracts.Subscriptions;
using PedeLocal.Domain.Establishments;
using PedeLocal.Domain.Subscriptions;
using PedeLocal.UnitTests.TestSupport;
using Xunit;

namespace PedeLocal.UnitTests.Subscriptions;

public class SubscriptionServiceTests
{
    private static async Task<(PedeLocal.Infrastructure.Persistence.PedeLocalDbContext Db, Guid EstablishmentId, Guid AdminUserId)> SeedAsync(
        CommercialStatus commercialStatus = CommercialStatus.Active)
    {
        var db = InMemoryDbContextFactory.Create();
        var establishment = new Establishment
        {
            CityId = Guid.NewGuid(), Name = "Loja", Slug = "loja", Document = "1", Phone = "1", Email = "a@a.com",
            CategoryId = Guid.NewGuid(), AddressId = Guid.NewGuid(), CommercialStatus = commercialStatus
        };
        db.Establishments.Add(establishment);
        await db.SaveChangesAsync();
        return (db, establishment.Id, Guid.NewGuid());
    }

    [Fact]
    public async Task Upsert_CreatesSubscription()
    {
        var (db, establishmentId, adminUserId) = await SeedAsync();
        var sut = new SubscriptionService(db, new FakeCurrentUserService(adminUserId));

        var response = await sut.UpsertAsync(establishmentId,
            new UpsertSubscriptionRequest(150m, 10, new DateOnly(2026, 10, 10), 5), CancellationToken.None);

        Assert.Equal(150m, response.MonthlyAmount);
        Assert.Equal("Active", response.PaymentStatus);
    }

    [Fact]
    public async Task RegisterPayment_AdvancesDueDate_AndReactivatesSuspendedEstablishment()
    {
        var (db, establishmentId, adminUserId) = await SeedAsync(CommercialStatus.SuspendedPayment);
        var sut = new SubscriptionService(db, new FakeCurrentUserService(adminUserId));
        await sut.UpsertAsync(establishmentId, new UpsertSubscriptionRequest(150m, 10, new DateOnly(2026, 10, 10), 5), CancellationToken.None);
        db.EstablishmentSubscriptions.Single().PaymentStatus = SubscriptionPaymentStatus.SuspendedPayment;
        await db.SaveChangesAsync();

        var payment = await sut.RegisterPaymentAsync(establishmentId,
            new RegisterPaymentRequest(150m, "PIX", DateTime.UtcNow, "pago via pix", null), CancellationToken.None);

        Assert.Equal(150m, payment.Amount);
        var subscription = db.EstablishmentSubscriptions.Single();
        Assert.Equal("Active", subscription.PaymentStatus.ToString());
        Assert.Equal(new DateOnly(2026, 11, 10), subscription.DueDate);

        var establishment = db.Establishments.Single(e => e.Id == establishmentId);
        Assert.Equal(CommercialStatus.Active, establishment.CommercialStatus);

        Assert.Contains(db.AuditLogs, a => a.Action == Domain.Audit.AuditActions.EstablishmentReactivated);
    }
}
