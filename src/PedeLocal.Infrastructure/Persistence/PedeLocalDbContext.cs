using Microsoft.EntityFrameworkCore;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Domain.Addresses;
using PedeLocal.Domain.Audit;
using PedeLocal.Domain.Catalog;
using PedeLocal.Domain.Cities;
using PedeLocal.Domain.Deliveries;
using PedeLocal.Domain.Drivers;
using PedeLocal.Domain.Establishments;
using PedeLocal.Domain.Features;
using PedeLocal.Domain.Finance;
using PedeLocal.Domain.Identity;
using PedeLocal.Domain.Inventory;
using PedeLocal.Domain.Notifications;
using PedeLocal.Domain.Orders;
using PedeLocal.Domain.Pricing;
using PedeLocal.Domain.Subscriptions;

namespace PedeLocal.Infrastructure.Persistence;

public class PedeLocalDbContext : DbContext, IAppDbContext
{
    public PedeLocalDbContext(DbContextOptions<PedeLocalDbContext> options) : base(options)
    {
    }

    // Identity
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    // Cities
    public DbSet<City> Cities => Set<City>();

    // Addresses
    public DbSet<Address> Addresses => Set<Address>();

    // Establishments
    public DbSet<EstablishmentCategory> EstablishmentCategories => Set<EstablishmentCategory>();
    public DbSet<Establishment> Establishments => Set<Establishment>();
    public DbSet<EstablishmentUser> EstablishmentUsers => Set<EstablishmentUser>();
    public DbSet<EstablishmentSettings> EstablishmentSettings => Set<EstablishmentSettings>();

    // Catalog
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductOptionGroup> ProductOptionGroups => Set<ProductOptionGroup>();
    public DbSet<ProductOption> ProductOptions => Set<ProductOption>();

    // Inventory
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    // Pricing
    public DbSet<DeliveryPricingSettings> DeliveryPricingSettings => Set<DeliveryPricingSettings>();

    // Orders
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderItemOption> OrderItemOptions => Set<OrderItemOption>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();

    // Drivers
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<DriverLicense> DriverLicenses => Set<DriverLicense>();
    public DbSet<DriverVehicle> DriverVehicles => Set<DriverVehicle>();

    // Deliveries
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<DeliveryConfirmation> DeliveryConfirmations => Set<DeliveryConfirmation>();
    public DbSet<DeliveryOccurrence> DeliveryOccurrences => Set<DeliveryOccurrence>();

    // Finance
    public DbSet<DriverReceivable> DriverReceivables => Set<DriverReceivable>();
    public DbSet<DriverPaymentBatch> DriverPaymentBatches => Set<DriverPaymentBatch>();

    // Subscriptions
    public DbSet<EstablishmentSubscription> EstablishmentSubscriptions => Set<EstablishmentSubscription>();
    public DbSet<EstablishmentPayment> EstablishmentPayments => Set<EstablishmentPayment>();

    // Notifications
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();

    // Features
    public DbSet<EstablishmentFeature> EstablishmentFeatures => Set<EstablishmentFeature>();

    // Audit
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PedeLocalDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        TouchTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        TouchTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public void DiscardPendingChanges() => ChangeTracker.Clear();

    private void TouchTimestamps()
    {
        foreach (var entry in ChangeTracker.Entries<Domain.Common.BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = DateTime.UtcNow;
                entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
            }
        }
    }
}
