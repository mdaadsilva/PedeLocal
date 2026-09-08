using Microsoft.EntityFrameworkCore;
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

namespace PedeLocal.Application.Common.Interfaces;

/// <summary>
/// The single persistence gateway the Application layer depends on, so use-case code never
/// references EF Core's concrete DbContext or the Infrastructure project directly.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }

    DbSet<City> Cities { get; }

    DbSet<Address> Addresses { get; }

    DbSet<EstablishmentCategory> EstablishmentCategories { get; }
    DbSet<Establishment> Establishments { get; }
    DbSet<EstablishmentUser> EstablishmentUsers { get; }
    DbSet<EstablishmentSettings> EstablishmentSettings { get; }

    DbSet<MenuCategory> MenuCategories { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductOptionGroup> ProductOptionGroups { get; }
    DbSet<ProductOption> ProductOptions { get; }

    DbSet<InventoryMovement> InventoryMovements { get; }

    DbSet<DeliveryPricingSettings> DeliveryPricingSettings { get; }

    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<OrderItemOption> OrderItemOptions { get; }
    DbSet<OrderStatusHistory> OrderStatusHistories { get; }

    DbSet<Driver> Drivers { get; }

    DbSet<Delivery> Deliveries { get; }
    DbSet<DeliveryConfirmation> DeliveryConfirmations { get; }

    DbSet<DriverReceivable> DriverReceivables { get; }
    DbSet<DriverPaymentBatch> DriverPaymentBatches { get; }

    DbSet<EstablishmentSubscription> EstablishmentSubscriptions { get; }
    DbSet<EstablishmentPayment> EstablishmentPayments { get; }

    DbSet<Notification> Notifications { get; }
    DbSet<DeviceToken> DeviceTokens { get; }

    DbSet<EstablishmentFeature> EstablishmentFeatures { get; }

    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
