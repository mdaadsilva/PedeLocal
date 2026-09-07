using System.Security.Cryptography;

namespace PedeLocal.Application.Deliveries;

public static class DeliveryCodeGenerator
{
    /// <summary>Cryptographically secure 6-digit numeric code (000000-999999).</summary>
    public static string GenerateCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}
