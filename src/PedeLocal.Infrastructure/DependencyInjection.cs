using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Application.Identity;
using PedeLocal.Infrastructure.Identity;
using PedeLocal.Infrastructure.Notifications;
using PedeLocal.Infrastructure.Persistence;
using PedeLocal.Infrastructure.RealTime;
using PedeLocal.Infrastructure.Routing;
using PedeLocal.Infrastructure.Storage;

namespace PedeLocal.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddDbContext<PedeLocalDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(PedeLocalDbContext).Assembly.FullName)));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<PedeLocalDbContext>());

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Secret) && o.Secret.Length >= 32,
                "Jwt:Secret must be configured with at least 32 characters.")
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IRouteDistanceService, HaversineDistanceService>();
        services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();
        services.AddSingleton<IPushSender, NoOpPushSender>();
        services.AddScoped<INotificationService, AppNotificationService>();
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();

        return services;
    }
}
