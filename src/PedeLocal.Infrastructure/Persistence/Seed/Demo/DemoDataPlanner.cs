using PedeLocal.Domain.Addresses;
using PedeLocal.Domain.Catalog;
using PedeLocal.Domain.Cities;
using PedeLocal.Domain.Deliveries;
using PedeLocal.Domain.Drivers;
using PedeLocal.Domain.Establishments;
using PedeLocal.Domain.Features;
using PedeLocal.Domain.Finance;
using PedeLocal.Domain.Identity;
using PedeLocal.Domain.Inventory;
using PedeLocal.Domain.Orders;
using PedeLocal.Domain.Pricing;
using PedeLocal.Domain.Subscriptions;
using PedeLocal.Infrastructure.Routing;

namespace PedeLocal.Infrastructure.Persistence.Seed.Demo;

/// <summary>Pure, deterministic builder of the full demo-data entity graph — zero EF Core / DB
/// access anywhere in this class or the files it's split across (Establishments/People/Orders
/// partials). Same <see cref="DemoSeedProfile"/> always produces the exact same
/// <see cref="DemoDataPlan"/> (ids, values, relationships): every random choice comes from one
/// <see cref="Random"/> seeded from a fixed per-profile constant, and every id from
/// <see cref="DemoIdFactory"/>. This is what makes the whole thing testable without a database and
/// idempotent/resettable without a marker table — see docs/DEMO_DATA.md.</summary>
public static partial class DemoDataPlanner
{
    private const int SmallSeed = 20260001;
    private const int DemoSeed = 20260002;
    private const int LargeSeed = 20260003;

    /// <summary>Shared mutable planning state threaded through every private phase method (cities →
    /// establishments/catalog/subscriptions → customers/drivers → orders/deliveries). Not exposed
    /// outside this class — <see cref="Plan"/> copies everything into the immutable
    /// <see cref="DemoDataPlan"/> at the end.</summary>
    private sealed class Ctx
    {
        public required DemoSeedProfile Profile;
        public required DemoSeedOptions Options;
        public required Random Rng;
        public required DateOnly Today;
        public required DateTime NowUtc;
        public required TimeZoneInfo LocalZone;

        public readonly List<City> Cities = [];
        public readonly List<User> Users = [];
        public readonly List<UserRole> UserRoles = [];
        public readonly List<Address> Addresses = [];
        public readonly List<Establishment> Establishments = [];
        public readonly List<EstablishmentUser> EstablishmentUsers = [];
        public readonly List<EstablishmentSettings> EstablishmentSettingsList = [];
        public readonly List<DeliveryPricingSettings> DeliveryPricingSettingsList = [];
        public readonly List<EstablishmentFeature> EstablishmentFeatures = [];
        public readonly List<EstablishmentSubscription> Subscriptions = [];
        public readonly List<EstablishmentPayment> Payments = [];
        public readonly List<Guid> EstablishmentsWithoutSubscription = [];
        public readonly List<MenuCategory> MenuCategories = [];
        public readonly List<Product> Products = [];
        public readonly List<ProductOptionGroup> ProductOptionGroups = [];
        public readonly List<ProductOption> ProductOptions = [];
        public readonly List<InventoryMovement> InventoryMovements = [];
        public readonly List<Driver> Drivers = [];
        public readonly List<Order> Orders = [];
        public readonly List<OrderItem> OrderItems = [];
        public readonly List<OrderItemOption> OrderItemOptions = [];
        public readonly List<OrderStatusHistory> OrderStatusHistories = [];
        public readonly List<Delivery> Deliveries = [];
        public readonly List<PendingDeliveryConfirmation> PendingConfirmations = [];
        public readonly List<DriverReceivable> DriverReceivables = [];
        public readonly List<DemoCredentialRow> Credentials = [];

        // Planning-only helper state (not copied verbatim into the output plan).
        public readonly List<EstablishmentPlanInfo> EstablishmentInfos = [];
        public readonly Dictionary<int, List<CustomerInfo>> CustomersByCity = [];
        public readonly Dictionary<int, List<DriverInfo>> DriversByCity = [];
        public readonly HaversineDistanceService Haversine = new();

        /// <summary>Starts at a profile-dependent offset (see <see cref="Plan"/>) specifically so
        /// every value DERIVED from this counter — filler account emails, CPFs, phone numbers —
        /// never collides with another profile's, even though this counter's OWN sequence (0, 1,
        /// 2, ...) would otherwise be identical across profiles. This is what lets a developer seed
        /// one profile on top of another without a prior reset failing on a real unique-index
        /// violation (Users.Email, Drivers.Cpf) — see docs/DEMO_DATA.md. Entity IDS were already
        /// safe on their own via DemoIdFactory hashing the profile in; this offset covers the
        /// human-readable fields hashing doesn't touch.</summary>
        public int GlobalCounter;
        public int NextCounter() => ++GlobalCounter;
    }

    private sealed class EstablishmentPlanInfo
    {
        public required Establishment Establishment;
        public required int CityIndex;
        public required DemoFixtureData.EstablishmentSize Size;
        public required bool SupportsDelivery;
        public required Address Address;
        public required DeliveryPricingSettings? Pricing;
        public required bool IsShowcase;
        public required List<CatalogProductInfo> Catalog;
    }

    private sealed class CatalogProductInfo
    {
        public required Product Product;
        public required bool IsControlled;
        /// <summary>Null for Uncontrolled (unlimited). Mutated as orders consume/restore stock
        /// chronologically while planning orders, mirroring OrderService.DeductStockAsync /
        /// RestoreStockAsync exactly (never allowed to go below 0).</summary>
        public int? RemainingStock;
    }

    private sealed class CustomerInfo
    {
        public required User User;
        public required Address Address;
    }

    private sealed class DriverInfo
    {
        public required Driver Driver;
        public bool Busy;
    }

    public static DemoDataPlan Plan(DemoSeedProfile profile)
    {
        var seed = profile switch
        {
            DemoSeedProfile.Small => SmallSeed,
            DemoSeedProfile.Demo => DemoSeed,
            DemoSeedProfile.Large => LargeSeed,
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };

        var nowUtc = DateTime.UtcNow;
        var zone = ResolveSaoPauloTimeZone();
        // See the GlobalCounter doc comment on Ctx — gap is far larger than any profile's total
        // NextCounter() call count (LARGE tops out in the low tens of thousands).
        var counterOffset = profile switch
        {
            DemoSeedProfile.Small => 0,
            DemoSeedProfile.Demo => 1_000_000,
            DemoSeedProfile.Large => 2_000_000,
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };
        var ctx = new Ctx
        {
            Profile = profile,
            Options = DemoSeedOptions.For(profile),
            Rng = new Random(seed),
            NowUtc = nowUtc,
            LocalZone = zone,
            Today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone)),
            GlobalCounter = counterOffset
        };

        PlanCities(ctx);
        PlanEstablishments(ctx);
        PlanCustomers(ctx);
        PlanDrivers(ctx);
        PlanOrders(ctx);
        BuildCredentials(ctx);

        return new DemoDataPlan
        {
            Profile = profile,
            Cities = ctx.Cities,
            Users = ctx.Users,
            UserRoles = ctx.UserRoles,
            Addresses = ctx.Addresses,
            Establishments = ctx.Establishments,
            EstablishmentUsers = ctx.EstablishmentUsers,
            EstablishmentSettingsList = ctx.EstablishmentSettingsList,
            DeliveryPricingSettingsList = ctx.DeliveryPricingSettingsList,
            EstablishmentFeatures = ctx.EstablishmentFeatures,
            Subscriptions = ctx.Subscriptions,
            Payments = ctx.Payments,
            EstablishmentsWithoutSubscription = ctx.EstablishmentsWithoutSubscription,
            MenuCategories = ctx.MenuCategories,
            Products = ctx.Products,
            ProductOptionGroups = ctx.ProductOptionGroups,
            ProductOptions = ctx.ProductOptions,
            InventoryMovements = ctx.InventoryMovements,
            Drivers = ctx.Drivers,
            Orders = ctx.Orders,
            OrderItems = ctx.OrderItems,
            OrderItemOptions = ctx.OrderItemOptions,
            OrderStatusHistories = ctx.OrderStatusHistories,
            Deliveries = ctx.Deliveries,
            PendingConfirmations = ctx.PendingConfirmations,
            DriverReceivables = ctx.DriverReceivables,
            Credentials = ctx.Credentials
        };
    }

    private static void PlanCities(Ctx ctx)
    {
        for (var i = 0; i < ctx.Options.CityCount; i++)
        {
            var fx = DemoFixtureData.Cities[i];
            ctx.Cities.Add(new City
            {
                Id = DemoPlaceholders.City(ctx.Profile, i),
                Name = fx.Name,
                State = "SP",
                IsActive = true
            });
        }
    }

    // ---------------------------------------------------------------
    // Small shared helpers used by every phase (Establishments/People/Orders partials).
    // ---------------------------------------------------------------

    private static T WeightedPick<T>(Random rng, params (T Value, int Weight)[] options)
    {
        var total = options.Sum(o => o.Weight);
        var roll = rng.Next(total);
        var cumulative = 0;
        foreach (var (value, weight) in options)
        {
            cumulative += weight;
            if (roll < cumulative)
            {
                return value;
            }
        }
        return options[^1].Value;
    }

    private static T PickOne<T>(Random rng, IReadOnlyList<T> items) => items[rng.Next(items.Count)];

    private static decimal RandomDecimal(Random rng, decimal min, decimal max, int decimals = 2)
    {
        var value = min + (decimal)rng.NextDouble() * (max - min);
        return Math.Round(value, decimals);
    }

    private static DateTime ToUtc(Ctx ctx, DateOnly date, int hour, int minute) =>
        TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(hour, minute)), DateTimeKind.Unspecified),
            ctx.LocalZone);

    private static TimeZoneInfo ResolveSaoPauloTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
    }
}
