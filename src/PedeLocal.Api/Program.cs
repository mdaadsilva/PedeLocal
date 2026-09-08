using System.Text;
using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using PedeLocal.Api.Errors;
using PedeLocal.Api.Filters;
using Scalar.AspNetCore;
using PedeLocal.Api.Services;
using PedeLocal.Application;
using PedeLocal.Application.Common.Interfaces;
using PedeLocal.Application.Subscriptions;
using PedeLocal.Infrastructure;
using PedeLocal.Infrastructure.Identity;
using PedeLocal.Infrastructure.Persistence;
using PedeLocal.Infrastructure.Persistence.Seed;
using PedeLocal.Infrastructure.RealTime;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

WebApplication? builtApp = null;
try
{
    var builder = WebApplication.CreateBuilder(args);

    // "Testing" is used exclusively by PedeLocal.IntegrationTests (WebApplicationFactory<Program>)
    // to run the real ASP.NET Core pipeline against an EF Core InMemory database, with no
    // dependency on a reachable Postgres — Hangfire (which needs a real relational store to install
    // its schema) and the recurring-job registration are skipped in that mode. Nothing about
    // Development/Staging/Production behavior changes.
    var isTestingEnvironment = builder.Environment.IsEnvironment("Testing");

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddControllers(options => options.Filters.Add<ValidationActionFilter>());
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Components ??= new();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Informe apenas o token JWT (sem o prefixo 'Bearer ')."
            };
            return Task.CompletedTask;
        });
    });

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    if (!isTestingEnvironment)
    {
        var connectionString = builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");
        builder.Services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connectionString)));
        builder.Services.AddHangfireServer();
    }

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

    builder.Services.AddExceptionHandler<ApiExceptionHandler>();
    builder.Services.AddProblemDetails();

    var jwtSecret = builder.Configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException("Jwt:Secret is not configured.");
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
            // Browsers can't set an Authorization header on a WebSocket handshake, so SignalR
            // clients pass the token via query string instead — accepted only for the hub path.
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });
    builder.Services.AddAuthorization();

    builder.Services.AddSignalR();

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Default", policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                // AllowCredentials is required for the SignalR hub's negotiate/WebSocket handshake.
                policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
            }
        });
    });

    var authPermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 5);
    var authWindowSeconds = builder.Configuration.GetValue("RateLimiting:AuthWindowSeconds", 60);
    var globalPermitLimit = builder.Configuration.GetValue("RateLimiting:GlobalPermitLimit", 100);
    var globalWindowSeconds = builder.Configuration.GetValue("RateLimiting:GlobalWindowSeconds", 60);

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = globalPermitLimit,
                    Window = TimeSpan.FromSeconds(globalWindowSeconds),
                    QueueLimit = 0
                }));

        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = authPermitLimit,
                    Window = TimeSpan.FromSeconds(authWindowSeconds),
                    QueueLimit = 0
                }));
    });

    var app = builder.Build();

    if (builder.Configuration.GetValue("Database:AutoMigrate", true))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PedeLocalDbContext>();

        // The InMemory provider (integration tests only) has no migrations to apply — it needs
        // EnsureCreated instead. Every real deployment uses Npgsql (relational), so this never
        // changes behavior outside tests.
        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }

        await DbSeeder.SeedRolesAsync(db);
        await DbSeeder.SeedEstablishmentCategoriesAsync(db);

        // Sample login-ready data (Super Admin, launch cities, one example establishment/
        // customer/driver) — Development only. Never seed weak credentials elsewhere.
        if (app.Environment.IsDevelopment())
        {
            var passwordHasher = scope.ServiceProvider.GetRequiredService<PedeLocal.Application.Identity.IPasswordHasher>();
            await DevelopmentDataSeeder.SeedAsync(db, passwordHasher);
        }
    }

    if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options => options.WithTitle("PedeLocal API"));
    }

    if (!isTestingEnvironment)
    {
        // `services.AddHangfire(...)` (DI-based registration) never sets the legacy static
        // `JobStorage.Current` singleton, so the static `RecurringJob.AddOrUpdate` API throws
        // "Current JobStorage instance has not been initialized" — resolve `IRecurringJobManager`
        // from DI instead, per Hangfire's own recommendation.
        using var recurringJobScope = app.Services.CreateScope();
        var recurringJobManager = recurringJobScope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
        recurringJobManager.AddOrUpdate<SubscriptionGracePeriodJob>(
            "subscription-grace-period",
            job => job.RunAsync(CancellationToken.None),
            Cron.Daily(hour: 3), // 03:00 UTC — off-peak for the América/São Paulo audience.
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }

    app.UseExceptionHandler();

    app.UseSerilogRequestLogging();

    if (!app.Environment.IsDevelopment())
    {
        // Not set in Development so local HTTP-over-localhost workflows (dotnet run without a
        // dev cert, WebApplicationFactory's in-memory TestServer) aren't forced onto HTTPS.
        app.UseHsts();
    }

    app.UseHttpsRedirection();

    app.Use(async (context, next) =>
    {
        // Cheap, unconditional response headers — none of these depend on trusting any
        // client-supplied header, unlike X-Forwarded-*, which needs a reverse proxy in front of
        // this app to be configured safely (see docs/SECURITY_AUDIT.md) and is deliberately left
        // for that deployment's own configuration instead of assumed here.
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        await next();
    });

    var uploadsRoot = builder.Configuration["Storage:LocalRootPath"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
    Directory.CreateDirectory(uploadsRoot);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsRoot),
        RequestPath = builder.Configuration["Storage:PublicBasePath"] ?? "/uploads"
    });

    app.UseCors("Default");

    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<PedeLocalHub>("/hubs/pedelocal");

    builtApp = app;
}
catch (Exception ex)
{
    Log.Fatal(ex, "PedeLocal.Api failed to start");
    Log.CloseAndFlush();
    throw;
}

// app.Run() is deliberately outside any catch clause above: WebApplicationFactory (used by
// PedeLocal.IntegrationTests) intercepts it by throwing HostAbortedException to capture the
// built host without actually listening — a catch here, even one filtered to exclude that type,
// risks swallowing it if the interception ever wraps/rethrows differently. A bare `finally` can't
// suppress it either way, so this is the safe place for that to happen.
try
{
    builtApp!.Run();
}
finally
{
    Log.CloseAndFlush();
}

// Makes the top-level-statements-generated Program class visible to
// PedeLocal.IntegrationTests for WebApplicationFactory<Program>.
public partial class Program;
