using System.Security.Cryptography;
using System.Text;

namespace DataManager.Core.Utilities;

/// <summary>
/// Lightweight hashing helpers used to detect unchanged DACPAC content.
/// </summary>
public static class HashHelper
{
    /// <summary>
    /// Returns the lowercase SHA-256 hex digest (64 characters) of <paramref name="text"/>.
    /// </summary>
    public static string Sha256Hex(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
