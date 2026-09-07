using Microsoft.Extensions.Options;
using PedeLocal.Application.Identity;
using PedeLocal.Infrastructure.Identity;

namespace PedeLocal.UnitTests.TestSupport;

public static class TestTokenService
{
    public static ITokenService Create() => new JwtTokenService(Options.Create(new JwtOptions
    {
        Issuer = "PedeLocal.Tests",
        Audience = "PedeLocal.Tests",
        Secret = new string('s', 32),
        AccessTokenMinutes = 15,
        RefreshTokenDays = 30
    }));
}
