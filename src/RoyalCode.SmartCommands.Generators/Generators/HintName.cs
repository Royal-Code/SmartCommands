using System.Security.Cryptography;
using System.Text;

namespace RoyalCode.SmartCommands.Generators.Generators;

/// <summary>
/// Produces deterministic, collision-resistant source hint names without materializing the complete metadata name
/// as a physical file name. This keeps generated paths below common Windows/Git limits even when the consumer asks
/// MSBuild to persist compiler-generated files.
/// </summary>
internal static class HintName
{
    private const int ReadableNameMaxLength = 32;
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    internal static string Create(string identity, string readableName)
    {
        if (string.IsNullOrWhiteSpace(identity))
            throw new ArgumentException("A hint name identity is required.", nameof(identity));
        if (string.IsNullOrWhiteSpace(readableName))
            throw new ArgumentException("A readable hint name is required.", nameof(readableName));

        var safeName = Sanitize(readableName);
        if (safeName.Length > ReadableNameMaxLength)
            safeName = safeName.Substring(0, ReadableNameMaxLength);

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(identity));
        var hash = EncodeBase32(hashBytes);

        return $"{safeName}.{hash}.g.cs";
    }

    /// <summary>
    /// Encodes the first 40 bits of SHA-256 as eight file-name-safe Base32 characters. Five bytes align exactly
    /// with eight Base32 symbols, so padding is unnecessary and no entropy is discarded by the encoding itself.
    /// </summary>
    private static string EncodeBase32(byte[] hash)
    {
        var encoded = new char[8];
        encoded[0] = Base32Alphabet[hash[0] >> 3];
        encoded[1] = Base32Alphabet[((hash[0] & 0x07) << 2) | (hash[1] >> 6)];
        encoded[2] = Base32Alphabet[(hash[1] >> 1) & 0x1F];
        encoded[3] = Base32Alphabet[((hash[1] & 0x01) << 4) | (hash[2] >> 4)];
        encoded[4] = Base32Alphabet[((hash[2] & 0x0F) << 1) | (hash[3] >> 7)];
        encoded[5] = Base32Alphabet[(hash[3] >> 2) & 0x1F];
        encoded[6] = Base32Alphabet[((hash[3] & 0x03) << 3) | (hash[4] >> 5)];
        encoded[7] = Base32Alphabet[hash[4] & 0x1F];
        return new string(encoded);
    }

    private static string Sanitize(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            result.Append(char.IsLetterOrDigit(character) || character is '_' or '-'
                ? character
                : '_');
        }

        return result.ToString();
    }
}
