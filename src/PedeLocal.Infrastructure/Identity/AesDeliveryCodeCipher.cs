using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using PedeLocal.Application.Deliveries;

namespace PedeLocal.Infrastructure.Identity;

/// <summary>AES-GCM encryption for the delivery code's customer-retrievable copy. The key is
/// derived from the existing JWT signing secret (SHA-256 of secret + a fixed label) rather than a
/// brand-new configuration entry — that secret is already required and provisioned in every
/// environment (appsettings, Docker, CI, tests), so this adds no new deployment surface. See
/// docs/DECISIONS.md #17.</summary>
public class AesDeliveryCodeCipher : IDeliveryCodeCipher
{
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private readonly byte[] _key;

    public AesDeliveryCodeCipher(IOptions<JwtOptions> jwtOptions)
    {
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(jwtOptions.Value.Secret + ":delivery-code-cipher"));
    }

    public string Encrypt(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSizeBytes];

        using var aesGcm = new AesGcm(_key, TagSizeBytes);
        aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var payload = new byte[NonceSizeBytes + TagSizeBytes + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSizeBytes);
        Buffer.BlockCopy(tag, 0, payload, NonceSizeBytes, TagSizeBytes);
        Buffer.BlockCopy(ciphertext, 0, payload, NonceSizeBytes + TagSizeBytes, ciphertext.Length);
        return Convert.ToBase64String(payload);
    }

    public string Decrypt(string ciphertext)
    {
        var payload = Convert.FromBase64String(ciphertext);
        var nonce = payload[..NonceSizeBytes];
        var tag = payload[NonceSizeBytes..(NonceSizeBytes + TagSizeBytes)];
        var cipherBytes = payload[(NonceSizeBytes + TagSizeBytes)..];
        var plaintextBytes = new byte[cipherBytes.Length];

        using var aesGcm = new AesGcm(_key, TagSizeBytes);
        aesGcm.Decrypt(nonce, cipherBytes, tag, plaintextBytes);
        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
