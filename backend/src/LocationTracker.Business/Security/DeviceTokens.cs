using System.Security.Cryptography;
using System.Text;

namespace LocationTracker.Business.Security;

public static class DeviceTokens
{
    private const int TokenBytes = 32;

    /// <summary>A new random token, base64url encoded without padding.</summary>
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenBytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>SHA-256 of the token as lower-case hex. This is the only form that is stored.</summary>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static bool HashesEqual(string expectedHash, string actualHash) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expectedHash),
            Encoding.ASCII.GetBytes(actualHash));
}
