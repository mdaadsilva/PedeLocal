using PedeLocal.Domain.Establishments;
using PedeLocal.Domain.Orders;
using PedeLocal.Domain.Subscriptions;
using PedeLocal.Infrastructure.Persistence.Seed.Demo;
using Xunit;

namespace PedeLocal.UnitTests.Demo;

/// <summary>Tests the pure planner ONLY — zero database anywhere here. See
/// tests/PedeLocal.IntegrationTests/Demo for the persistence-layer (SeedAsync/ResetAsync) tests.</summary>
public class DemoDataPlannerTests
{
    [Fact]
    public void Plan_SameProfile_IsFullyDeterministic()
    {
        var planA = DemoDataPlanner.Plan(DemoSeedProfile.Small);
        var planB = DemoDataPlanner.Plan(DemoSeedProfile.Small);

        Assert.Equal(planA.Establishments.Select(e => e.Id), planB.Establishments.Select(e => e.Id));
        Assert.Equal(planA.Establishments.Select(e => e.Name), planB.Establishments.Select(e => e.Name));
        Assert.Equal(planA.Orders.Select(o => o.Id), planB.Orders.Select(o => o.Id));
        Assert.Equal(planA.Orders.Select(o => o.Total), planB.Orders.Select(o => o.Total));
        Assert.Equal(planA.Orders.Select(o => o.CreatedAtUtc), planB.Orders.Select(o => o.CreatedAtUtc));
        Assert.Equal(planA.Users.Select(u => u.Email), planB.Users.Select(u => u.Email));
    }

    [Theory]
    [InlineData(DemoSeedProfile.Small)]
    [InlineData(DemoSeedProfile.Demo)]
    public void Plan_NeverProducesNegativeStock(DemoSeedProfile profile)
    {
        var plan = DemoDataPlanner.Plan(profile);

        Assert.All(plan.Products.Where(p => p.StockMode == PedeLocal.Domain.Catalog.StockMode.Controlled),
            p => Assert.True(p.StockQuantity is >= 0, $"Product '{p.Name}' has negative stock: {p.StockQuantity}"));
    }

    [Theory]
    [InlineData(DemoSeedProfile.Small)]
    [InlineData(DemoSeedProfile.Demo)]
    public void Plan_EveryOrder_SubtotalPlusFeeEqualsTotal(DemoSeedProfile profile)
    {
        var plan = DemoDataPlanner.Plan(profile);

        Assert.All(plan.Orders, o => Assert.Equal(o.Subtotal + o.DeliveryFee, o.Total));
    }

    [Theory]
    [InlineData(DemoSeedProfile.Small)]
    [InlineData(DemoSeedProfile.Demo)]
    public void Plan_DeliveryOrders_OnlyForEstablishmentsWithPricingSettings_AndHaveAnAddress(DemoSeedProfile profile)
    {
        var plan = DemoDataPlanner.Plan(profile);
        var establishmentsWithPricing = plan.DeliveryPricingSettingsList.Select(p => p.EstablishmentId).ToHashSet();

        foreach (var order in plan.Orders.Where(o => o.Type == OrderType.Delivery))
        {
            Assert.Contains(order.EstablishmentId, establishmentsWithPricing);
            Assert.NotNull(order.AddressId);
        }

        Assert.All(plan.Orders.Where(o => o.Type == OrderType.Pickup), o => Assert.Null(o.AddressId));
    }

    [Theory]
    [InlineData(DemoSeedProfile.Small)]
    [InlineData(DemoSeedProfile.Demo)]
    public void Plan_EveryDelivery_DriverIsFromTheSameCityAsTheDelivery(DemoSeedProfile profile)
    {
        var plan = DemoDataPlanner.Plan(profile);
        var driverCityById = plan.Drivers.ToDictionary(d => d.Id, d => d.CityId);

        foreach (var delivery in plan.Deliveries.Where(d => d.DriverId is not null))
        {
            Assert.Equal(delivery.CityId, driverCityById[delivery.DriverId!.Value]);
        }
    }

    [Theory]
    [InlineData(DemoSeedProfile.Small)]
    [InlineData(DemoSeedProfile.Demo)]
    public void Plan_EveryDriver_HasAtMostOneNonTerminalDeliveryAssigned(DemoSeedProfile profile)
    {
        var plan = DemoDataPlanner.Plan(profile);
        var nonTerminal = new[] { Domain.Deliveries.DeliveryStatus.Assigned, Domain.Deliveries.DeliveryStatus.PickedUp, Domain.Deliveries.DeliveryStatus.OutForDelivery };

        var activeCountsByDriver = plan.Deliveries
            .Where(d => d.DriverId is not null && nonTerminal.Contains(d.Status))
            .GroupBy(d => d.DriverId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.All(activeCountsByDriver.Values, count => Assert.Equal(1, count));
    }

    [Theory]
    [InlineData(DemoSeedProfile.Small)]
    [InlineData(DemoSeedProfile.Demo)]
    public void Plan_Subscriptions_OnlyUseRealEnumValues_AndGraceDaysIsAlwaysFive(DemoSeedProfile profile)
    {
        var plan = DemoDataPlanner.Plan(profile);

        Assert.All(plan.Subscriptions, s =>
        {
            Assert.True(Enum.IsDefined(s.PaymentStatus));
            Assert.Equal(5, s.GraceDays);
        });
    }

    [Theory]
    [InlineData(DemoSeedProfile.Small)]
    [InlineData(DemoSeedProfile.Demo)]
    public void Plan_GracePeriodEstablishments_AreWithinTheGraceWindow_SuspendedOnesAreBeyondIt(DemoSeedProfile profile)
    {
        var plan = DemoDataPlanner.Plan(profile);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var subscription in plan.Subscriptions.Where(s => s.PaymentStatus == SubscriptionPaymentStatus.OverdueGrace))
        {
            var daysOverdue = today.DayNumber - subscription.DueDate.DayNumber;
            Assert.InRange(daysOverdue, 0, subscription.GraceDays + 1); // +1 tolerance for the day the test runs vs. the day the plan was built.
        }

        foreach (var subscription in plan.Subscriptions.Where(s => s.PaymentStatus == SubscriptionPaymentStatus.SuspendedPayment))
        {
            var daysOverdue = today.DayNumber - subscription.DueDate.DayNumber;
            Assert.True(daysOverdue > subscription.GraceDays - 1, $"Expected > {subscription.GraceDays - 1} days overdue, got {daysOverdue}.");
        }
    }

    [Fact]
    public void Plan_Demo_HasExactly3EstablishmentsWithoutSubscription_AndTheyAreNotTheShowcaseOnes()
    {
        var plan = DemoDataPlanner.Plan(DemoSeedProfile.Demo);

        Assert.Equal(3, plan.EstablishmentsWithoutSubscription.Count);

        var subscriptionEstablishmentIds = plan.Subscriptions.Select(s => s.EstablishmentId).ToHashSet();
        Assert.All(plan.EstablishmentsWithoutSubscription, id => Assert.DoesNotContain(id, subscriptionEstablishmentIds));

        var showcaseEmails = plan.Credentials.Where(c => c.Role == "ESTABLISHMENT_ADMIN").Select(c => c.EstablishmentName).ToHashSet();
        var noSubscriptionNames = plan.Establishments.Where(e => plan.EstablishmentsWithoutSubscription.Contains(e.Id)).Select(e => e.Name);
        Assert.DoesNotContain(noSubscriptionNames, name => showcaseEmails.Contains(name));
    }

    [Fact]
    public void Plan_Small_GeneratesStrictlyFewerEstablishmentsAndOrdersThanDemo()
    {
        var small = DemoDataPlanner.Plan(DemoSeedProfile.Small);
        var demo = DemoDataPlanner.Plan(DemoSeedProfile.Demo);

        Assert.True(small.Establishments.Count < demo.Establishments.Count);
        Assert.True(small.Orders.Count < demo.Orders.Count);
        Assert.True(small.Cities.Count < demo.Cities.Count);
    }

    [Fact]
    public void Plan_Demo_GeneratesRoughly60EstablishmentsAcross3Cities()
    {
        var plan = DemoDataPlanner.Plan(DemoSeedProfile.Demo);

        Assert.Equal(3, plan.Cities.Count);
        Assert.InRange(plan.Establishments.Count, 55, 65);
    }

    [Theory]
    [InlineData(DemoSeedProfile.Small)]
    [InlineData(DemoSeedProfile.Demo)]
    public void Plan_PredictableAccounts_ExistForEveryCity(DemoSeedProfile profile)
    {
        var plan = DemoDataPlanner.Plan(profile);

        foreach (var city in plan.Cities)
        {
            var cityFx = DemoFixtureData.Cities.Single(c => c.Name == city.Name);
            Assert.Contains(plan.Users, u => u.Email == $"customer.{cityFx.EmailSlug}@pedelocal.dev");
            Assert.Contains(plan.Users, u => u.Email == $"driver.{cityFx.EmailSlug}@pedelocal.dev");
            Assert.Contains(plan.Users, u => u.Email == $"merchant.{cityFx.EmailSlug}@pedelocal.dev");
        }
    }

    [Fact]
    public void Plan_NoTwoEstablishments_ShareTheSameSlug()
    {
        var plan = DemoDataPlanner.Plan(DemoSeedProfile.Demo);

        var duplicateSlugs = plan.Establishments.GroupBy(e => e.Slug).Where(g => g.Count() > 1).ToList();
        Assert.Empty(duplicateSlugs);
    }

    [Fact]
    public void Plan_Large_DoesNotThrow_AndGeneratesMoreThanDemo()
    {
        // LARGE is never run automatically by anything (see docs/DEMO_DATA.md) — this only checks
        // the pure planner itself never throws and scales up, not full invariant coverage (kept
        // cheap/fast: it's still just in-memory object construction, no I/O).
        var demo = DemoDataPlanner.Plan(DemoSeedProfile.Demo);
        var large = DemoDataPlanner.Plan(DemoSeedProfile.Large);

        Assert.True(large.Establishments.Count > demo.Establishments.Count);
        Assert.True(large.Orders.Count > demo.Orders.Count);
        Assert.All(large.Orders, o => Assert.Equal(o.Subtotal + o.DeliveryFee, o.Total));
    }

    [Fact]
    public void Plan_NoTwoUsers_ShareTheSameEmail()
    {
        var plan = DemoDataPlanner.Plan(DemoSeedProfile.Demo);

        var duplicateEmails = plan.Users.GroupBy(u => u.Email).Where(g => g.Count() > 1).ToList();
        Assert.Empty(duplicateEmails);
    }

    [Fact]
    public void Plan_Demo_DriverOnboardingCoversEveryStatus_AndSomeHaveExpiredLicenses()
    {
        var plan = DemoDataPlanner.Plan(DemoSeedProfile.Demo);
        var licenseByDriverId = plan.DriverLicenses.ToDictionary(l => l.DriverId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        Assert.Contains(plan.Drivers, d => d.OnboardingStatus == Domain.Drivers.DriverOnboardingStatus.PendingDocuments);
        Assert.Contains(plan.Drivers, d => d.OnboardingStatus == Domain.Drivers.DriverOnboardingStatus.UnderReview);
        Assert.Contains(plan.Drivers, d => d.OnboardingStatus == Domain.Drivers.DriverOnboardingStatus.Rejected && d.RejectionReason != null);
        Assert.Contains(plan.Drivers, d => d.OnboardingStatus == Domain.Drivers.DriverOnboardingStatus.Approved && d.Status == Domain.Drivers.DriverStatus.Blocked);
        Assert.Contains(plan.Drivers,
            d => d.OnboardingStatus == Domain.Drivers.DriverOnboardingStatus.Approved &&
                 licenseByDriverId.TryGetValue(d.Id, out var l) && l.ExpiresAt < today);

        // PendingDocuments drivers never have a license/vehicle on file; everyone else does.
        foreach (var driver in plan.Drivers)
        {
            var hasLicense = licenseByDriverId.ContainsKey(driver.Id);
            if (driver.OnboardingStatus == Domain.Drivers.DriverOnboardingStatus.PendingDocuments)
            {
                Assert.False(hasLicense);
            }
            else
            {
                Assert.True(hasLicense);
            }
        }
    }

    [Fact]
    public void Plan_Demo_HasPausedEstablishments_WithReasonSet()
    {
        var plan = DemoDataPlanner.Plan(DemoSeedProfile.Demo);
        var paused = plan.Establishments.Where(e => e.OperationalStatus == OperationalStatus.Paused).ToList();

        Assert.NotEmpty(paused);
        Assert.All(paused, e => Assert.NotNull(e.PauseReason));
    }

    [Fact]
    public void Plan_Large_PausedEstablishments_HaveVariedExpiry()
    {
        // LARGE has enough establishments that all three PausedUntilUtc buckets (still paused,
        // already expired, indefinite) are reliably represented — DEMO's much smaller sample can
        // land entirely in one or two buckets by chance with this deterministic seed.
        var plan = DemoDataPlanner.Plan(DemoSeedProfile.Large);
        var paused = plan.Establishments.Where(e => e.OperationalStatus == OperationalStatus.Paused).ToList();

        Assert.NotEmpty(paused);
        Assert.Contains(paused, e => e.PausedUntilUtc == null);
        Assert.Contains(paused, e => e.PausedUntilUtc != null && e.PausedUntilUtc > DateTime.UtcNow);
        Assert.Contains(paused, e => e.PausedUntilUtc != null && e.PausedUntilUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void Plan_Demo_DeliveryPricing_UsesFlatFeeAndRadius()
    {
        var plan = DemoDataPlanner.Plan(DemoSeedProfile.Demo);

        Assert.NotEmpty(plan.DeliveryPricingSettingsList);
        Assert.All(plan.DeliveryPricingSettingsList, p =>
        {
            Assert.True(p.DeliveryFee > 0);
            Assert.True(p.DeliveryRadiusKm > 0);
        });
        // Varied, not one identical value everywhere.
        Assert.True(plan.DeliveryPricingSettingsList.Select(p => p.DeliveryFee).Distinct().Count() > 1);
    }

    [Fact]
    public void Plan_CommercialStatus_MatchesSubscriptionTierMapping()
    {
        var plan = DemoDataPlanner.Plan(DemoSeedProfile.Demo);
        var subscriptionByEstablishment = plan.Subscriptions.ToDictionary(s => s.EstablishmentId);

        foreach (var establishment in plan.Establishments)
        {
            if (establishment.CommercialStatus == CommercialStatus.SuspendedAdmin)
            {
                // BLOCKED scenario: subscription itself must still be Active — the block is administrative.
                if (subscriptionByEstablishment.TryGetValue(establishment.Id, out var subscription))
                {
                    Assert.Equal(SubscriptionPaymentStatus.Active, subscription.PaymentStatus);
                }
                continue;
            }

            if (!subscriptionByEstablishment.TryGetValue(establishment.Id, out var sub))
            {
                Assert.Contains(establishment.Id, plan.EstablishmentsWithoutSubscription);
                continue;
            }

            var expected = sub.PaymentStatus switch
            {
                SubscriptionPaymentStatus.Active => CommercialStatus.Active,
                SubscriptionPaymentStatus.OverdueGrace => CommercialStatus.OverdueGrace,
                SubscriptionPaymentStatus.SuspendedPayment => CommercialStatus.SuspendedPayment,
                _ => throw new InvalidOperationException("unexpected subscription status in demo plan")
            };
            Assert.Equal(expected, establishment.CommercialStatus);
        }
    }
}
