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

namespace PedeLocal.Infrastructure.Persistence.Seed.Demo;

/// <summary>One row of the "ready-to-use login credentials" table the CLI prints after a seed run
/// and `docs/DEMO_DATA.md` documents. Purely informational — the plaintext password is the single
/// shared Development-only constant (<see cref="DemoDataSeeder.DevPassword"/>), never derived per
/// row; kept here only so the report can show it next to each account.</summary>
public sealed record DemoCredentialRow(string Role, string City, string Email, string? EstablishmentName);

/// <summary>A delivery confirmation code the planner decided on (plaintext, deterministic) but
/// can't turn into a real <see cref="Deliveries.DeliveryConfirmation"/> row itself — hashing
/// (<c>ITokenService.HashToken</c>) and reversible encryption (<c>IDeliveryCodeCipher</c>) are
/// DI-resolved production services, deliberately kept out of the pure planner. DemoDataSeeder
/// resolves them once and turns every one of these into a real row right before persisting.</summary>
public sealed record PendingDeliveryConfirmation(
    Guid DeliveryId, string PlainCode, DateTime ExpiresAtUtc, int Attempts, int MaxAttempts, DateTime? ConfirmedAtUtc);

/// <summary>The full in-memory demo-data graph, built once by <see cref="DemoDataPlanner"/> as
/// plain <c>PedeLocal.Domain</c> entities — no EF Core dependency at all in this file or the
/// planner, which is what makes the planner's business-rule invariants (no negative stock,
/// Subtotal+DeliveryFee==Total, driver/order same city, etc.) testable with zero database.
///
/// Some reference fields (<c>Establishment.CityId</c>, <c>Establishment.CategoryId</c>, every
/// City-typed foreign key, and the "registered by" actor on historical <c>EstablishmentPayment</c>
/// rows) can't be assigned their real database id at planning time — they point at rows (<c>City</c>,
/// <c>EstablishmentCategory</c>, the Super Admin <c>User</c>) that may already exist with a
/// non-deterministic id created by <c>DevelopmentDataSeeder</c>/<c>DbSeeder</c> on a previous run.
/// The planner fills those fields with a stable placeholder id from <see cref="DemoPlaceholders"/>;
/// <c>DemoDataSeeder</c> recomputes the exact same placeholders, resolves each one's real id by
/// natural key (city/category name, Super Admin email), and substitutes them in one pass before
/// persisting — see <see cref="DemoPlaceholders"/> for why no lookup dictionary needs to travel
/// inside this plan.</summary>
public sealed class DemoDataPlan
{
    public required DemoSeedProfile Profile { get; init; }

    public required List<City> Cities { get; init; }

    public required List<User> Users { get; init; }
    public required List<UserRole> UserRoles { get; init; }
    public required List<Address> Addresses { get; init; }

    public required List<Establishment> Establishments { get; init; }
    public required List<EstablishmentUser> EstablishmentUsers { get; init; }
    public required List<EstablishmentSettings> EstablishmentSettingsList { get; init; }
    public required List<DeliveryPricingSettings> DeliveryPricingSettingsList { get; init; }
    public required List<EstablishmentFeature> EstablishmentFeatures { get; init; }

    public required List<EstablishmentSubscription> Subscriptions { get; init; }
    public required List<EstablishmentPayment> Payments { get; init; }
    /// <summary>Establishment ids the planner deliberately left without a Subscriptions row — the
    /// "SUBSCRIPTION_NOT_FOUND" demo scenario. Never a percentage-based count, always this fixed
    /// documented set.</summary>
    public required List<Guid> EstablishmentsWithoutSubscription { get; init; }

    public required List<MenuCategory> MenuCategories { get; init; }
    public required List<Product> Products { get; init; }
    public required List<ProductOptionGroup> ProductOptionGroups { get; init; }
    public required List<ProductOption> ProductOptions { get; init; }
    public required List<InventoryMovement> InventoryMovements { get; init; }

    public required List<Driver> Drivers { get; init; }
    public required List<DriverLicense> DriverLicenses { get; init; }
    public required List<DriverVehicle> DriverVehicles { get; init; }

    public required List<Order> Orders { get; init; }
    public required List<OrderItem> OrderItems { get; init; }
    public required List<OrderItemOption> OrderItemOptions { get; init; }
    public required List<OrderStatusHistory> OrderStatusHistories { get; init; }

    public required List<Delivery> Deliveries { get; init; }
    public required List<PendingDeliveryConfirmation> PendingConfirmations { get; init; }
    public required List<DriverReceivable> DriverReceivables { get; init; }

    public required List<DemoCredentialRow> Credentials { get; init; }
}
