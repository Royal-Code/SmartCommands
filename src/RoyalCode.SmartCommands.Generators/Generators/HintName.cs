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
    private const int HashByteCount = 8;

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
        var hash = new StringBuilder(HashByteCount * 2);
        for (var index = 0; index < HashByteCount; index++)
            hash.Append(hashBytes[index].ToString("x2"));

        return $"{hash}.{safeName}.g.cs";
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
