using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PedeLocal.Application.Catalog;
using PedeLocal.Application.Addresses;
using PedeLocal.Application.Admin;
using PedeLocal.Application.Cities;
using PedeLocal.Application.Deliveries;
using PedeLocal.Application.Drivers;
using PedeLocal.Application.Establishments;
using PedeLocal.Application.Finance;
using PedeLocal.Application.Identity;
using PedeLocal.Application.Inventory;
using PedeLocal.Application.Me;
using PedeLocal.Application.Notifications;
using PedeLocal.Application.Orders;
using PedeLocal.Application.Pricing;
using PedeLocal.Application.Subscriptions;

namespace PedeLocal.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<AuthService>();
        services.AddScoped<CityService>();
        services.AddScoped<AddressService>();
        services.AddScoped<EstablishmentService>();
        services.AddScoped<PedeLocal.Application.Common.EstablishmentAccessGuard>();
        services.AddScoped<PedeLocal.Application.Common.DriverAccessGuard>();
        services.AddScoped<CatalogService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<DeliveryPricingService>();
        services.AddScoped<OrderService>();
        services.AddScoped<DriverService>();
        services.AddScoped<DeliveryService>();
        services.AddScoped<FinanceService>();
        services.AddScoped<SubscriptionService>();
        services.AddScoped<SubscriptionGracePeriodJob>();
        services.AddScoped<PedeLocal.Application.Establishments.EstablishmentPauseExpiryJob>();
        services.AddScoped<AdminService>();
        services.AddScoped<MeService>();
        services.AddScoped<DeviceTokenService>();

        return services;
    }
}
