using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PedeLocal.Infrastructure.Persistence;

namespace PedeLocal.IntegrationTests.Infrastructure;

/// <summary>Boots the real ASP.NET Core pipeline (the actual Program.cs — DI composition,
/// middleware order, JWT auth, rate limiting, exception handling, JSON serialization) against an
/// isolated EF Core InMemory database, so these tests exercise the production wiring without
/// needing a reachable Postgres/Docker. See docs/DECISIONS.md.
///
/// IMPORTANT: configuration is supplied via process environment variables set in the constructor,
/// NOT via ConfigureAppConfiguration. Program.cs reads several settings (Jwt:Secret,
/// ConnectionStrings:Default) SYNCHRONOUSLY between WebApplication.CreateBuilder(args) and
/// builder.Build() — WebApplicationFactory's ConfigureAppConfiguration/ConfigureServices hooks are
/// only guaranteed to apply by the time Build() completes, which is too late for those reads (they
/// silently saw the empty defaults from appsettings.json instead, e.g. producing a zero-length JWT
/// signing key). Environment variables are read by WebApplication.CreateBuilder itself as one of
/// its first configuration sources, so they're visible immediately — this is what actually works.</summary>
public class PedeLocalWebApplicationFactory : WebApplicationFactory<Program>
{
    public readonly string DatabaseName = $"IntegrationTests_{Guid.NewGuid()}";

    public PedeLocalWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
        Environment.SetEnvironmentVariable("ConnectionStrings__Default",
            "Host=localhost;Database=unused;Username=unused;Password=unused"); // never connected to — DbContext is swapped to InMemory below.
        Environment.SetEnvironmentVariable("Jwt__Issuer", "PedeLocal.Tests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "PedeLocal.Tests.Client");
        Environment.SetEnvironmentVariable("Jwt__Secret", "integration-tests-only-secret-key-32chars-minimum");
        Environment.SetEnvironmentVariable("Jwt__AccessTokenMinutes", "15");
        Environment.SetEnvironmentVariable("Jwt__RefreshTokenDays", "30");
        Environment.SetEnvironmentVariable("RateLimiting__AuthPermitLimit", "1000");
        Environment.SetEnvironmentVariable("RateLimiting__GlobalPermitLimit", "10000");
        Environment.SetEnvironmentVariable("Database__AutoMigrate", "true");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // AddInfrastructure's AddDbContext<PedeLocalDbContext>(...UseNpgsql...) registers more
            // than just DbContextOptions<PedeLocalDbContext> — EF Core also adds provider-specific
            // closed-generic descriptors (e.g. IDbContextOptionsConfiguration<PedeLocalDbContext>).
            // Removing only the options descriptor leaves those behind, and EF Core then throws
            // "Only a single database provider can be registered" once the InMemory provider below
            // adds its own. Remove every descriptor closed over our DbContext type instead.
            var efCoreDescriptors = services
                .Where(d => d.ServiceType.IsGenericType && d.ServiceType.GetGenericArguments().Contains(typeof(PedeLocalDbContext)))
                .ToList();
            foreach (var descriptor in efCoreDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<PedeLocalDbContext>(options => options.UseInMemoryDatabase(DatabaseName));
        });
    }

    /// <summary>Fresh scope over the SAME InMemory database this factory's HTTP client talks to
    /// — for seeding prerequisite data (cities, establishments, products) directly rather than
    /// walking every admin HTTP flow just to set up a test.</summary>
    public IServiceScope CreateDataScope() => Services.CreateScope();
}
