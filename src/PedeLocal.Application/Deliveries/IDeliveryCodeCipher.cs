namespace PedeLocal.Application.Deliveries;

/// <summary>Reversible encryption for the delivery confirmation code's customer-facing copy.
/// Deliberately separate from <c>ITokenService.HashToken</c> (one-way, used for driver-side
/// validation): the customer-retrieval flow needs the plaintext back, which a hash can never give.
/// See docs/DECISIONS.md #17.</summary>
public interface IDeliveryCodeCipher
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}
