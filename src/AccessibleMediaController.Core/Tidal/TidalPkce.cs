using System.Security.Cryptography;
using System.Text;

namespace AccessibleMediaController.Core.Tidal;

public sealed record TidalPkcePair(string Verifier, string Challenge);

public static class TidalPkce
{
    public static TidalPkcePair Create()
    {
        var verifier = Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        return new TidalPkcePair(verifier, CreateChallenge(verifier));
    }

    public static string CreateChallenge(string verifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(verifier);
        return Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
