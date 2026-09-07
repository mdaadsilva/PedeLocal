using Microsoft.Extensions.Options;
using PedeLocal.Application.Common.Exceptions;
using PedeLocal.Application.Identity;
using PedeLocal.Contracts.Auth;
using PedeLocal.Domain.Cities;
using PedeLocal.Infrastructure.Identity;
using PedeLocal.UnitTests.TestSupport;
using Xunit;

namespace PedeLocal.UnitTests.Identity;

public class AuthServiceTests
{
    private static AuthService CreateSut(out PedeLocal.Infrastructure.Persistence.PedeLocalDbContext db)
    {
        db = InMemoryDbContextFactory.Create();
        var hasher = new BCryptPasswordHasher();
        var tokenService = new JwtTokenService(Options.Create(new JwtOptions
        {
            Issuer = "PedeLocal.Tests",
            Audience = "PedeLocal.Tests",
            Secret = new string('s', 32),
            AccessTokenMinutes = 15,
            RefreshTokenDays = 30
        }));
        return new AuthService(db, hasher, tokenService);
    }

    [Fact]
    public async Task RegisterCustomer_WithNewEmail_CreatesUserWithCustomerRole()
    {
        var sut = CreateSut(out var db);

        var response = await sut.RegisterCustomerAsync(
            new RegisterCustomerRequest("Maria Silva", "maria@example.com", "14999990000", "Senha123"),
            CancellationToken.None);

        var user = await db.Users.FindAsync(response.UserId);
        Assert.NotNull(user);
        Assert.Equal("maria@example.com", user!.Email);
        Assert.NotEqual("Senha123", user.PasswordHash);
    }

    [Fact]
    public async Task RegisterCustomer_WithDuplicateEmail_Throws()
    {
        var sut = CreateSut(out _);
        var request = new RegisterCustomerRequest("Maria Silva", "maria@example.com", "14999990000", "Senha123");
        await sut.RegisterCustomerAsync(request, CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            sut.RegisterCustomerAsync(request with { Name = "Maria 2" }, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterDriver_WithoutApprovedCity_Throws()
    {
        var sut = CreateSut(out _);

        await Assert.ThrowsAsync<NotFoundException>(() => sut.RegisterDriverAsync(
            new RegisterDriverRequest("João", "12345678901", "14999990000", "joao@example.com", "Senha123",
                Guid.NewGuid(), "Motorcycle", "Cpf", "12345678901"),
            CancellationToken.None));
    }

    [Fact]
    public async Task RegisterDriver_StartsAsPendingApproval()
    {
        var sut = CreateSut(out var db);
        var city = new City { Name = "Guaimbê", State = "SP", IsActive = true };
        db.Cities.Add(city);
        await db.SaveChangesAsync();

        var response = await sut.RegisterDriverAsync(
            new RegisterDriverRequest("João", "12345678901", "14999990000", "joao@example.com", "Senha123",
                city.Id, "Motorcycle", "Cpf", "12345678901"),
            CancellationToken.None);

        Assert.Equal("PendingApproval", response.ApprovalStatus);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ThrowsUnauthorized()
    {
        var sut = CreateSut(out _);
        await sut.RegisterCustomerAsync(
            new RegisterCustomerRequest("Maria Silva", "maria@example.com", "14999990000", "Senha123"),
            CancellationToken.None);

        await Assert.ThrowsAsync<UnauthorizedAppException>(() =>
            sut.LoginAsync(new LoginRequest("maria@example.com", "WrongPass1"), "127.0.0.1", CancellationToken.None));
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsTokens()
    {
        var sut = CreateSut(out _);
        await sut.RegisterCustomerAsync(
            new RegisterCustomerRequest("Maria Silva", "maria@example.com", "14999990000", "Senha123"),
            CancellationToken.None);

        var tokens = await sut.LoginAsync(
            new LoginRequest("maria@example.com", "Senha123"), "127.0.0.1", CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndOldTokenBecomesInvalid()
    {
        var sut = CreateSut(out _);
        await sut.RegisterCustomerAsync(
            new RegisterCustomerRequest("Maria Silva", "maria@example.com", "14999990000", "Senha123"),
            CancellationToken.None);
        var login = await sut.LoginAsync(
            new LoginRequest("maria@example.com", "Senha123"), "127.0.0.1", CancellationToken.None);

        var refreshed = await sut.RefreshAsync(new RefreshRequest(login.RefreshToken), "127.0.0.1", CancellationToken.None);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);

        await Assert.ThrowsAsync<UnauthorizedAppException>(() =>
            sut.RefreshAsync(new RefreshRequest(login.RefreshToken), "127.0.0.1", CancellationToken.None));
    }

    [Fact]
    public async Task ForgotPassword_WithUnknownEmail_DoesNotThrow()
    {
        var sut = CreateSut(out _);

        await sut.ForgotPasswordAsync(new ForgotPasswordRequest("unknown@example.com"), CancellationToken.None);
    }
}
