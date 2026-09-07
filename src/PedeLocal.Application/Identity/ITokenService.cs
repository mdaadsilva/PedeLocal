using PedeLocal.Domain.Identity;

namespace PedeLocal.Application.Identity;

public record GeneratedAccessToken(string Token, DateTime ExpiresAtUtc);

public interface ITokenService
{
    GeneratedAccessToken GenerateAccessToken(User user, IEnumerable<string> roles);

    /// <summary>Cryptographically random opaque refresh token. Only its hash is ever persisted.</summary>
    string GenerateRefreshToken();

    string HashToken(string token);

    TimeSpan RefreshTokenLifetime { get; }
}
