using PedeLocal.Application.Subscriptions;
using PedeLocal.Domain.Establishments;
using PedeLocal.Domain.Subscriptions;
using PedeLocal.UnitTests.TestSupport;
using Xunit;

namespace PedeLocal.UnitTests.Subscriptions;

public class SubscriptionGracePeriodJobTests
{
    private static DateOnly TodayInSaoPaulo()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
    }

    private static async Task<(PedeLocal.Infrastructure.Persistence.PedeLocalDbContext Db, Establishment Establishment, EstablishmentSubscription Subscription)> SeedAsync(
        DateOnly dueDate, SubscriptionPaymentStatus status = SubscriptionPaymentStatus.Active, int graceDays = 5)
    {
        var db = InMemoryDbContextFactory.Create();
        var establishment = new Establishment
        {
            CityId = Guid.NewGuid(), Name = "Loja", Slug = "loja", Document = "1", Phone = "1", Email = "a@a.com",
            CategoryId = Guid.NewGuid(), AddressId = Guid.NewGuid(), CommercialStatus = CommercialStatus.Active
        };
        db.Establishments.Add(establishment);

        var subscription = new EstablishmentSubscription
        {
            EstablishmentId = establishment.Id, MonthlyAmount = 150m, BillingDay = dueDate.Day,
            DueDate = dueDate, GraceDays = graceDays, PaymentStatus = status
        };
        db.EstablishmentSubscriptions.Add(subscription);
        await db.SaveChangesAsync();

        return (db, establishment, subscription);
    }

    [Fact]
    public async Task BeforeDueDate_StaysActive()
    {
        var (db, establishment, subscription) = await SeedAsync(TodayInSaoPaulo().AddDays(5));
        var sut = new SubscriptionGracePeriodJob(db, new NoOpRealtimeNotifier(), new NoOpNotificationService());

        await sut.RunAsync();

        Assert.Equal(SubscriptionPaymentStatus.Active, db.EstablishmentSubscriptions.Single().PaymentStatus);
        Assert.Equal(CommercialStatus.Active, db.Establishments.Single().CommercialStatus);
    }

    [Fact]
    public async Task WithinGraceWindow_TransitionsToOverdueGrace_ButNotSuspended()
    {
        // Doc example: due 10/10, day 11-15 = grace (atraso 1-5).
        var (db, establishment, subscription) = await SeedAsync(TodayInSaoPaulo().AddDays(-3));
        var sut = new SubscriptionGracePeriodJob(db, new NoOpRealtimeNotifier(), new NoOpNotificationService());

        await sut.RunAsync();

        Assert.Equal(SubscriptionPaymentStatus.OverdueGrace, db.EstablishmentSubscriptions.Single().PaymentStatus);
        Assert.Equal(CommercialStatus.OverdueGrace, db.Establishments.Single().CommercialStatus);
    }

    [Fact]
    public async Task PastGraceWindow_TransitionsToSuspended_AndLogsAudit()
    {
        // due 10 days ago with 5 grace days => day 16 equivalent => must be suspended.
        var (db, establishment, subscription) = await SeedAsync(TodayInSaoPaulo().AddDays(-10));
        var sut = new SubscriptionGracePeriodJob(db, new NoOpRealtimeNotifier(), new NoOpNotificationService());

        await sut.RunAsync();

        Assert.Equal(SubscriptionPaymentStatus.SuspendedPayment, db.EstablishmentSubscriptions.Single().PaymentStatus);
        Assert.Equal(CommercialStatus.SuspendedPayment, db.Establishments.Single().CommercialStatus);
        Assert.Contains(db.AuditLogs, a => a.Action == Domain.Audit.AuditActions.EstablishmentSuspended);
    }

    [Fact]
    public async Task RunningTwice_IsIdempotent_DoesNotDuplicateAuditLog()
    {
        var (db, establishment, subscription) = await SeedAsync(TodayInSaoPaulo().AddDays(-10));
        var sut = new SubscriptionGracePeriodJob(db, new NoOpRealtimeNotifier(), new NoOpNotificationService());

        await sut.RunAsync();
        await sut.RunAsync();

        Assert.Single(db.AuditLogs);
        Assert.Equal(SubscriptionPaymentStatus.SuspendedPayment, db.EstablishmentSubscriptions.Single().PaymentStatus);
    }

    [Fact]
    public async Task ExactlyOnGraceBoundaryDay_StillOverdueGrace_NotYetSuspended()
    {
        // due 5 days ago with graceDays=5 => today == dueDate + GraceDays, must NOT suspend yet
        // (doc: day 15/10 is still "atraso 5", suspension only happens on day 16/10).
        var (db, establishment, subscription) = await SeedAsync(TodayInSaoPaulo().AddDays(-5));
        var sut = new SubscriptionGracePeriodJob(db, new NoOpRealtimeNotifier(), new NoOpNotificationService());

        await sut.RunAsync();

        Assert.Equal(SubscriptionPaymentStatus.OverdueGrace, db.EstablishmentSubscriptions.Single().PaymentStatus);
    }
}
