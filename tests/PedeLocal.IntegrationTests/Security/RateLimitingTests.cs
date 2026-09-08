using System.Net;
using System.Net.Http.Json;
using PedeLocal.Contracts.Auth;
using PedeLocal.Contracts.Notifications;
using PedeLocal.IntegrationTests.Infrastructure;
using Xunit;

namespace PedeLocal.IntegrationTests.Security;

/// <summary>Real HTTP coverage that the `auth` rate-limit policy (docs/FRONTEND_GUIDE.md) actually
/// rejects with 429 once exceeded. Uses a dedicated factory with a low limit
/// (<see cref="RateLimitedWebApplicationFactory"/>) instead of the shared one, which deliberately
/// sets a very high limit so unrelated tests never trip it — and creates a FRESH factory instance
/// per test (rather than IClassFixture) since the limiter's counter is per-host and every endpoint
/// tagged `[EnableRateLimiting("auth")]` shares the same bucket per IP: reusing one host across
/// tests in this class would let an earlier test's calls count against a later test's budget.</summary>
public class RateLimitingTests
{
    [Fact]
    public async Task Login_ExceedingPermitLimit_Returns429()
    {
        using var factory = new RateLimitedWebApplicationFactory();
        var client = factory.CreateClient();
        var request = new LoginRequest("nobody@example.com", "WrongPassword1");

        for (var i = 0; i < RateLimitedWebApplicationFactory.AuthPermitLimit; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", request);
            // Wrong credentials still count against the limit — the limiter runs before the handler.
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var throttled = await client.PostAsJsonAsync("/api/auth/login", request);

        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }

    [Fact]
    public async Task RegisterDeviceToken_ExceedingPermitLimit_Returns429()
    {
        using var factory = new RateLimitedWebApplicationFactory();
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(factory);
        var client = factory.CreateClient();
        // Login itself is also under the "auth" policy — same IP, same shared bucket — so it
        // already consumes one of the permits before the device-token calls below.
        await client.AsAuthenticatedAsync(scenario.CustomerEmail, IntegrationDataSeeder.Password);

        for (var i = 0; i < RateLimitedWebApplicationFactory.AuthPermitLimit - 1; i++)
        {
            var response = await client.PutAsJsonAsync("/api/me/device-tokens", new RegisterDeviceTokenRequest($"token-{i}", "Android"));
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var throttled = await client.PutAsJsonAsync("/api/me/device-tokens", new RegisterDeviceTokenRequest("token-over-limit", "Android"));

        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }
}
