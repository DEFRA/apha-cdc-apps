using System.Security.Cryptography;
using System.Text;

namespace CDC.Common.Auth;

/// <summary>Compares a secret sent by a caller with the configured one.</summary>
public static class SharedSecret
{
    /// <summary>
    /// Returns whether <paramref name="provided"/> equals <paramref name="expected"/>, in constant time so the
    /// response time does not reveal how much of the secret was right.
    /// </summary>
    /// <param name="provided">The value the caller sent; null when it sent none.</param>
    /// <param name="expected">The configured secret.</param>
    /// <returns><see langword="true"/> when the values are identical.</returns>
    public static bool Matches(string? provided, string expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        var providedBytes = Encoding.UTF8.GetBytes(provided ?? string.Empty);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);

        return providedBytes.Length == expectedBytes.Length
            && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
