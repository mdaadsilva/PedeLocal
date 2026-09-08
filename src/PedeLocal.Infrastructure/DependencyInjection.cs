using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Application.Deliveries;
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
        services.AddSingleton<IDeliveryCodeCipher, AesDeliveryCodeCipher>();
        services.AddSingleton<IRouteDistanceService, HaversineDistanceService>();
        services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();
        services.AddSingleton<IPushSender>(sp => BuildPushSender(configuration, sp));
        services.AddScoped<INotificationService, AppNotificationService>();
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();

        return services;
    }

    /// <summary>Real Firebase push only when explicitly opted in (`Firebase:Enabled=true`) AND
    /// credentials actually resolve — any missing/invalid configuration falls back to
    /// <see cref="NoOpPushSender"/> instead of failing startup, so the API always comes up even
    /// without a Firebase project (docs/PUSH_NOTIFICATIONS.md). No credential is ever read from
    /// appsettings*.json; only from configuration sources ultimately backed by environment
    /// variables/secret management in real deployments.</summary>
    private static IPushSender BuildPushSender(IConfiguration configuration, IServiceProvider sp)
    {
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        var options = configuration.GetSection(FirebaseOptions.SectionName).Get<FirebaseOptions>() ?? new FirebaseOptions();
        if (!options.Enabled)
        {
            return new NoOpPushSender(loggerFactory.CreateLogger<NoOpPushSender>());
        }

        try
        {
            // GoogleCredential.FromJson/FromFile are flagged obsolete in favor of CredentialFactory
            // (a defense against blindly trusting an *externally/user-supplied* credential blob).
            // That threat model doesn't apply here — CredentialsJson/CredentialsPath are
            // operator-provided infra config (env var / mounted secret), never end-user input — and
            // CredentialFactory's generic API requires committing to a specific credential subtype
            // up front, which isn't worth the added complexity for that non-issue. See docs/DECISIONS.md.
#pragma warning disable CS0618
            var credential = !string.IsNullOrWhiteSpace(options.CredentialsJson)
                ? GoogleCredential.FromJson(options.CredentialsJson)
                : !string.IsNullOrWhiteSpace(options.CredentialsPath)
                    ? GoogleCredential.FromFile(options.CredentialsPath)
                    : GoogleCredential.GetApplicationDefault(); // honors GOOGLE_APPLICATION_CREDENTIALS.
#pragma warning restore CS0618

            var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions { Credential = credential });
            return new FirebaseFcmPushSender(FirebaseMessaging.GetMessaging(app), loggerFactory.CreateLogger<FirebaseFcmPushSender>());
        }
        catch (Exception ex)
        {
            loggerFactory.CreateLogger(typeof(DependencyInjection).FullName!).LogError(ex,
                "Firebase:Enabled is true but credentials could not be loaded — falling back to no-op push. " +
                "Set Firebase:CredentialsJson, Firebase:CredentialsPath, or GOOGLE_APPLICATION_CREDENTIALS.");
            return new NoOpPushSender(loggerFactory.CreateLogger<NoOpPushSender>());
        }
    }
}
