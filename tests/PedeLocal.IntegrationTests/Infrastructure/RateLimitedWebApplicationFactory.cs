namespace PedeLocal.IntegrationTests.Infrastructure;

/// <summary>Same wiring as <see cref="PedeLocalWebApplicationFactory"/>, except with a
/// deliberately low `auth` rate-limit policy so tests can actually trip 429 without waiting or
/// firing hundreds of requests. Never share this factory across test classes whose other tests
/// assume the normal high limit — that's exactly why <see cref="PedeLocalWebApplicationFactory"/>'s
/// own constructor unconditionally resets the limit back to 1000 for everyone else (integration
/// tests run sequentially in this assembly — see AssemblyInfo.cs — so there's no cross-test race on
/// these process-wide environment variables).</summary>
public class RateLimitedWebApplicationFactory : PedeLocalWebApplicationFactory
{
    public const int AuthPermitLimit = 3;

    public RateLimitedWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("RateLimiting__AuthPermitLimit", AuthPermitLimit.ToString());
        Environment.SetEnvironmentVariable("RateLimiting__AuthWindowSeconds", "60");
    }
}
