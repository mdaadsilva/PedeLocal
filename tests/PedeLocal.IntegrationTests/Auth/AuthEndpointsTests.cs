using System.Net;
using System.Net.Http.Json;
using PedeLocal.Contracts.Auth;
using PedeLocal.IntegrationTests.Infrastructure;
using Xunit;

namespace PedeLocal.IntegrationTests.Auth;

public class AuthEndpointsTests : IClassFixture<PedeLocalWebApplicationFactory>
{
    private readonly PedeLocalWebApplicationFactory _factory;

    public AuthEndpointsTests(PedeLocalWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task RegisterCustomer_ThenLogin_ReturnsTokens()
    {
        var client = _factory.CreateClient();
        var email = $"novo-{Guid.NewGuid():N}@example.com";

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register/customer",
            new RegisterCustomerRequest("Maria Teste", email, "14999990000", "Senha123"));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Senha123"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var tokens = await loginResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();
        Assert.NotNull(tokens);
        Assert.False(string.IsNullOrWhiteSpace(tokens!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401WithErrorEnvelope()
    {
        var client = _factory.CreateClient();
        var email = $"login-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register/customer",
            new RegisterCustomerRequest("Cliente", email, "14999990000", "Senha123"));

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "SenhaErrada1"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorEnvelope>();
        Assert.Equal("INVALID_CREDENTIALS", body!.Code);
        Assert.False(string.IsNullOrWhiteSpace(body.TraceId));
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndOldTokenCannotBeReused()
    {
        var client = _factory.CreateClient();
        var email = $"refresh-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/auth/register/customer",
            new RegisterCustomerRequest("Cliente Refresh", email, "14999990000", "Senha123"));
        var login = await client.LoginAsync(email, "Senha123");

        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();
        Assert.NotEqual(login.RefreshToken, refreshed!.RefreshToken);

        // Reusing the original (now-rotated) refresh token must fail.
        var reuseResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private record ErrorEnvelope(string Code, string Message, string TraceId);
}
