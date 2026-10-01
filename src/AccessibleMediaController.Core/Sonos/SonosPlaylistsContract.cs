using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// Kontrakt ODCZYTU PLAYLIST SONOSA domu i ich URUCHOMIENIA w grupie.
///
/// Zrodlo: definicja OpenAPI 3.0.3 "Sonos Control API (cloud)"
/// v1.56.0-alpha.1-1-gc264f93f-production-cloud, operacje
/// Playlists-GetPlaylists-HouseholdId (GET /households/{householdId}/playlists,
/// schematy playlistsList / playlist) oraz Playlists-LoadPlaylist-GroupId
/// (POST /groups/{groupId}/playlists, cialo Playlists-LoadPlaylistBody).
/// Wszystkie limity dlugosci i liczebnosci pochodza z pol maxLength/maxItems tej
/// definicji, nie z domyslow i nie z ulubionych.
///
/// UWAGA - playlisty NIE sa ulubionymi i ich definicja jest INNA:
///  * playlistsList.version jest OPCJONALNE i nullable (w favoritesList.version
///    jest WYMAGANE),
///  * playlistsList.playlists jest OPCJONALNE i nullable (favoritesList.items
///    jest WYMAGANE), wiec brak pola i null to NADAL sukces z pusta lista,
///  * playlistsList.playlists ma maxItems 100 (ulubione 70),
///  * playlist ma pola type (maxLength 64, nullable) i trackCount (int32,
///    nullable), ktorych favorite nie ma; favorite ma description i service,
///    ktorych playlist nie ma.
/// Nic z tego nie zostalo przeniesione z ulubionych "bo pasuje".
///
/// Wedlug definicji lista playlist NIE zawiera ulubionych Sonos ani przypietych
/// pozycji My Sonos - to dwa rozne zbiory i AMC ich nie zlepia.
///
/// Granice tego etapu: model + walidacja odpowiedzi + odczyt i uruchomienie na
/// ISTNIEJACYM transporcie i koordynatorze. NIE ma tu UI, skrotow, presetow,
/// AppSettings, cache, pollingu ani subskrypcji zdarzenia playlistsVersionChange.
/// Pola nieznane sa IGNOROWANE.
/// </summary>
public static class SonosPlaylistsLimits
{
    /// <summary>playlistsList.version: maxLength 36, OPCJONALNE i nullable.</summary>
    public const int MaxVersionLength = 36;

    /// <summary>playlistsList.playlists: maxItems 100.</summary>
    public const int MaxItems = 100;

    /// <summary>playlist.id: maxLength 36, wymagane.</summary>
    public const int MaxPlaylistIdLength = 36;

    /// <summary>playlist.name: maxLength 100, wymagane.</summary>
    public const int MaxPlaylistNameLength = 100;

    /// <summary>playlist.type: maxLength 64, nullable, opcjonalne.</summary>
    public const int MaxPlaylistTypeLength = 64;
}

/// <summary>
/// Jedna playlista Sonosa (schema "playlist"). Niemutowalna.
///
/// Wymagane sa WYLACZNIE id i name; AMC dodatkowo wymaga, by nie byly puste -
/// pusty identyfikator nie da sie potem uruchomic, a pusta nazwa nie da sie
/// przeczytac czytnikiem ekranu. To LOKALNA polityka OSTRZEJSZA od definicji
/// (OpenAPI nie stawia minLength), zapisana tu jawnie - taka sama jak przy
/// ulubionych.
///
/// type i trackCount sa OPCJONALNE i nullable: brak znaczy "Sonos nie podal",
/// a nie "zero utworow". Zera nie dopisujemy, bo pusta playlista i playlista o
/// nieznanej liczbie utworow to rozne informacje.
///
/// Wartosci zrodlowe zachowujemy DOKLADNIE: bez trim, bez obcinania, bez
/// zmiany wielkosci liter. Nie naprawiamy zrodla.
/// </summary>
public sealed class SonosPlaylist
{
    public SonosPlaylist(string id, string name, string? type = null, int? trackCount = null)
    {
        if (string.IsNullOrEmpty(id) || id.Length > SonosPlaylistsLimits.MaxPlaylistIdLength)
        {
            throw new ArgumentException("Niepoprawny identyfikator playlisty Sonos.", nameof(id));
        }

        if (string.IsNullOrEmpty(name) || name.Length > SonosPlaylistsLimits.MaxPlaylistNameLength)
        {
            throw new ArgumentException("Niepoprawna nazwa playlisty Sonos.", nameof(name));
        }

        if (type is not null && type.Length > SonosPlaylistsLimits.MaxPlaylistTypeLength)
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        // Ujemna liczba utworow nie jest informacja, ktora wolno pokazac
        // uzytkownikowi ("minus trzy utwory"). Odczyt uzywa WSPOLNEGO
        // OptionalInt32, ktory sprawdza tylko typ i zakres int32, wiec znak
        // pilnuje model - u siebie, raz, zamiast drugiego parsera JSON.
        if (trackCount is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(trackCount));
        }

        Id = id;
        Name = name;
        Type = type;
        TrackCount = trackCount;
    }

    public string Id { get; }

    public string Name { get; }

    /// <summary>
    /// Rodzaj listy; definicja mowi, ze jest to "playlist". Przenoszony
    /// LITERALNIE i nieuzywany jako warunek - AMC nie odrzuca playlisty za to,
    /// ze Sonos kiedys poda tu inna wartosc.
    /// </summary>
    public string? Type { get; }

    /// <summary>
    /// Liczba utworow podana przez Sonosa. null znaczy "nie podano" - NIE zero.
    /// Wartosc przenosimy taka, jaka przyszla; nie jest tu zadnym limitem ani
    /// warunkiem uruchomienia.
    /// </summary>
    public int? TrackCount { get; }

    /// <summary>
    /// Kontrolowane ToString: NIE wypisuje identyfikatora ani nazwy ze zrodla,
    /// bo wynik moze trafic do logu albo zgloszenia bledu.
    /// </summary>
    public override string ToString() =>
        "Playlista Sonos: rodzaj "
        + (Type is null ? "brak" : "jest")
        + ", liczba utworów "
        + (TrackCount is null ? "nieznana" : TrackCount.Value.ToString(CultureInfo.InvariantCulture));
}

/// <summary>
/// Lista playlist domu (schema "playlistsList") wraz z wersja i LITERALNYM
/// identyfikatorem domu, ktorego dotyczylo zapytanie.
///
/// Dwie playlisty o roznych identyfikatorach i TAKIEJ SAMEJ nazwie to DWIE
/// pozycje - nazwa nie jest kluczem. Powtorzony identyfikator odrzuca natomiast
/// CALA odpowiedz (patrz <see cref="SonosPlaylistsOutcome"/>).
///
/// Version jest OPCJONALNA I NULLABLE wprost z definicji: dom bez podanej
/// wersji to legalna odpowiedz, nie blad.
/// </summary>
public sealed class SonosPlaylistsList
{
    public SonosPlaylistsList(string householdId, string? version, IReadOnlyList<SonosPlaylist> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (string.IsNullOrEmpty(householdId))
        {
            throw new ArgumentException("Brak identyfikatora domu.", nameof(householdId));
        }

        if (version is not null && version.Length > SonosPlaylistsLimits.MaxVersionLength)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        if (items.Count > SonosPlaylistsLimits.MaxItems)
        {
            throw new ArgumentOutOfRangeException(nameof(items));
        }

        HouseholdId = householdId;
        Version = version;
        // Kopia defensywna: pozniejsza zmiana listy wolajacego nie rusza modelu.
        Items = new ReadOnlyCollection<SonosPlaylist>(new List<SonosPlaylist>(items));
    }

    /// <summary>Dom, o ktory PYTALISMY - literalnie, bez normalizacji.</summary>
    public string HouseholdId { get; }

    /// <summary>
    /// Wersja listy playlist, jesli Sonos ja podal. null znaczy "nie podano" -
    /// AMC nie podstawia tu pustego napisu ani wartosci z ulubionych.
    /// </summary>
    public string? Version { get; }

    public IReadOnlyList<SonosPlaylist> Items { get; }

    /// <summary>Kontrolowane ToString: tylko licznik, bez tresci i bez identyfikatora domu.</summary>
    public override string ToString() =>
        "Playlisty Sonos: pozycji "
        + Items.Count.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Wynik GET /households/{householdId}/playlists.
///
/// Dom BEZ playlist to SUKCES z pusta lista - i w przeciwienstwie do ulubionych
/// dotyczy to TAKZE braku pola playlists oraz playlists:null, bo definicja
/// opisuje je jako opcjonalne i nullable. Nie wolno tu kopiowac reguly z F1,
/// gdzie brak items jest bledem.
///
/// Nie ma tu zadnego stanu "czesciowego" - albo cala lista, albo nic.
/// </summary>
public sealed class SonosPlaylistsOutcome
{
    private SonosPlaylistsOutcome(SonosControlApiStatus status, SonosPlaylistsList? playlists)
    {
        Status = status;
        Playlists = playlists;
    }

    public SonosControlApiStatus Status { get; }

    public SonosPlaylistsList? Playlists { get; }

    public bool Succeeded => Status == SonosControlApiStatus.Success && Playlists is not null;

    /// <summary>
    /// Staly komunikat ze wspolnego zestawu. NIGDY nie zawiera surowego
    /// globalError.reason, tresci odpowiedzi, tokenu ani klucza integracji.
    /// </summary>
    public string Message => SonosControlApiMessages.Describe(Status);

    internal static SonosPlaylistsOutcome Ok(SonosPlaylistsList playlists) =>
        new(SonosControlApiStatus.Success, playlists);

    internal static SonosPlaylistsOutcome Failure(SonosControlApiStatus status) =>
        new(status == SonosControlApiStatus.Success ? SonosControlApiStatus.InvalidResponse : status, null);

    public override string ToString() =>
        "Odczyt playlist Sonos: "
        + Status
        + ", "
        + (Playlists is null ? "brak danych" : Playlists.ToString())
        + ", "
        + Message;
}

/// <summary>
/// WASKA granica ODCZYTU playlist - dokladnie jedna operacja. Pozwala
/// pozniejszym warstwom (UI biblioteki, Ctrl+L) zalezec od tego kontraktu, a
/// nie od calego klienta transportu. Zadnego zapisu, zadnego konta.
/// </summary>
public interface ISonosPlaylistsApi
{
    Task<SonosPlaylistsOutcome> GetPlaylistsAsync(
        string? accessToken, string? householdId, CancellationToken cancellationToken);
}

/// <summary>
/// WASKA granica URUCHOMIENIA playlisty - dokladnie jedna operacja zapisu
/// (POST /groups/{groupId}/playlists). Osobna od <see cref="ISonosPlaylistsApi"/>,
/// zeby odczyt playlist pozostal odczytem.
///
/// Akcja kolejki to ISTNIEJACY <see cref="SonosFavoriteQueueAction"/>: definicja
/// uzywa dla obu operacji TEGO SAMEGO schematu "queueAction" (REPLACE, APPEND,
/// INSERT, INSERT_NEXT, PLAY_NOW), wiec drugi enum o tych samych wartosciach
/// bylby tylko drugim, rozjezdzajacym sie zrodlem prawdy. Nazwa istniejacego
/// typu zostaje nietknieta - przemianowanie dzialajacego enuma dotknelo by
/// odebranych testow F3 bez zadnego zysku dla uzytkownika.
///
/// Zadnego konta, presetu, koordynatora ani UI tu nie ma.
/// </summary>
public interface ISonosPlaylistLoadApi
{
    Task<SonosGroupCommandOutcome> LoadPlaylistAsync(
        string? accessToken,
        string? groupId,
        string? playlistId,
        SonosFavoriteQueueAction action,
        bool playOnCompletion,
        CancellationToken cancellationToken);
}
