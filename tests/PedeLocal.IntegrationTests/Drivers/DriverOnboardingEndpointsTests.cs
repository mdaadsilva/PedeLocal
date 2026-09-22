using System.Net;
using System.Net.Http.Json;
using PedeLocal.Contracts.Auth;
using PedeLocal.Contracts.Drivers;
using PedeLocal.Contracts.Establishments;
using PedeLocal.IntegrationTests.Infrastructure;
using Xunit;

namespace PedeLocal.IntegrationTests.Drivers;

/// <summary>End-to-end HTTP coverage of the driver onboarding gate (section 13's checklist) — a
/// freshly registered driver cannot go online or accept deliveries until a SUPER_ADMIN approves
/// them, and only after submitting CNH + vehicle documents.</summary>
public class DriverOnboardingEndpointsTests : IClassFixture<PedeLocalWebApplicationFactory>
{
    private readonly PedeLocalWebApplicationFactory _factory;

    public DriverOnboardingEndpointsTests(PedeLocalWebApplicationFactory factory) => _factory = factory;

    private async Task<(HttpClient Client, string Email, Guid CityId)> RegisterAndLoginDriverAsync(Guid cityId)
    {
        var email = $"driver-{Guid.NewGuid():N}@example.com";
        var client = _factory.CreateClient();
        var cpf = Random.Shared.NextInt64(10000000000L, 99999999999L).ToString();
        var registerResponse = await client.PostAsJsonAsync("/api/auth/register/driver", new RegisterDriverRequest(
            "Entregador Teste", cpf, "14999990000", email, "Senha123",
            cityId, "Motorcycle", "Cpf", cpf));
        var body = await registerResponse.Content.ReadAsStringAsync();
        Assert.True(registerResponse.StatusCode == HttpStatusCode.Created, $"Register failed: {registerResponse.StatusCode} {body}");
        await client.AsAuthenticatedAsync(email, "Senha123");
        return (client, email, cityId);
    }

    [Fact]
    public async Task NewlyRegisteredDriver_StartsAsPendingDocuments_AndCannotGoOnline()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var (client, _, _) = await RegisterAndLoginDriverAsync(scenario.CityId);

        var profile = await (await client.GetAsync("/api/driver/me")).Content.ReadFromJsonAsync<DriverResponse>();
        Assert.Equal("PendingDocuments", profile!.OnboardingStatus);

        var goOnline = await client.PostAsync("/api/driver/me/status/online", null);
        Assert.Equal(HttpStatusCode.Forbidden, goOnline.StatusCode);
    }

    [Fact]
    public async Task SubmittingLicenseAndVehicle_MovesToUnderReview_StillCannotGoOnline()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var (client, _, _) = await RegisterAndLoginDriverAsync(scenario.CityId);

        var licenseResponse = await client.PutAsJsonAsync("/api/driver/me/documents/license",
            new SubmitDriverLicenseRequest("12345678901", "A", DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)), true, null));
        Assert.Equal(HttpStatusCode.OK, licenseResponse.StatusCode);

        var vehicleResponse = await client.PutAsJsonAsync("/api/driver/me/documents/vehicle",
            new SubmitDriverVehicleRequest("Motorcycle", "ABC1234", "CG 160", "Preto", null));
        var detail = await vehicleResponse.Content.ReadFromJsonAsync<DriverDetailResponse>();

        Assert.Equal("UnderReview", detail!.Driver.OnboardingStatus);

        var goOnline = await client.PostAsync("/api/driver/me/status/online", null);
        Assert.Equal(HttpStatusCode.Forbidden, goOnline.StatusCode);
    }

    [Fact]
    public async Task Driver_CannotApproveOwnAccount_AdminRoutesRejectDriverToken()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var (client, _, _) = await RegisterAndLoginDriverAsync(scenario.CityId);

        var listResponse = await client.GetAsync("/api/admin/drivers");
        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);

        var approveResponse = await client.PostAsync($"/api/admin/drivers/{Guid.NewGuid()}/approve", null);
        Assert.Equal(HttpStatusCode.Forbidden, approveResponse.StatusCode);
    }

    [Fact]
    public async Task Driver_CannotViewAnotherDriversDocuments()
    {
        var scenario = await IntegrationDataSeeder.SeedOrderableEstablishmentAsync(_factory);
        var (clientA, _, _) = await RegisterAndLoginDriverAsync(scenario.CityId);
        var (clientB, _, _) = await RegisterAndLoginDriverAsync(scenario.CityId);

        await clientA.PutAsJsonAsync("/api/driver/me/documents/license",
            new SubmitDriverLicenseRequest("11111111111", "A", DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)), true, null));

        // There is no "get another driver's documents by id" self-service route at all — B's own
        // "my documents" call can only ever return B's own (empty) documents, never A's.
        var bDocuments = await (await clientB.GetAsync("/api/driver/me/documents")).Content.ReadFromJsonAsync<DriverDetailResponse>();
        Assert.Null(bDocuments!.License);
    }
}
