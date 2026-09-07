using Microsoft.Extensions.Options;
using PedeLocal.Application.Deliveries;
using PedeLocal.Infrastructure.Identity;

namespace PedeLocal.UnitTests.TestSupport;

public static class TestDeliveryCodeCipher
{
    public static IDeliveryCodeCipher Create() => new AesDeliveryCodeCipher(Options.Create(new JwtOptions
    {
        Issuer = "PedeLocal.Tests",
        Audience = "PedeLocal.Tests",
        Secret = new string('s', 32),
        AccessTokenMinutes = 15,
        RefreshTokenDays = 30
    }));
}
