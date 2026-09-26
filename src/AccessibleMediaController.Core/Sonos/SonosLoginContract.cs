using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// PIERWSZE logowanie Sonos przez WLASNY broker AMC (projekt amc-sonos-auth).
/// Ten plik zawiera wylacznie kontrakt: PKCE, zaufany origin brokera, polityke
/// adresu autoryzacji, model tokenow i rozpoznane wyniki.
///
/// Granice, ktorych tu nie wolno przekroczyc:
///   * klient NIE zna client_secret Sonos - sekret zostaje w brokerze,
///   * verifier jest wysylany WYLACZNIE na skonfigurowany origin brokera,
///     nigdy na adres pochodzacy z odpowiedzi,
///   * zaden komunikat bledu nie cytuje surowego ciala odpowiedzi ani tokenu.
/// Odswiezanie tokenu, trwaly zapis (DPAPI) i UI to osobne etapy - nie ma ich tutaj.
/// </summary>
public static class SonosLoginPkce
{
    /// <summary>Broker (core.py, _S256_RE) przyjmuje DOKLADNIE 43 znaki base64url bez paddingu.</summary>
    public const int ChallengeLength = 43;

    /// <summary>32 bajty CSPRNG = 43 znaki base64url. Osobna para dla KAZDEJ operacji.</summary>
    public const int VerifierEntropyBytes = 32;

    public static string CreateVerifier()
    {
        var raw = RandomNumberGenerator.GetBytes(VerifierEntropyBytes);
        return Base64UrlNoPadding(raw);
    }

    public static string CreateChallenge(string verifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(verifier);
        var digest = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlNoPadding(digest);
    }

    public static (string Verifier, string Challenge) Create()
    {
        var verifier = CreateVerifier();
        return (verifier, CreateChallenge(verifier));
    }

    public static bool LooksLikeS256Field(string? value)
    {
        if (value is null || value.Length != ChallengeLength)
        {
            return false;
        }

        foreach (var character in value)
        {
            var allowed = character is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '-'
                or '_';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    internal static string Base64UrlNoPadding(byte[] raw) =>
        Convert.ToBase64String(raw).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>
/// Zaufany origin brokera z konfiguracji PRODUKCYJNEJ. Tylko HTTPS, bez danych
/// logowania w adresie, bez query i fragmentu. Zadna odpowiedz serwera nie moze
/// tego podmienic - verifier zawsze idzie tutaj.
/// </summary>
public sealed class SonosLoginBrokerConfiguration
{
    private SonosLoginBrokerConfiguration(Uri origin)
    {
        Origin = origin;
        StartUri = new Uri(origin, "login/start");
        ResultUri = new Uri(origin, "login/result");
    }

    public Uri Origin { get; }

    public Uri StartUri { get; }

    public Uri ResultUri { get; }

    public static bool TryCreate(string? origin, out SonosLoginBrokerConfiguration? configuration)
    {
        configuration = null;
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        if (!Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(parsed.UserInfo)
            || !string.IsNullOrEmpty(parsed.Query)
            || !string.IsNullOrEmpty(parsed.Fragment)
            || string.IsNullOrEmpty(parsed.Host))
        {
            return false;
        }

        var path = parsed.AbsolutePath.EndsWith('/') ? parsed.AbsolutePath : parsed.AbsolutePath + "/";
        var normalized = new UriBuilder(parsed) { Path = path, Query = string.Empty, Fragment = string.Empty }.Uri;
        configuration = new SonosLoginBrokerConfiguration(normalized);
        return true;
    }

    /// <summary>Czy dany adres nalezy do tego samego, zaufanego origin brokera.</summary>
    public bool IsSameOrigin(Uri? candidate) =>
        candidate is not null
        && string.Equals(candidate.Scheme, Origin.Scheme, StringComparison.Ordinal)
        && string.Equals(candidate.Host, Origin.Host, StringComparison.OrdinalIgnoreCase)
        && candidate.Port == Origin.Port;
}

/// <summary>
/// Adres autoryzacji sprawdzamy wobec RZECZYWISTEGO protokolu Sonos z backendu
/// (core.py: SONOS_AUTHORIZE_URL = https://api.sonos.com/login/v3/oauth).
/// Samo "zaczyna sie od https" nie jest kryterium - taki warunek przepuszczal
/// dowolna cudza strone logowania.
/// </summary>
public static class SonosAuthorizeUrlPolicy
{
    public const string ExpectedHost = "api.sonos.com";
    public const string ExpectedPath = "/login/v3/oauth";

    public static bool IsTrustedAuthorizeUrl(string? value) =>
        TryParse(value, out _);

    public static bool TryParse(string? value, out Uri? authorizeUri)
    {
        authorizeUri = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(parsed.Host, ExpectedHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(parsed.UserInfo))
        {
            return false;
        }

        if (parsed.Port != 443)
        {
            return false;
        }

        var path = parsed.AbsolutePath.TrimEnd('/');
        if (!string.Equals(path, ExpectedPath, StringComparison.Ordinal))
        {
            return false;
        }

        authorizeUri = parsed;
        return true;
    }
}

/// <summary>Rozpoznane, rozdzielne wyniki operacji logowania.</summary>
public enum SonosLoginStatus
{
    /// <summary>Broker wydal tokeny (odczyt jednorazowy - juz zuzyty).</summary>
    Success,
    /// <summary>Uzytkownik jeszcze nie skonczyl logowania w przegladarce.</summary>
    Pending,
    /// <summary>Sesja logowania wygasla po stronie klienta albo brokera.</summary>
    Expired,
    /// <summary>Poprawnie zgloszona ODMOWA (HTTP 400 access_denied) - NIE ma tokenow.</summary>
    Denied,
    /// <summary>
    /// HTTP 403 verifier_mismatch: LOKALNY dowod PKCE klienta nie pasuje do
    /// wyzwania sesji. To blad po naszej stronie, NIE decyzja Sonos. Broker nie
    /// konsumuje wtedy gotowego wyniku.
    /// </summary>
    ProofMismatch,
    /// <summary>Limit sesji/zapytan (HTTP 429).</summary>
    RateLimited,
    /// <summary>Broker dziala, ale nie ma skonfigurowanych kluczy Sonos (503 server_not_configured).</summary>
    BrokerNotConfigured,
    /// <summary>Broker odpowiedzial bledem 5xx innym niz brak konfiguracji.</summary>
    BrokerError,
    /// <summary>Nie udalo sie nawiazac polaczenia albo minal skonczony limit czasu.</summary>
    BrokerUnreachable,
    /// <summary>Operacje przerwal token anulowania wolajacego.</summary>
    Canceled,
    /// <summary>Odpowiedz niezgodna z kontraktem: nie-JSON, brak pol, zly adres, przekroczony limit.</summary>
    InvalidResponse,
    /// <summary>Przekierowanie HTTP: fail-closed, zadnego payloadu na inny host.</summary>
    RedirectRefused
}

/// <summary>
/// Stale, bezpieczne komunikaty. Nigdy nie zawieraja ciala odpowiedzi, tokenu,
/// verifiera ani identyfikatora sesji.
/// </summary>
public static class SonosLoginMessages
{
    private static readonly IReadOnlyDictionary<SonosLoginStatus, string> Texts =
        new Dictionary<SonosLoginStatus, string>
        {
            [SonosLoginStatus.Success] = "Logowanie Sonos zakończone.",
            [SonosLoginStatus.Pending] = "Logowanie Sonos jeszcze nie zostało ukończone w przeglądarce.",
            [SonosLoginStatus.Expired] = "Sesja logowania Sonos wygasła. Rozpocznij logowanie ponownie.",
            [SonosLoginStatus.Denied] = "Sonos nie przyznał dostępu. Logowanie zostało odrzucone.",
            [SonosLoginStatus.ProofMismatch] =
                "Dowód logowania Sonos nie zgadza się z rozpoczętą sesją. Rozpocznij logowanie ponownie.",
            [SonosLoginStatus.RateLimited] = "Serwer logowania Sonos jest chwilowo przeciążony. Spróbuj później.",
            [SonosLoginStatus.BrokerNotConfigured] = "Serwer logowania Sonos nie ma skonfigurowanego dostępu do Sonos.",
            [SonosLoginStatus.BrokerError] = "Serwer logowania Sonos zgłosił błąd.",
            [SonosLoginStatus.BrokerUnreachable] = "Nie udało się połączyć z serwerem logowania Sonos.",
            [SonosLoginStatus.Canceled] = "Logowanie Sonos zostało anulowane.",
            [SonosLoginStatus.InvalidResponse] = "Odpowiedź serwera logowania Sonos była niezgodna z oczekiwaną.",
            [SonosLoginStatus.RedirectRefused] = "Serwer logowania Sonos próbował przekierować żądanie; zostało zatrzymane."
        };

    public static string Describe(SonosLoginStatus status) =>
        Texts.TryGetValue(status, out var text) ? text : Texts[SonosLoginStatus.InvalidResponse];
}

/// <summary>
/// Tokeny Sonos: TYLKO model w pamieci. ToString nie wypisuje wartosci, zeby
/// zaden log, wyjatek ani zrzut kolekcji nie wyniosl tokenu z procesu.
/// </summary>
public sealed class SonosTokens
{
    /// <summary>Jedyny obslugiwany typ tokenu Sonos (backend domysla go w core.py).</summary>
    public const string BearerTokenType = "Bearer";

    public SonosTokens(
        string accessToken,
        string tokenType,
        int? expiresInSeconds,
        string? refreshToken,
        string? scope)
    {
        ArgumentException.ThrowIfNullOrEmpty(accessToken);
        if (!TryCanonicalizeTokenType(tokenType, out var canonical))
        {
            // Bez echa: komunikat NIE cytuje odrzuconej wartosci, zeby dowolny
            // tekst od serwera nie trafil do logu ani do ToString.
            throw new ArgumentException(
                "Nieobsługiwany typ tokenu Sonos.",
                nameof(tokenType));
        }

        AccessToken = accessToken;
        TokenType = canonical;
        ExpiresInSeconds = expiresInSeconds;
        RefreshToken = refreshToken;
        Scope = scope;
    }

    /// <summary>
    /// Brak pola i JSON null to wg kontraktu backendu domyslny Bearer. Kazda
    /// wartosc rowna "bearer" bez wzgledu na wielkosc liter i otaczajace biale
    /// znaki jest kanonizowana do "Bearer". Cokolwiek innego jest ODRZUCANE -
    /// klient nie wypisuje dowolnego tekstu z odpowiedzi.
    /// </summary>
    public static bool TryCanonicalizeTokenType(string? tokenType, out string canonical)
    {
        canonical = BearerTokenType;
        if (tokenType is null)
        {
            return true;
        }

        var trimmed = tokenType.Trim();
        return trimmed.Length > 0
            && string.Equals(trimmed, BearerTokenType, StringComparison.OrdinalIgnoreCase);
    }

    public string AccessToken { get; }

    public string TokenType { get; }

    public int? ExpiresInSeconds { get; }

    public string? RefreshToken { get; }

    public string? Scope { get; }

    public bool HasRefreshToken => !string.IsNullOrEmpty(RefreshToken);

    /// <summary>Opis BEZ wartosci tokenow - jedyna dozwolona reprezentacja tekstowa.</summary>
    public override string ToString() =>
        "Tokeny Sonos w pamięci (wartości ukryte): typ "
        + TokenType
        + ", token odświeżania "
        + (HasRefreshToken ? "obecny" : "brak")
        + ", czas ważności "
        + (ExpiresInSeconds.HasValue ? ExpiresInSeconds.Value.ToString() + " s" : "nieznany")
        + ".";
}

/// <summary>
/// Sesja pierwszego logowania. Verifier zyje TYLKO tutaj, w pamieci, i nie
/// trafia do ToString ani do state.json.
/// </summary>
public sealed class SonosLoginSession
{
    internal SonosLoginSession(string sessionId, string codeVerifier, Uri authorizeUri, DateTimeOffset expiresAtUtc)
    {
        SessionId = sessionId;
        CodeVerifier = codeVerifier;
        AuthorizeUri = authorizeUri;
        ExpiresAtUtc = expiresAtUtc;
    }

    public string SessionId { get; }

    /// <summary>Verifier PKCE tej jednej operacji. Nie zapisywac, nie logować.</summary>
    internal string CodeVerifier { get; }

    public Uri AuthorizeUri { get; }

    public DateTimeOffset ExpiresAtUtc { get; }

    public bool IsExpired(DateTimeOffset nowUtc) => nowUtc >= ExpiresAtUtc;

    public override string ToString() =>
        "Sesja logowania Sonos (identyfikator i weryfikator ukryte), adres autoryzacji "
        + AuthorizeUri.GetLeftPart(UriPartial.Path)
        + ".";
}

public sealed class SonosLoginStartOutcome
{
    private SonosLoginStartOutcome(SonosLoginStatus status, SonosLoginSession? session)
    {
        Status = status;
        Session = session;
    }

    public SonosLoginStatus Status { get; }

    public SonosLoginSession? Session { get; }

    public bool Succeeded => Status == SonosLoginStatus.Success && Session is not null;

    public string Message => SonosLoginMessages.Describe(Status);

    internal static SonosLoginStartOutcome Ok(SonosLoginSession session) =>
        new(SonosLoginStatus.Success, session);

    internal static SonosLoginStartOutcome Failure(SonosLoginStatus status) =>
        new(status == SonosLoginStatus.Success ? SonosLoginStatus.InvalidResponse : status, null);

    public override string ToString() => "Start logowania Sonos: " + Status + ", " + Message;
}

public sealed class SonosLoginResultOutcome
{
    private SonosLoginResultOutcome(SonosLoginStatus status, SonosTokens? tokens)
    {
        Status = status;
        Tokens = tokens;
    }

    public SonosLoginStatus Status { get; }

    public SonosTokens? Tokens { get; }

    public bool Succeeded => Status == SonosLoginStatus.Success && Tokens is not null;

    public string Message => SonosLoginMessages.Describe(Status);

    internal static SonosLoginResultOutcome Ok(SonosTokens tokens) =>
        new(SonosLoginStatus.Success, tokens);

    internal static SonosLoginResultOutcome Failure(SonosLoginStatus status) =>
        new(status == SonosLoginStatus.Success ? SonosLoginStatus.InvalidResponse : status, null);

    public override string ToString() => "Odbiór logowania Sonos: " + Status + ", " + Message;
}
