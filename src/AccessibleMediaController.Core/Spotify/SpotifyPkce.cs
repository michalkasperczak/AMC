using System.Security.Cryptography;
using System.Text;

namespace AccessibleMediaController.Core.Spotify;

public sealed record SpotifyPkcePair(string Verifier, string Challenge);

/// <summary>
/// Authorization Code + PKCE dla klienta desktop bez sekretu.
///
/// Spotify wymaga weryfikatora o długości 43-128 znaków ze zbioru
/// bezpiecznego dla adresu URL. 64 losowe bajty zakodowane Base64Url dają
/// 86 znaków, czyli mieszczą się w przedziale bez dodatkowego przycinania.
/// Metoda wyzwania to zawsze S256 - Spotify dopuszcza tylko ją, wariant
/// "plain" jest odrzucany.
/// </summary>
public static class SpotifyPkce
{
    public static SpotifyPkcePair Create()
    {
        var verifier = Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
        return new SpotifyPkcePair(verifier, CreateChallenge(verifier));
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
