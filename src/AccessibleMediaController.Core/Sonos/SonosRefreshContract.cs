using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// ODNAWIANIE dostepu Sonos przez WLASNY broker AMC (POST /login/refresh).
/// Ten plik zawiera wylacznie kontrakt: polityke naszego wejscia, rozpoznane
/// wyniki i model odpowiedzi. Zadnego magazynu, timera ani kontrolera sesji.
///
/// Granice, ktorych tu nie wolno przekroczyc:
///   * klient NIE zna client_secret Sonos - sekret zostaje w brokerze i w
///     zadaniu /login/refresh NIE ma zadnego pola sekretu,
///   * refresh token jest NIEPRZEZROCZYSTY: zero trim, zero normalizacji,
///     zero wzorca UUID - przekazujemy dokladnie te znaki, ktore dostalismy,
///   * zaden komunikat bledu nie cytuje ciala odpowiedzi, tokenu ani typu
///     tokenu przyslanego przez serwer,
///   * ta warstwa NIGDY nie kasuje poswiadczen; wystawia tylko rozpoznany
///     wynik, a decyzja o ponownym logowaniu nalezy do wyzszej warstwy.
///
/// Kontrakt brokera odczytany ze ZRODEL amc-sonos-auth (commit b27ed4b,
/// core.py + README.md), nie z produkcyjnego katalogu:
///   POST /login/refresh {"refresh_token": "&lt;opaque&gt;"}
///     200 {access_token, token_type:"Bearer", refresh_token (ZAWSZE),
///          expires_in? (dodatnia liczba calkowita), scope?}
///     400 invalid_body / invalid_json / invalid_refresh_token (dostawca NIE wolany)
///     401 reauthorization_required  -> JEDYNY przypadek "zaloguj sie ponownie"
///     405 method_not_allowed
///     413 body_too_large (cialo zadania > 4096 B)
///     429 refresh_rate_limited
///     502 provider_status / provider_bad_payload / provider_response_too_large
///     503 server_not_configured (sprawa operatora)
///     503 refresh_unavailable (5xx/siec/TLS/timeout dostawcy - PRZEJSCIOWE)
/// </summary>
public static class SonosRefreshTokenPolicy
{
    /// <summary>
    /// NASZ limit rozmiaru zdekodowanego tokenu (backend: MAX_REFRESH_TOKEN_BYTES).
    /// To ochrona rozmiarow NASZEGO wejscia, a NIE deklaracja, jak dlugie tokeny
    /// wydaje Sonos - tego Sonos nie dokumentuje.
    /// </summary>
    public const int MaxDecodedBytes = 2048;

    /// <summary>
    /// NASZ limit calego ciala zadania (backend: MAX_BODY_BYTES). Rowniez nasza
    /// granica, nie regula Sonosa.
    /// </summary>
    public const int MaxRequestBodyBytes = 4096;

    /// <summary>
    /// Zmierzone opakowanie jednego pola w NASZYM serializatorze. Bierzemy
    /// WIEKSZA z dwoch wartosci: wlasnego pomiaru i 21 B zmierzonych przez
    /// backend (json.dumps ze spacja po dwukropku), zeby nasza akceptacja nigdy
    /// nie byla luzniejsza od tej, ktora backend zastosuje do naszego ciala.
    /// </summary>
    public static readonly int OneFieldEnvelopeBytes = Math.Max(
        Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(new RefreshRequestBody(string.Empty))),
        21);

    /// <summary>6 B na jednostke UTF-16 ("\uXXXX"), wiec 12 B poza BMP.</summary>
    public const int EscapedUnitBytes = 6;

    private const string LiteralSafeAscii =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 -._~";

    /// <summary>
    /// ZACHOWAWCZY gorny rozmiar ciala {"refresh_token": value} po escapowaniu.
    /// Sam limit 2048 B zdekodowanego UTF-8 NIE wystarcza: domyslny enkoder
    /// System.Text.Json escapuje takze czesc ASCII, wiec token mieszczacy sie w
    /// 2048 B moze dac cialo ponad 4096 B (backend zmierzyl: 2048 cudzyslowow
    /// -> 4117 B, 512 emoji -> 6165 B). To NASZ kontrakt odzwierciedlajacy
    /// polityke naszego brokera, nie limit Sonosa.
    /// </summary>
    public static int EncodedRequestBudgetBytes(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var total = OneFieldEnvelopeBytes;
        foreach (var character in value)
        {
            total += LiteralSafeAscii.IndexOf(character) >= 0 ? 1 : EscapedUnitBytes;
        }

        return total;
    }

    /// <summary>
    /// Czy MOZEMY wyslac ten token w naszym zadaniu. Sprawdzamy: niepustosc,
    /// kodowalnosc w SCISLYM UTF-8 (odrzucone niesparowane surogaty), rozmiar
    /// zdekodowany, znaki sterujace C0/DEL/C1 oraz zachowawczy budzet
    /// zakodowanego ciala. Zero trim i zero normalizacji - wartosc jest opaque.
    /// </summary>
    public static bool IsAcceptable(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var character in value)
        {
            // C0 + DEL + C1: nie z powodu regul Sonosa, a zeby nie wstrzykiwac
            // znakow sterujacych w cialo/naglowki zadania wychodzacego.
            if (character < 0x20 || character == 0x7F || (character >= 0x80 && character <= 0x9F))
            {
                return false;
            }
        }

        int decoded;
        try
        {
            decoded = new UTF8Encoding(false, throwOnInvalidBytes: true).GetByteCount(value);
        }
        catch (EncoderFallbackException)
        {
            // Niesparowany surogat: nie ma poprawnego, scislego UTF-8.
            return false;
        }

        return decoded <= MaxDecodedBytes
               && EncodedRequestBudgetBytes(value) <= MaxRequestBodyBytes;
    }

    /// <summary>Cialo zadania: DOKLADNIE jedno pole, zadnego sekretu aplikacji.</summary>
    internal sealed record RefreshRequestBody(string refresh_token);
}

/// <summary>Rozpoznane, rozdzielne wyniki odnowienia dostepu.</summary>
public enum SonosRefreshStatus
{
    /// <summary>Broker wydal nowy access token i token odswiezania.</summary>
    Success,
    /// <summary>
    /// Nasze WEJSCIE nie przeszlo lokalnej polityki - zadnego zapytania HTTP nie
    /// bylo. To blad kontraktu zadania, NIE wyrok o waznosci poswiadczen.
    /// </summary>
    InvalidLocalToken,
    /// <summary>
    /// Broker odrzucil nasze ZADANIE (400 invalid_body/invalid_json/
    /// invalid_refresh_token, 405, 413 body_too_large). Blad kontraktu zadania,
    /// nie podstawa do wylogowania.
    /// </summary>
    RequestRejected,
    /// <summary>
    /// HTTP 401 z cialem JSON tego backendu i error DOKLADNIE
    /// reauthorization_required. JEDYNY wynik, po ktorym uzytkownik musi przejsc
    /// logowanie od nowa. Sam ten wynik NIE jest pozwoleniem, by skasowac
    /// nowsza generacje poswiadczen zapisana w miedzyczasie.
    /// </summary>
    ReauthorizationRequired,
    /// <summary>HTTP 429: odmowa TEMPA, nic nie mowi o waznosci tokenu.</summary>
    RateLimited,
    /// <summary>503 server_not_configured: backend bez Key/Secret, sprawa operatora.</summary>
    BrokerNotConfigured,
    /// <summary>503 refresh_unavailable: PRZEJSCIOWY problem dostawcy. Nie wylogowuj.</summary>
    RefreshUnavailable,
    /// <summary>502 i inne 5xx brokera. Nie wylogowuj.</summary>
    BrokerError,
    /// <summary>Brak polaczenia, TLS albo minal skonczony limit czasu. Nie wylogowuj.</summary>
    BrokerUnreachable,
    /// <summary>Operacje przerwal token anulowania wolajacego.</summary>
    Canceled,
    /// <summary>
    /// Odpowiedz niezgodna z kontraktem: nie-JSON, braki/zle typy pol, 404,
    /// nierozpoznane 401, przekroczony limit rozmiaru. NIGDY nie wnioskujemy
    /// z niej sukcesu ani koniecznosci ponownego logowania.
    /// </summary>
    InvalidResponse,
    /// <summary>Przekierowanie HTTP: fail-closed, zadnego tokenu na inny host.</summary>
    RedirectRefused
}

/// <summary>Stale, bezpieczne komunikaty. Nigdy nie cytuja tokenu ani ciala odpowiedzi.</summary>
public static class SonosRefreshMessages
{
    private static readonly IReadOnlyDictionary<SonosRefreshStatus, string> Texts =
        new Dictionary<SonosRefreshStatus, string>
        {
            [SonosRefreshStatus.Success] = "Dostęp do Sonos został odnowiony.",
            [SonosRefreshStatus.InvalidLocalToken] =
                "Zapisany token odświeżania Sonos nie spełnia naszych wymagań żądania; nie wysłano zapytania.",
            [SonosRefreshStatus.RequestRejected] =
                "Serwer logowania Sonos odrzucił żądanie odnowienia jako niezgodne.",
            [SonosRefreshStatus.ReauthorizationRequired] =
                "Sonos wymaga ponownego zalogowania. Rozpocznij logowanie od nowa.",
            [SonosRefreshStatus.RateLimited] =
                "Serwer logowania Sonos ogranicza tempo odnawiania. Spróbuj później.",
            [SonosRefreshStatus.BrokerNotConfigured] =
                "Serwer logowania Sonos nie ma skonfigurowanego dostępu do Sonos.",
            [SonosRefreshStatus.RefreshUnavailable] =
                "Odnowienie dostępu Sonos jest chwilowo niedostępne. Spróbuj później.",
            [SonosRefreshStatus.BrokerError] = "Serwer logowania Sonos zgłosił błąd.",
            [SonosRefreshStatus.BrokerUnreachable] = "Nie udało się połączyć z serwerem logowania Sonos.",
            [SonosRefreshStatus.Canceled] = "Odnawianie dostępu Sonos zostało anulowane.",
            [SonosRefreshStatus.InvalidResponse] =
                "Odpowiedź serwera logowania Sonos była niezgodna z oczekiwaną.",
            [SonosRefreshStatus.RedirectRefused] =
                "Serwer logowania Sonos próbował przekierować żądanie; zostało zatrzymane."
        };

    public static string Describe(SonosRefreshStatus status) =>
        Texts.TryGetValue(status, out var text) ? text : Texts[SonosRefreshStatus.InvalidResponse];
}

/// <summary>
/// Wynik JEDNEJ proby odnowienia. Sukces zawsze niesie ROTOWANY token
/// odswiezania od brokera - warstwa wyzsza zapisuje to, co tu dostanie, i nigdy
/// nie zostaje po cichu przy starej wartosci.
/// </summary>
public sealed class SonosRefreshOutcome
{
    private SonosRefreshOutcome(SonosRefreshStatus status, SonosTokens? tokens)
    {
        Status = status;
        Tokens = tokens;
    }

    public SonosRefreshStatus Status { get; }

    public SonosTokens? Tokens { get; }

    public bool Succeeded => Status == SonosRefreshStatus.Success && Tokens is not null;

    /// <summary>
    /// Wyprowadzone WYLACZNIE ze znanego, rozpoznanego wyniku 401
    /// reauthorization_required. Nie jest to zgoda na kasowanie zapisanych
    /// poswiadczen w tej ani w zadnej innej warstwie.
    /// </summary>
    public bool RequiresReauthorization => Status == SonosRefreshStatus.ReauthorizationRequired;

    public string Message => SonosRefreshMessages.Describe(Status);

    internal static SonosRefreshOutcome Ok(SonosTokens tokens) =>
        new(SonosRefreshStatus.Success, tokens);

    internal static SonosRefreshOutcome Failure(SonosRefreshStatus status) =>
        new(status == SonosRefreshStatus.Success ? SonosRefreshStatus.InvalidResponse : status, null);

    /// <summary>Opis BEZ tokenow i BEZ typu tokenu z odpowiedzi serwera.</summary>
    public override string ToString() =>
        "Odnowienie dostępu Sonos: " + Status + ", " + Message;
}
