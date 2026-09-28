using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// Kontrakt ODCZYTU domow, grup i gloshnikow z OFICJALNEGO Sonos Control API.
/// Tylko dane i polityka adresu - transport siedzi w SonosControlApiClient.
///
/// Kontrakt ustalony U ZRODLA (docs.sonos.com, definicja OpenAPI "Sonos Control
/// API (cloud)"), nie z pamieci. Pelny zapis zrodel i cytatow:
/// docs/SONOS_CONTROL_READ_PL.md. Skrot rozstrzygniec:
///   * servers: {protocol}://api.ws.sonos.com/control/api/{version},
///     version.enum = ["v1"] (JEDYNA wersja w definicji), protocol.enum = ["https"],
///   * GET /households                          -> obiekt households,
///   * GET /households/{householdId}/groups     -> obiekt groups,
///   * naglowek X-Sonos-Api-Key: wymagany w V1, deprecated w V2,
///   * household.name moze byc POMINIETE (dom bez nazwy) - to kontrakt, nie blad,
///   * groups.groups / groups.players sa nullable, a puste listy sa legalne,
///   * groups.partial (od 1.18.1) mowi, ze czesc grup lub gloshnikow ODPADLA.
///
/// Granice tego etapu: wylacznie GET dwoch powyzszych tras. Zadnego tworzenia
/// grup, subskrypcji, sesji odtwarzania ani sterowania dzwiekiem.
/// </summary>
public sealed class SonosControlApiConfiguration
{
    /// <summary>Host bramki chmury Sonos z pola servers definicji OpenAPI.</summary>
    public const string ExpectedHost = "api.ws.sonos.com";

    /// <summary>Sciezka bazowa bez wersji (przewodnik "Control": base URL).</summary>
    public const string BasePath = "/control/api";

    /// <summary>
    /// JEDYNA wersja w servers.variables.version.enum definicji. To nie nasz
    /// domysl - w tej definicji nie ma zadnej innej wersji.
    /// </summary>
    public const string ApiVersion = "v1";

    private SonosControlApiConfiguration(Uri origin, string? apiKey)
    {
        Origin = origin;
        ApiKey = apiKey;
        HouseholdsUri = new Uri(origin, "households");
    }

    /// <summary>Zaufany, znormalizowany origin z wersja, np. https://api.ws.sonos.com/control/api/v1/ .</summary>
    public Uri Origin { get; }

    /// <summary>
    /// PUBLICZNY klucz klienta integracji (naglowek X-Sonos-Api-Key). To NIE
    /// jest client_secret - sekret aplikacji zostaje w brokerze i klient go nie
    /// zna. Null znaczy "nie podano": klient odmowi wywolania LOKALNIE, zamiast
    /// wysylac zadanie, ktore Sonos odrzuci.
    /// </summary>
    public string? ApiKey { get; }

    public bool HasApiKey => !string.IsNullOrEmpty(ApiKey);

    /// <summary>Adres GET /households.</summary>
    public Uri HouseholdsUri { get; }

    /// <summary>
    /// Buduje konfiguracje dla PRODUKCYJNEJ bramki Sonos. Adres nie pochodzi z
    /// odpowiedzi serwera ani z danych uzytkownika - jest staly w kodzie.
    /// </summary>
    public static SonosControlApiConfiguration CreateDefault(string? apiKey = null)
    {
        var origin = new Uri(
            "https://" + ExpectedHost + BasePath + "/" + ApiVersion + "/",
            UriKind.Absolute);
        return new SonosControlApiConfiguration(origin, apiKey);
    }

    /// <summary>
    /// Przyjmuje wylacznie produkcyjny endpoint HTTPS Sonos, bez dodatkowego
    /// portu, danych logowania, query i fragmentu. Testy podmieniaja transport,
    /// a nie adres docelowy. Klucz zachowany literalnie, sprawdzany przed HTTP.
    /// </summary>
    public static bool TryCreate(
        string? origin,
        string? apiKey,
        out SonosControlApiConfiguration? configuration)
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
            || !string.Equals(parsed.Host, ExpectedHost, StringComparison.OrdinalIgnoreCase)
            || !parsed.IsDefaultPort
            || (parsed.AbsolutePath != BasePath + "/" + ApiVersion
                && parsed.AbsolutePath != BasePath + "/" + ApiVersion + "/"))
        {
            return false;
        }

        var path = parsed.AbsolutePath.EndsWith('/') ? parsed.AbsolutePath : parsed.AbsolutePath + "/";
        var normalized = new UriBuilder(parsed) { Path = path, Query = string.Empty, Fragment = string.Empty }.Uri;
        configuration = new SonosControlApiConfiguration(normalized, apiKey);
        return true;
    }

    /// <summary>Czy adres nalezy do tego samego, zaufanego origin.</summary>
    public bool IsSameOrigin(Uri? candidate) =>
        candidate is not null
        && string.Equals(candidate.Scheme, Origin.Scheme, StringComparison.Ordinal)
        && string.Equals(candidate.Host, Origin.Host, StringComparison.OrdinalIgnoreCase)
        && candidate.Port == Origin.Port;
}

/// <summary>
/// Polityka householdId w ADRESIE. Identyfikator pochodzi z odpowiedzi Sonos,
/// wiec nie wolno go wklejac do sciezki bez sprawdzenia: znak "/" albo ".."
/// zmienilby cel zadania, a "@" moglby udawac dane logowania w URI.
///
/// To lokalna, konserwatywna polityka segmentu adresu, NIE regex z OpenAPI.
/// Przewodnik "Control" pokazuje np.
/// Sonos_1bdj48fbvjJDSkwO90djantsse948J.GNR-OWd0284lDeq325dk; kodujemy
/// wynik jako POJEDYNCZY segment sciezki.
/// </summary>
public static class SonosHouseholdIdPolicy
{
    /// <summary>household.id: maxLength 64 w definicji OpenAPI.</summary>
    public const int MaxLength = 64;

    public static bool IsAcceptable(string? householdId) => TryEncode(householdId, out _);

    /// <summary>
    /// Zwraca bezpieczna postac do WSTAWIENIA jako jeden segment sciezki.
    /// Odrzuca puste, zbyt dlugie i zawierajace jakikolwiek znak poza
    /// dopuszczona lista (w szczegolnosci '/', '\', '%', '?', '#', '@', ':').
    /// </summary>
    public static bool TryEncode(string? householdId, out string? encoded)
    {
        encoded = null;
        if (string.IsNullOrEmpty(householdId) || householdId.Length > MaxLength)
        {
            return false;
        }

        foreach (var character in householdId)
        {
            var allowed = character is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '_'
                or '-'
                or '.';
            if (!allowed)
            {
                return false;
            }
        }

        // Kropka jest w identyfikatorach Sonos dozwolona, ale segment
        // skladajacy sie tylko z kropek to przejscie po katalogach.
        var onlyDots = true;
        foreach (var character in householdId)
        {
            if (character != '.')
            {
                onlyDots = false;
                break;
            }
        }

        if (onlyDots)
        {
            return false;
        }

        // Po tej bramce Uri.EscapeDataString niczego nie zmienia, ale zostaje
        // jako druga, niezalezna warstwa: gdyby lista kiedys sie rozszerzyla,
        // segment nadal bedzie zakodowany.
        encoded = Uri.EscapeDataString(householdId);
        return true;
    }
}

/// <summary>Stan odtwarzania grupy wg enuma playbackState z definicji Sonos.</summary>
public enum SonosPlaybackState
{
    /// <summary>Pole nieobecne albo JSON null - kontraktowo dopuszczalne.</summary>
    Unknown,
    Idle,
    Buffering,
    Paused,
    Playing
}

/// <summary>Tlumaczenie napisow enuma Sonos. Nieznana wartosc NIE jest bledem odpowiedzi.</summary>
public static class SonosPlaybackStates
{
    public static bool TryParse(string? value, out SonosPlaybackState state)
    {
        switch (value)
        {
            case "PLAYBACK_STATE_IDLE":
                state = SonosPlaybackState.Idle;
                return true;
            case "PLAYBACK_STATE_BUFFERING":
                state = SonosPlaybackState.Buffering;
                return true;
            case "PLAYBACK_STATE_PAUSED":
                state = SonosPlaybackState.Paused;
                return true;
            case "PLAYBACK_STATE_PLAYING":
                state = SonosPlaybackState.Playing;
                return true;
            default:
                state = SonosPlaybackState.Unknown;
                return false;
        }
    }
}

/// <summary>
/// Dom Sonos. NIEZMIENNY. Nazwa moze byc null, bo definicja mowi wprost, ze dla
/// domu bez nazwy pole jest POMINIETE - nie podstawiamy tu identyfikatora ani
/// tekstu zastepczego, bo to decyzja warstwy prezentacji.
/// </summary>
public sealed class SonosHousehold
{
    public SonosHousehold(string id, string? name, string? softwareVersion)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        Id = id;
        Name = name;
        SoftwareVersion = softwareVersion;
    }

    /// <summary>household.id - wymagane w definicji.</summary>
    public string Id { get; }

    /// <summary>household.name - nullable; brak pola znaczy dom bez nazwy.</summary>
    public string? Name { get; }

    public bool HasName => !string.IsNullOrEmpty(Name);

    /// <summary>household.swVersion (od 1.42.0) - nullable.</summary>
    public string? SoftwareVersion { get; }

    public override string ToString() =>
        "Dom Sonos " + Id + (HasName ? " (" + Name + ")" : " (bez nazwy)");
}

/// <summary>
/// Gloshnik (logiczny player) Sonos. NIEZMIENNY. Modelujemy tylko pola, ktore
/// NIE sa w definicji przeterminowane: capabilities, deviceIds, devices i
/// isUnregistered maja deprecated:true, wiec ich tu nie ma. websocketUrl tez
/// pomijamy - ten etap nie otwiera zadnego dodatkowego polaczenia.
/// </summary>
public sealed class SonosPlayer
{
    public SonosPlayer(
        string id,
        string name,
        string? softwareVersion,
        string? apiVersion,
        string? minApiVersion)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(name);
        Id = id;
        Name = name;
        SoftwareVersion = softwareVersion;
        ApiVersion = apiVersion;
        MinApiVersion = minApiVersion;
    }

    public string Id { get; }

    public string Name { get; }

    public string? SoftwareVersion { get; }

    public string? ApiVersion { get; }

    public string? MinApiVersion { get; }

    public override string ToString() => "Głośnik Sonos " + Id + " (" + Name + ")";
}

/// <summary>
/// Grupa Sonos. NIEZMIENNA. playerIds zachowuje KOLEJNOSC i LICZBE elementow z
/// odpowiedzi - zadnego cichego odsiewania ani scalania duplikatow, bo licznik
/// gloshnikow w grupie jest widoczny dla uzytkownika.
/// </summary>
public sealed class SonosGroup
{
    public SonosGroup(
        string id,
        string name,
        string coordinatorId,
        IReadOnlyList<string> playerIds,
        SonosPlaybackState playbackState)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentException.ThrowIfNullOrEmpty(coordinatorId);
        ArgumentNullException.ThrowIfNull(playerIds);

        Id = id;
        Name = name;
        CoordinatorId = coordinatorId;
        PlayerIds = new ReadOnlyCollection<string>(new List<string>(playerIds));
        PlaybackState = playbackState;
    }

    public string Id { get; }

    public string Name { get; }

    /// <summary>coordinatorId - to playerId koordynatora grupy.</summary>
    public string CoordinatorId { get; }

    /// <summary>
    /// playerIds: "This list includes the coordinatorId". Zachowana kolejnosc i
    /// liczba elementow z odpowiedzi.
    /// </summary>
    public IReadOnlyList<string> PlayerIds { get; }

    /// <summary>
    /// playbackState - wg definicji oddawany TYLKO w odpowiedzi getGroups i
    /// nullable, wiec Unknown jest legalnym stanem.
    /// </summary>
    public SonosPlaybackState PlaybackState { get; }

    /// <summary>Czy koordynator jest wymieniony na liscie gloshnikow grupy.</summary>
    public bool CoordinatorListed
    {
        get
        {
            foreach (var playerId in PlayerIds)
            {
                if (string.Equals(playerId, CoordinatorId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public override string ToString() =>
        "Grupa Sonos "
        + Id
        + " ("
        + Name
        + "), głośników: "
        + PlayerIds.Count.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Migawka grup i gloshnikow jednego domu. NIEZMIENNA.
/// Partial pochodzi WPROST z pola groups.partial i nie jest domyslane -
/// niepelna lista musi byc jawna, zeby UI nie pokazalo jej jako calosci.
/// </summary>
public sealed class SonosHouseholdTopology
{
    public SonosHouseholdTopology(
        IReadOnlyList<SonosGroup> groups,
        IReadOnlyList<SonosPlayer> players,
        bool partial)
    {
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(players);
        Groups = new ReadOnlyCollection<SonosGroup>(new List<SonosGroup>(groups));
        Players = new ReadOnlyCollection<SonosPlayer>(new List<SonosPlayer>(players));
        Partial = partial;
    }

    public IReadOnlyList<SonosGroup> Groups { get; }

    public IReadOnlyList<SonosPlayer> Players { get; }

    /// <summary>
    /// true, gdy Sonos zglosil partial:true - czesc grup lub gloshnikow ODPADLA
    /// z odpowiedzi (np. przejsciowy stan domu podczas grupowania).
    /// </summary>
    public bool Partial { get; }

    public bool IsEmpty => Groups.Count == 0 && Players.Count == 0;

    public override string ToString() =>
        "Topologia domu Sonos: grup "
        + Groups.Count.ToString(CultureInfo.InvariantCulture)
        + ", głośników "
        + Players.Count.ToString(CultureInfo.InvariantCulture)
        + (Partial ? ", lista NIEPEŁNA" : ", lista pełna");
}

/// <summary>Rozpoznane, rozdzielne wyniki ODCZYTU z Control API.</summary>
public enum SonosControlApiStatus
{
    /// <summary>Odpowiedz zgodna z kontraktem (pusta lista tez jest sukcesem).</summary>
    Success,
    /// <summary>Brak klucza API albo zly householdId - ZADNE zadanie nie poszlo.</summary>
    InvalidConfiguration,
    /// <summary>401 ERROR_NOT_AUTHORIZED: token odrzucony. NIE kasujemy tu poswiadczen.</summary>
    Unauthorized,
    /// <summary>403 ERROR_NO_PERMISSION: brak uprawnien do tej operacji.</summary>
    Forbidden,
    /// <summary>404: dom/grupa nie istnieje albo sie zmienila (ERROR_GROUP_CHANGED).</summary>
    NotFound,
    /// <summary>400: Sonos odrzucil zadanie (zly identyfikator, skladnia, brak parametrow).</summary>
    RequestRejected,
    /// <summary>429: limit zapytan.</summary>
    RateLimited,
    /// <summary>499 ERROR_COMMAND_FAILED i pokrewne - niestandardowy kod Sonos.</summary>
    CommandFailed,
    /// <summary>500/503 po stronie Sonos.</summary>
    ServiceError,
    /// <summary>Brak polaczenia albo minal skonczony budzet operacji.</summary>
    Unreachable,
    /// <summary>Anulowane tokenem wolajacego.</summary>
    Canceled,
    /// <summary>Odpowiedz niezgodna: nie-JSON, zle typy, brak wymaganych pol, ponad limit.</summary>
    InvalidResponse,
    /// <summary>Przekierowanie HTTP: fail-closed, token NIGDY nie idzie na inny host.</summary>
    RedirectRefused
}

/// <summary>
/// Stale, bezpieczne komunikaty. NIGDY nie zawieraja tokenu, klucza API ani
/// zadnego fragmentu ciala odpowiedzi.
/// </summary>
public static class SonosControlApiMessages
{
    private static readonly IReadOnlyDictionary<SonosControlApiStatus, string> Texts =
        new Dictionary<SonosControlApiStatus, string>
        {
            [SonosControlApiStatus.Success] = "Odczyt z Sonos zakończony.",
            [SonosControlApiStatus.InvalidConfiguration] =
                "Odczyt z Sonos nie został wysłany: brak klucza integracji albo niepoprawny identyfikator domu.",
            [SonosControlApiStatus.Unauthorized] =
                "Sonos nie przyjął dostępu do konta. Odśwież dostęp albo zaloguj się ponownie.",
            [SonosControlApiStatus.Forbidden] = "Konto Sonos nie ma uprawnień do tego odczytu.",
            [SonosControlApiStatus.NotFound] = "Sonos nie znalazł wskazanego domu lub grupy.",
            [SonosControlApiStatus.RequestRejected] = "Sonos odrzucił zapytanie o urządzenia.",
            [SonosControlApiStatus.RateLimited] = "Sonos chwilowo ogranicza liczbę zapytań. Spróbuj później.",
            [SonosControlApiStatus.CommandFailed] = "Sonos nie wykonał zapytania o urządzenia.",
            [SonosControlApiStatus.ServiceError] = "Usługa Sonos zgłosiła błąd.",
            [SonosControlApiStatus.Unreachable] = "Nie udało się połączyć z usługą Sonos.",
            [SonosControlApiStatus.Canceled] = "Odczyt z Sonos został anulowany.",
            [SonosControlApiStatus.InvalidResponse] = "Odpowiedź Sonos była niezgodna z oczekiwaną.",
            [SonosControlApiStatus.RedirectRefused] =
                "Sonos próbował przekierować zapytanie; zostało zatrzymane."
        };

    public static string Describe(SonosControlApiStatus status) =>
        Texts.TryGetValue(status, out var text) ? text : Texts[SonosControlApiStatus.InvalidResponse];
}

/// <summary>
/// Wynik GET /households. Pusta lista domow to SUKCES, nie blad.
/// ToString nie wypisuje tokenu ani klucza - tylko status i liczniki.
/// </summary>
public sealed class SonosHouseholdsOutcome
{
    private static readonly IReadOnlyList<SonosHousehold> None =
        new ReadOnlyCollection<SonosHousehold>(new List<SonosHousehold>());

    private SonosHouseholdsOutcome(SonosControlApiStatus status, IReadOnlyList<SonosHousehold> households)
    {
        Status = status;
        Households = households;
    }

    public SonosControlApiStatus Status { get; }

    public IReadOnlyList<SonosHousehold> Households { get; }

    public bool Succeeded => Status == SonosControlApiStatus.Success;

    public string Message => SonosControlApiMessages.Describe(Status);

    internal static SonosHouseholdsOutcome Ok(IReadOnlyList<SonosHousehold> households) =>
        new(SonosControlApiStatus.Success, new ReadOnlyCollection<SonosHousehold>(
            new List<SonosHousehold>(households)));

    internal static SonosHouseholdsOutcome Failure(SonosControlApiStatus status) =>
        new(status == SonosControlApiStatus.Success ? SonosControlApiStatus.InvalidResponse : status, None);

    public override string ToString() =>
        "Odczyt domów Sonos: "
        + Status
        + ", domów "
        + Households.Count.ToString(CultureInfo.InvariantCulture)
        + ", "
        + Message;
}

/// <summary>
/// Wynik GET /households/{householdId}/groups. Dom bez gloshnikow to SUKCES z
/// pusta topologia.
/// </summary>
public sealed class SonosGroupsOutcome
{
    private SonosGroupsOutcome(SonosControlApiStatus status, SonosHouseholdTopology? topology)
    {
        Status = status;
        Topology = topology;
    }

    public SonosControlApiStatus Status { get; }

    public SonosHouseholdTopology? Topology { get; }

    public bool Succeeded => Status == SonosControlApiStatus.Success && Topology is not null;

    public string Message => SonosControlApiMessages.Describe(Status);

    internal static SonosGroupsOutcome Ok(SonosHouseholdTopology topology) =>
        new(SonosControlApiStatus.Success, topology);

    internal static SonosGroupsOutcome Failure(SonosControlApiStatus status) =>
        new(status == SonosControlApiStatus.Success ? SonosControlApiStatus.InvalidResponse : status, null);

    public override string ToString() =>
        "Odczyt grup Sonos: "
        + Status
        + ", "
        + (Topology is null ? "brak danych" : Topology.ToString())
        + ", "
        + Message;
}
