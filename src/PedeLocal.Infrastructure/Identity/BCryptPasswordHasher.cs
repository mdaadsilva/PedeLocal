using PedeLocal.Application.Identity;

namespace PedeLocal.Infrastructure.Identity;

public class BCryptPasswordHasher : IPasswordHasher
{
    // Work factor 12 is a reasonable balance for an interactive login path in 2026 hardware.
    private const int WorkFactor = 12;

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}
