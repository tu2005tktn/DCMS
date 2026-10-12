using System.Security.Cryptography;
using System.Text;

namespace DCMS.Domain.Features.Authentication;

/// <summary>
/// Verification-proof helpers shared by the UC-02 recovery process. Proofs are compared by hash and
/// in constant time so a wrong proof cannot be narrowed down by timing. A proof, its hash and a reset
/// grant are never logged or entered into an audit entry.
/// </summary>
public static class RecoveryProof
{
    private const int MinCodeLength = 4;
    private const int MaxCodeLength = 12;

    public static string Hash(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    /// <summary>Constant-time comparison of a submitted proof against a stored hash.</summary>
    public static bool Matches(string? submitted, string? storedHash)
    {
        if (string.IsNullOrEmpty(submitted) || string.IsNullOrEmpty(storedHash)) return false;
        var submittedHash = Hash(submitted);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(submittedHash), Encoding.UTF8.GetBytes(storedHash));
    }

    /// <summary>Zero-padded numeric proof, cryptographically random.</summary>
    public static string GenerateCode(int length)
    {
        if (length is < MinCodeLength or > MaxCodeLength)
            throw new ArgumentOutOfRangeException(nameof(length),
                $"Verification code length must be between {MinCodeLength} and {MaxCodeLength}.");
        return RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, length)).ToString(new string('0', length));
    }

    /// <summary>Opaque single-use reset grant (256-bit).</summary>
    public static string GenerateGrant() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static bool IsWellFormedCode(string? proof, int length)
    {
        if (string.IsNullOrWhiteSpace(proof) || proof.Length != length) return false;
        return proof.All(char.IsAsciiDigit);
    }
}
