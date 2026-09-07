using System.Net.Http.Headers;
using System.Net.Http.Json;
using PedeLocal.Contracts.Auth;

namespace PedeLocal.IntegrationTests.Infrastructure;

public static class HttpClientAuthExtensions
{
    public static async Task<AuthTokensResponse> LoginAsync(this HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var tokens = await response.Content.ReadFromJsonAsync<AuthTokensResponse>();
        return tokens ?? throw new InvalidOperationException("Login did not return tokens.");
    }

    public static void SetBearerToken(this HttpClient client, string accessToken) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    public static async Task<HttpClient> AsAuthenticatedAsync(this HttpClient client, string email, string password)
    {
        var tokens = await client.LoginAsync(email, password);
        client.SetBearerToken(tokens.AccessToken);
        return client;
    }
}
