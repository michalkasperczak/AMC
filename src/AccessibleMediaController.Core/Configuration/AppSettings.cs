using System.Globalization;
using AccessibleMediaController.Core.Presentation;
using AccessibleMediaController.Core.Sessions;

namespace AccessibleMediaController.Core.Configuration;

public enum StartupTarget
{
    MediaList,
    SessionList
}

public enum PercentageSeekAnnouncementMode
{
    Percent,
    Time,
    PercentAndTime
}

public enum ResumePositionMode
{
    Inherit,
    Remember,
    StartFromBeginning
}

public enum CollectionSortMode
{
    AddedNewest,
    Alphabetical,
    Custom
}

public enum RadioRecognitionScope
{
    CurrentStation,
    RecordingStations,
    CurrentAndRecordingStations
}

public static class RadioRecognitionScopeRules
{
    public static bool IncludesCurrentStation(RadioRecognitionScope scope) =>
        scope is RadioRecognitionScope.CurrentStation
            or RadioRecognitionScope.CurrentAndRecordingStations;

    public static bool IncludesRecordingStations(RadioRecognitionScope scope) =>
        scope is RadioRecognitionScope.RecordingStations
            or RadioRecognitionScope.CurrentAndRecordingStations;

    public static string GetLabel(RadioRecognitionScope scope) => scope switch
    {
        RadioRecognitionScope.RecordingStations => "tylko stacje nagrywane w tle",
        RadioRecognitionScope.CurrentAndRecordingStations =>
            "aktualnie odtwarzana stacja i wszystkie stacje nagrywane w tle",
        _ => "tylko aktualnie odtwarzana stacja"
    };
}

/// <summary>
/// Co ma zrobic Enter na wyniku wyszukiwania. Michal ustalil 20.09.2026:
/// domyslnie wynik ma sie OTWORZYC i zagrac, bez dopisywania go do Biblioteki.
/// Swiadome dodanie zostaje pod Ctrl+Shift+L. Jedno ustawienie dla wszystkich
/// serwisow (radio, podcasty, YouTube, TIDAL, Spotify i przyszle sesje) - nie
/// osobne per serwis, bo uzytkownik chce jednego przewidywalnego zachowania.
/// </summary>
public enum SearchResultEnterBehavior
{
    /// <summary>
    /// Otwiera albo odtwarza wynik bez zapisu do Biblioteki. Cache, historia i
    /// pozycja w sesji potrzebne do odtworzenia oraz powrotu fokusu NIE sa
    /// czlonkostwem w Bibliotece.
    /// </summary>
    OpenWithoutLibrary = 0,

    /// <summary>
    /// Dawne zachowanie: Enter dodatkowo dopisuje wynik do Biblioteki.
    /// </summary>
    AddToLibrary = 1
}

public sealed class AppSettings
{
    public string InterfaceLanguage { get; set; } = "pl-PL";

    /// <summary>
    /// Zachowanie Enter na wynikach wyszukiwania. Domyslnie otwieranie bez
    /// dodawania do Biblioteki.
    /// </summary>
    public SearchResultEnterBehavior SearchResultEnterBehavior { get; set; } =
        SearchResultEnterBehavior.OpenWithoutLibrary;
    public string PrefixChord { get; set; } = "Ctrl+Alt+Windows+F12";
    public int PrefixTimeoutMilliseconds { get; set; } = 3000;
    public int SessionContinuationMilliseconds { get; set; } = 2000;
    public StartupTarget StartupTarget { get; set; } = StartupTarget.MediaList;
    public string ActiveKeyboardProfileId { get; set; } = "default";
    public bool RememberLastSession { get; set; } = true;
    public bool PausePlaybackWhenLeavingPlayer { get; set; } = true;
    public bool FollowPlaybackOnPlayerExit { get; set; } = true;

    /// <summary>
    /// Wspolne podglady Alt+R (nagrywane stacje), Alt+Shift+R (historia
    /// nagrywania) i Ctrl+I (nowe odcinki) dostepne ze WSZYSTKICH sesji AMC.
    /// Wylaczenie zostawia kazdy z nich w sesji macierzystej.
    /// </summary>
    public bool GlobalTransientPreviews { get; set; } = true;
    public bool OpenPlayerWhenActivatingPreset { get; set; }
    public bool RememberLocalPlaybackPositions { get; set; } = true;

    /// <summary>
    /// Zachowywanie kopii pliku po UDANEJ edycji istniejacego nagrania: usuniecia
    /// zaznaczonego fragmentu z oryginalu oraz dopisania fragmentu na koncu
    /// istniejacego pliku. Domyslnie WYLACZONE, bo po sprawdzeniu pliku wynikowego
    /// kopia jest juz tylko duplikatem zajmujacym miejsce. Przy bledzie albo
    /// niepewnosci kopia zostaje zachowana NIEZALEZNIE od tego ustawienia.
    ///
    /// Ustawienie nie dotyczy zapisu fragmentu do NOWEGO pliku (tam nie ma czego
    /// nadpisac) i NIE sprzata kopii utworzonych wczesniej ani przy starcie —
    /// dotyczy wylacznie kopii powstajacej w danej edycji.
    /// </summary>
    public bool KeepAudioEditBackups { get; set; }

    /// <summary>
    /// Dlugosc przeskoku pod Alt+Ctrl+strzalka w lewo i w prawo, w sekundach.
    /// Osobna od stalych krokow 10/30/60 s - sluzy do przechodzenia po dlugich
    /// nagraniach (audycja, mecz, sluchowisko), dlatego domyslnie 5 minut.
    /// Dopuszczalny zakres pilnuje <see cref="PlaybackSeekRules"/>.
    /// </summary>
    public int CustomSeekSeconds { get; set; } = PlaybackSeekRules.DefaultCustomSeekSeconds;

    /// <summary>
    /// Zapamiętywanie pozycji odtwarzania ustawiane OSOBNO dla wybranej sesji
    /// (radio, podcasty, biblioteka lokalna, TIDAL). Klucz to identyfikator
    /// sesji. Brak wpisu albo <see cref="ResumePositionMode.Inherit"/> oznacza
    /// „jak ustawienie globalne" (<see cref="RememberLocalPlaybackPositions"/>),
    /// więc dotychczasowe konfiguracje działają bez zmian.
    /// </summary>
    public Dictionary<string, ResumePositionMode> ResumePositionModeBySession { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public PlaybackAudioSettings Audio { get; set; } = new();

    /// <summary>
    /// Ustawienia odtwarzania pozycji Spotify (pamiec pozycji per utwor, album
    /// i podcast). W AppSettings, a nie w PersistedState.Spotify, bo tamta
    /// sekcja trzyma konto i cache biblioteki przepisywany przy kazdym
    /// odswiezeniu, a te wybory maja przezyc odswiezenie.
    /// </summary>
    public SpotifyPlaybackSettings SpotifyPlayback { get; set; } = new();
    public SpotifyPlaybackSettings SpotifyLibrespotPlayback { get; set; } = new();
    public string? SpotifyLibrespotDeviceName { get; set; }

    /// <summary>
    /// Silnik odtwarzania JEDNEJ sesji Spotify. Domyslnie Librespot, bo to on
    /// gra cale utwory. Nieznana albo uszkodzona wartosc w pliku ustawien NIE
    /// przerywa odczytu konfiguracji - wraca Librespot
    /// (<see cref="Spotify.SpotifyPlaybackEngineJsonConverter"/>).
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(Spotify.SpotifyPlaybackEngineJsonConverter))]
    public Spotify.SpotifyPlaybackEngine SpotifyEngine { get; set; } =
        Spotify.SpotifyPlaybackEngine.Librespot;

    /// <summary>
    /// Znacznik wersji scalenia dwoch sesji Spotify w jedna. Zero oznacza plik
    /// sprzed scalenia. Wartosc pilnuje, by migracja wykonala sie DOKLADNIE raz:
    /// powtorne zlozenie kolejek albo prefsow zdublowalo by wpisy uzytkownika.
    /// </summary>
    public int SpotifySessionUnificationVersion { get; set; }

    /// <summary>
    /// Archiwum danych starej, osobnej sesji Librespot. Migracja jest
    /// NIEUSUWAJACA: zrodlowe kolejki, pozycje i listy zostaja tutaj w calosci,
    /// nawet gdy scalenie odrzucilo je jako konfliktowe.
    /// </summary>
    public SpotifyLegacySessionArchive? SpotifyLegacySession { get; set; }
    public string LastSessionId { get; set; } = "tidal";
    public Dictionary<int, string> SessionSlots { get; set; } = SessionSlotOrder.CreateDefault();
    public ListDisplaySettings Lists { get; set; } = new();
    public MessageSettings Messages { get; set; } = MessageSettings.CreateDefault();
    public UpdateSettings Updates { get; set; } = new();
    public ShellIntegrationSettings ShellIntegration { get; set; } = new();
}

public sealed class PlaybackAudioSettings
{
    public bool LoudnessNormalizationEnabled { get; set; }
    public bool SmoothTrackTransitionsEnabled { get; set; }
    public int InterTrackSilenceMilliseconds { get; set; }
    public bool AllSessionsMuted { get; set; }
    public Dictionary<string, bool> SessionMutedById { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> OutputDeviceIdsBySession { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Ustawienia odtwarzania osobno dla KAZDEJ sesji (cale TIDAL, cale radio,
    /// wszystkie pliki lokalne). Poziom sesji lezy MIEDZY folderem a ustawieniem
    /// ogolnym: plik -> folder -> sesja -> ustawienie ogolne. Brak wpisu oznacza
    /// "bez odstepstwa", czyli zejscie o poziom nizej.
    /// </summary>
    public Dictionary<string, SessionPlaybackAudioOverrides> OverridesBySession { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Odstepstwa od ustawien ogolnych dla jednej sesji. Kazde pole moze byc puste
/// (null) - wtedy sesja niczego nie narzuca.
/// </summary>
public sealed class SessionPlaybackAudioOverrides
{
    public bool? LoudnessNormalizationOverride { get; set; }
    public bool? SmoothTrackTransitionsOverride { get; set; }
    public int? InterTrackSilenceMillisecondsOverride { get; set; }

    /// <summary>
    /// Wstrzymywanie odtwarzania po wyjsciu z odtwarzacza USTAWIONE OSOBNO dla
    /// tej sesji. Puste (null) oznacza "jak ustawienie ogolne"
    /// (<see cref="AppSettings.PausePlaybackWhenLeavingPlayer"/>), wiec
    /// dotychczasowe konfiguracje dzialaja bez zmian. ZGLOSZENIE Michala:
    /// radio ma grac dalej po Escape, a pliki lokalne maja sie zatrzymywac.
    /// </summary>
    public bool? PausePlaybackWhenLeavingPlayerOverride { get; set; }

    public bool IsEmpty =>
        !LoudnessNormalizationOverride.HasValue
        && !SmoothTrackTransitionsOverride.HasValue
        && !InterTrackSilenceMillisecondsOverride.HasValue
        && !PausePlaybackWhenLeavingPlayerOverride.HasValue;
}

public static class PlaybackSeekRules
{
    /// <summary>Domyslny przeskok Alt+Ctrl+strzalki: 5 minut.</summary>
    public const int DefaultCustomSeekSeconds = 300;

    public const int MinimumCustomSeekSeconds = 5;

    /// <summary>Gorna granica: pol godziny. Wyzej przeskok mija sie z celem.</summary>
    public const int MaximumCustomSeekSeconds = 1800;

    /// <summary>
    /// Wartosci proponowane w Ustawieniach. Uzytkownik moze wpisac wlasna
    /// liczbe - lista jest wygoda, nie ograniczeniem.
    /// </summary>
    public static readonly IReadOnlyList<int> SuggestedCustomSeekSeconds =
        [15, 30, 60, 120, 180, 300, 600, 900, 1800];

    public static int NormalizeCustomSeekSeconds(int seconds) =>
        Math.Clamp(seconds, MinimumCustomSeekSeconds, MaximumCustomSeekSeconds);

    /// <summary>
    /// Czyta dlugosc przeskoku z tekstu wpisanego przez uzytkownika.
    /// Przyjmuje "5 minut", "5 min", "90 s", "90 sekund", "1:30" i samo "300".
    /// SAMA LICZBA BEZ JEDNOSTKI znaczy MINUTY, bo pole sluzy do dlugich
    /// przeskokow - "5" ma znaczyc 5 minut, nie 5 sekund. Zwraca false, gdy
    /// tekstu nie da sie odczytac; wtedy wywolujacy zostawia stara wartosc
    /// zamiast po cichu ja zerowac.
    /// </summary>
    public static bool TryParseSeekLength(string? text, out int seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var value = text.Trim().ToLowerInvariant().Replace(',', '.');

        var colon = value.IndexOf(':');
        if (colon > 0)
        {
            var minutePart = value[..colon].Trim();
            var secondPart = value[(colon + 1)..].Trim();
            if (!int.TryParse(minutePart, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var mm)
                || !int.TryParse(secondPart, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var ss)
                || mm < 0 || ss is < 0 or > 59)
            {
                return false;
            }
            seconds = NormalizeCustomSeekSeconds(mm * 60 + ss);
            return true;
        }

        var digits = new string(value.TakeWhile(character =>
            char.IsDigit(character) || character == '.').ToArray());
        if (digits.Length == 0) return false;
        if (!double.TryParse(digits, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        var unit = value[digits.Length..].Trim();
        var inSeconds = unit.StartsWith('s')
            ? number
            // Brak jednostki albo cokolwiek zaczynajace sie na "m" = minuty.
            : unit.Length == 0 || unit.StartsWith('m')
                ? number * 60
                : double.NaN;
        if (double.IsNaN(inSeconds) || inSeconds <= 0) return false;
        seconds = NormalizeCustomSeekSeconds((int)Math.Round(inSeconds));
        return true;
    }

    /// <summary>
    /// Opis dlugosci przeskoku po polsku, z poprawna odmiana. Uzywany i w
    /// Ustawieniach, i w opisie skrotow, wiec regula jest w JEDNYM miejscu.
    /// </summary>
    public static string DescribeSeekLength(int seconds)
    {
        seconds = NormalizeCustomSeekSeconds(seconds);
        if (seconds % 60 != 0) return $"{seconds} {PluralizeSeconds(seconds)}";
        var minutes = seconds / 60;
        return $"{minutes} {PluralizeMinutes(minutes)}";
    }

    private static string PluralizeSeconds(int value) => value switch
    {
        1 => "sekundę",
        _ when IsFewForm(value) => "sekundy",
        _ => "sekund"
    };

    private static string PluralizeMinutes(int value) => value switch
    {
        1 => "minutę",
        _ when IsFewForm(value) => "minuty",
        _ => "minut"
    };

    // Polska liczba mnoga: 2-4 (ale nie 12-14) bierze forme "minuty".
    private static bool IsFewForm(int value)
    {
        var lastTwo = value % 100;
        if (lastTwo is >= 12 and <= 14) return false;
        var last = value % 10;
        return last is >= 2 and <= 4;
    }
}

public static class PlaybackAudioSettingsRules
{
    public static readonly IReadOnlyList<int> SupportedInterTrackSilenceMilliseconds =
        [0, 500, 1000, 2000, 3000, 5000];

    public static bool IsSupportedSilence(int milliseconds) =>
        SupportedInterTrackSilenceMilliseconds.Contains(milliseconds);

    public static string GetInterTrackSilenceLabel(int milliseconds) => milliseconds switch
    {
        500 => "pół sekundy",
        1000 => "1 sekunda",
        2000 => "2 sekundy",
        3000 => "3 sekundy",
        5000 => "5 sekund",
        _ => "bez dodatkowej ciszy"
    };
}

public static class SessionSlotOrder
{
    private static readonly (string Id, string DisplayName)[] KnownSessions =
    [
        ("local", "Pliki lokalne"),
        ("wiim", "WiiM"),
        ("tidal", "TIDAL"),
        ("spotify", "Spotify"),
        ("appleMusic", "Apple Music"),
        ("radio", "Radio internetowe"),
        ("podcasts", "Podcasty i YouTube")
    ];

    public static IReadOnlyList<string> DefaultSessionIds =>
        KnownSessions.Select(session => session.Id).ToArray();

    // Spotify dopisany na KOŃCU domyślnej kolejności celowo. Wstawienie go
    // między istniejące sesje przesunęłoby numery slotów, a te są skrótami
    // Alt+cyfra, których użytkownik ma już wyuczone. Nowa sesja nie może
    // zmieniać znaczenia klawiszy, które ktoś zna na pamięć.
    public static Dictionary<int, string> CreateDefault() => new()
    {
        [1] = "local",
        [2] = "wiim",
        [3] = "tidal",
        [4] = "appleMusic",
        [5] = "radio",
        [6] = "podcasts",
        [7] = "spotify"
    };

    public static Dictionary<int, string> Normalize(IReadOnlyDictionary<int, string>? slots)
    {
        // Numery slotow to skroty Alt+cyfra, ktore uzytkownik zna na pamiec.
        // ZACHOWUJEMY wlasny numer kazdego zapisanego wpisu - nie wolno ich
        // przenumerowac "po kolei". Gdyby scalenie sesji Spotify zwolnilo numer
        // ze srodka (stara sesja Librespot), zageszczanie przesunelo by WSZYSTKIE
        // dalsze sesje i zmienilo znaczenie klawiszy, ktorych nikt nie zmienial.
        var zajete = new Dictionary<int, string>();
        var znaneId = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in (slots ?? new Dictionary<int, string>())
                 .Where(pair => pair.Key is >= 1 and <= 9 && !string.IsNullOrWhiteSpace(pair.Value))
                 .OrderBy(pair => pair.Key))
        {
            var sessionId = pair.Value.Trim();
            // Ten sam identyfikator dwa razy: zostaje przy NIZSZYM numerze.
            if (!znaneId.Add(sessionId)) continue;
            zajete[pair.Key] = sessionId;
        }

        // Sesje, ktore nigdzie nie maja numeru, dostaja pierwszy wolny.
        foreach (var session in KnownSessions)
        {
            if (znaneId.Contains(session.Id)) continue;
            var wolny = Enumerable.Range(1, 9).FirstOrDefault(slot => !zajete.ContainsKey(slot));
            if (wolny == 0) break;
            zajete[wolny] = session.Id;
            znaneId.Add(session.Id);
        }

        return zajete
            .OrderBy(pair => pair.Key)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    public static string GetDisplayName(string sessionId) =>
        string.Equals(sessionId, "spotifyLibrespot", StringComparison.OrdinalIgnoreCase)
            ? "Spotify — Librespot"
            : KnownSessions.FirstOrDefault(session =>
            string.Equals(session.Id, sessionId, StringComparison.OrdinalIgnoreCase)).DisplayName
        ?? sessionId;
}

public sealed class ListDisplaySettings
{
    public List<MediaItemField> FieldOrder { get; set; } = CreateDefaultFieldOrder();

    public static List<MediaItemField> CreateDefaultFieldOrder() =>
    [
        MediaItemField.Title,
        MediaItemField.Artist,
        MediaItemField.Duration,
        MediaItemField.Kind
    ];
}

public sealed class UpdateSettings
{
    public bool CheckAutomatically { get; set; } = true;
    public bool DownloadAutomatically { get; set; } = true;
    public bool InstallOnExit { get; set; } = true;
    public bool AllowMeteredConnection { get; set; }
    public string Channel { get; set; } = "stable";
}

/// <summary>
/// Wpiecie AMC w system: menu kontekstowe plikow i zgloszenie sie jako
/// mozliwosc otwarcia. Zapisujemy NAZWY GRUP, nie pojedyncze rozszerzenia -
/// gdy w nowej wersji dopiszemy do grupy format, uzytkownik dostanie go bez
/// zagladania w ustawienia.
///
/// Programem domyslnym AMC tu NIE zostaje: tego Windows nie pozwala ustawic
/// z programu (pilnuje wyboru uzytkownika skrotem kontrolnym i cofa zmiany).
/// Ustawienia tylko prowadza uzytkownika do systemowego okna wyboru.
/// </summary>
public sealed class ShellIntegrationSettings
{
    /// <summary>Czy pokazywac polecenia AMC w menu po kliknięciu prawym przyciskiem.</summary>
    public bool ContextMenuEnabled { get; set; }

    /// <summary>Nazwy wlaczonych grup rozszerzen.</summary>
    public List<string> EnabledGroups { get; set; } = [];

    /// <summary>
    /// Czy uzytkownik juz widzial pytanie o wpiecie w system. Bez tego pytanie
    /// wracaloby przy kazdym uruchomieniu, a odmowa nie bylaby szanowana.
    /// </summary>
    public bool SetupOffered { get; set; }
}

public sealed class MessageSettings
{
    public bool Enabled { get; set; } = true;
    public bool DetailedHints { get; set; }
    public bool SeekMessages { get; set; } = true;
    public bool ArrowSeekMessages { get; set; } = true;
    public bool PercentageSeekMessages { get; set; } = true;
    public bool BookmarkNavigationMessages { get; set; } = true;
    public PercentageSeekAnnouncementMode PercentageSeekAnnouncement { get; set; } = PercentageSeekAnnouncementMode.Percent;
    public bool SessionMessages { get; set; } = true;
    public bool HistoryMessages { get; set; } = true;
    public bool PlaybackMessages { get; set; } = true;
    public bool VolumeMessages { get; set; } = true;
    public bool AutomaticRecognitionMessages { get; set; } = true;
    public bool LoadingMessages { get; set; } = true;
    public bool ErrorMessages { get; set; } = true;
    public Dictionary<string, string> Templates { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static MessageSettings CreateDefault()
    {
        return new MessageSettings
        {
            Templates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["session.changed"] = "{slot}, {service}",
                ["session.unassigned"] = "Sesja {slot} nieprzypisana",
                ["favorite.added"] = "Dodano do ulubionych: {item}",
                ["favorite.removed"] = "Usunięto z ulubionych: {item}",
                ["queue.added"] = "Dodano do kolejki: {item}",
                ["queue.removed"] = "Usunięto z kolejki: {item}",
                ["playNext.added"] = "Odtwarzaj jako następne: {item}",
                ["playNext.removed"] = "Usunięto z następnych: {item}",
                ["volume.changed"] = "{value}%",
                ["time.elapsed"] = "{elapsed}",
                ["time.remaining"] = "{remaining}",
                ["time.total"] = "{total}",
                ["command.unavailable"] = "Polecenie niedostępne"
            }
        };
    }
}

public sealed class PersistedState
{
    public int SchemaVersion { get; set; } = 48;
    public AppSettings Settings { get; set; } = new();
    public SearchHistorySettings SearchHistory { get; set; } = new();
    public PlaybackHistorySettings PlaybackHistory { get; set; } = new();
    public BookmarkSettings Bookmarks { get; set; } = new();
    public SessionNavigationSettings SessionNavigation { get; set; } = new();
    public CollectionOrderSettings CollectionOrders { get; set; } = new();
    public PlaylistSettings Playlists { get; set; } = new();
    public SessionPresetSettings SessionPresets { get; set; } = new();
    public PlaybackVolumeMemorySettings PlaybackVolumes { get; set; } = new();
    public LocalMediaSettings LocalMedia { get; set; } = new();
    public RadioSettings Radio { get; set; } = new();
    public PodcastSettings Podcasts { get; set; } = new();
    public WiiMSettings WiiM { get; set; } = new();
    public TidalSettings Tidal { get; set; } = new();
    public SpotifySettings Spotify { get; set; } = new();
    public RemoteQueueCacheSettings RemoteQueues { get; set; } = new();
    public List<Input.KeyboardProfile> KeyboardProfiles { get; set; } = [Input.KeyboardProfile.CreateDefault()];
}

public sealed class RemoteQueueCacheSettings
{
    public Dictionary<string, List<RemoteQueueItemSettings>> ItemsBySession { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RemoteQueueItemSettings
{
    public string Id { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public MediaItemKind Kind { get; set; } = MediaItemKind.Track;
    public long DurationTicks { get; set; }
    public int? BitrateKbps { get; set; }
    public int? SampleRateHz { get; set; }
    public string? PublicUri { get; set; }
    public string? HomepageUri { get; set; }
    public string? Country { get; set; }
    public string? Language { get; set; }
    public string? Tags { get; set; }
    public string? Codec { get; set; }
    public string? RelatedAlbumExternalId { get; set; }
    public string? RelatedAlbumTitle { get; set; }
    public string? RelatedArtistExternalId { get; set; }
    public string? RelatedArtistName { get; set; }
    public bool IsInQueue { get; set; }
    public bool IsPlayNext { get; set; }

    public static RemoteQueueItemSettings FromMediaItem(string sessionId, MediaItem item) => new()
    {
        Id = TransientQueuePersistence.StorageItemId(sessionId, item),
        ExternalId = item.ExternalId,
        Title = item.Title,
        Artist = item.Artist,
        Kind = item.Kind,
        DurationTicks = item.Duration.Ticks,
        BitrateKbps = item.BitrateKbps,
        SampleRateHz = item.SampleRateHz,
        PublicUri = item.PublicUri,
        HomepageUri = item.HomepageUri,
        Country = item.Country,
        Language = item.Language,
        Tags = item.Tags,
        Codec = item.Codec,
        RelatedAlbumExternalId = item.RelatedAlbumExternalId,
        RelatedAlbumTitle = item.RelatedAlbumTitle,
        RelatedArtistExternalId = item.RelatedArtistExternalId,
        RelatedArtistName = item.RelatedArtistName,
        IsInQueue = item.IsInQueue,
        IsPlayNext = item.IsPlayNext
    };

    public MediaItem ToMediaItem() => new()
    {
        Id = Id,
        ExternalId = ExternalId,
        Title = Title,
        Artist = Artist,
        Kind = Kind,
        Duration = TimeSpan.FromTicks(Math.Max(0, DurationTicks)),
        BitrateKbps = BitrateKbps,
        SampleRateHz = SampleRateHz,
        PublicUri = PublicUri,
        HomepageUri = HomepageUri,
        Country = Country,
        Language = Language,
        Tags = Tags,
        Codec = Codec,
        RelatedAlbumExternalId = RelatedAlbumExternalId,
        RelatedAlbumTitle = RelatedAlbumTitle,
        RelatedArtistExternalId = RelatedArtistExternalId,
        RelatedArtistName = RelatedArtistName,
        IsInQueue = IsInQueue,
        IsPlayNext = IsPlayNext
    };
}

public sealed class TidalSettings
{
    // The application secret is deliberately not persisted. Installed desktop
    // clients use Authorization Code + PKCE and the user's default browser.
    public string ClientId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = "http://127.0.0.1:43821/tidal/callback/";
    public string CountryCode { get; set; } = "PL";
    public string AccountDisplayName { get; set; } = string.Empty;
    // User-interface preference only. The opaque identifier is never exposed
    // through an accessible label and is harmless when the playlist is later
    // removed on another device: the picker simply falls back to its first row.
    public string? LastPlaylistExternalId { get; set; }
    public int Volume { get; set; } = 35;
    // 2026-09-11: odtwarzanie przez zainstalowaną aplikację TIDAL zamiast
    // wbudowanego odtwarzacza. ZMIERZONE i WYŁĄCZONE domyślnie: adres
    // tidal://track/<id> NIE wybiera wskazanego utworu — aplikacja wznawia to,
    // co miała zapamiętane, albo otwiera inny utwór tego wykonawcy. Żaden
    // wariant adresu (tracks/, play/track/, listen.tidal.com) nie działa.
    // Dopóki nie znajdziemy sposobu wskazania utworu, włączenie tego dawałoby
    // pełne odtwarzanie NIE TEGO utworu, który wybrał użytkownik — a to gorsze
    // niż trzydziestosekundowa próbka wbudowanego odtwarzacza.
    public bool UseDesktopApp { get; set; }
    public long LastSuccessfulSyncUtcTicks { get; set; }
    // Last complete or safely merged collection snapshot. It deliberately
    // contains no access or refresh token; those remain in Windows Credential
    // Manager. Keeping the catalogue here prevents a temporary authentication
    // or network failure from turning the user's TIDAL Library into an empty
    // view at the next application start.
    public List<TidalCachedCollectionItemSettings> CachedCollectionItems { get; set; } = [];
}

public sealed class SpotifySettings
{
    // Sekret aplikacji celowo NIE jest przechowywany. Klient desktop używa
    // Authorization Code + PKCE, więc sekret nie jest potrzebny, a zapisany
    // w pliku ustawień byłby jawny dla każdego, kto ma dostęp do dysku.
    public string ClientId { get; set; } = string.Empty;
    // Spotify wymaga, by adres powrotu był wpisany znak w znak w panelu
    // aplikacji. Pętla zwrotna po HTTP jest dozwolona i wystarcza - patrz
    // dokumentacja adresów powrotu Spotify. Musi to być 127.0.0.1, NIE
    // "localhost": Spotify odrzuca "localhost" od 2025 roku.
    public string RedirectUri { get; set; } = "http://127.0.0.1:43822/spotify/callback/";
    public string CountryCode { get; set; } = "PL";
    public string AccountDisplayName { get; set; } = string.Empty;
    // Rodzaj konta zwrócony przez Spotify ("premium", "free", "open").
    // Zapamiętany, by po ponownym uruchomieniu od razu wiedzieć, czy
    // wbudowany odtwarzacz ma sens, bez pytania serwera.
    public string AccountProduct { get; set; } = string.Empty;
    public string GrantedScope { get; set; } = string.Empty;
    public int Volume { get; set; } = 35;
    public long LastSuccessfulSyncUtcTicks { get; set; }
    // Zapamietana biblioteka, zeby po ponownym uruchomieniu sesja Spotify nie
    // byla pusta do czasu ponownego pobrania. Uzyty jest ten sam kszalt zapisu
    // co dla TIDAL - to swiadome wspoldzielenie formatu, nie pomylka nazwy.
    public List<TidalCachedCollectionItemSettings> CachedCollectionItems { get; set; } = new();
}

/// <summary>
/// Rzeczywiste ustawienia odtwarzania pozycji Spotify. Osobna sekcja, nie czesc
/// <see cref="SpotifySettings"/>, bo SpotifySettings trzyma konto i cache
/// biblioteki: cache jest przepisywany przy kazdym odswiezeniu i wybory
/// uzytkownika ginely by razem z nim.
///
/// Sekcja NIE zawiera zadnych pol przetwarzania dzwieku. Spotify nie
/// przechodzi przez nasz lancuch DSP, nie ma zmiany tempa, a polityka uslugi
/// zabrania crossfade - zapisane "wlaczone", ktorego nikt nie odczyta, byloby
/// martwa kontrolka.
/// </summary>
public sealed class SpotifyPlaybackSettings
{
    /// <summary>
    /// Ustawienia pojedynczych pozycji. Klucz to STABILNY adres uslugi
    /// (np. "spotify:track:..."), nigdy losowe MediaItem.Id, ktore zmienia sie
    /// przy kazdym pobraniu biblioteki.
    /// </summary>
    public Dictionary<string, SpotifyItemPlaybackSettings> ItemsByKey { get; set; } =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Ustawienia albumow, podcastow i list. Poziom miedzy pojedyncza pozycja
    /// a sesja - "caly ten podcast od poczatku" bez klikania kazdego odcinka.
    /// </summary>
    public Dictionary<string, ResumePositionMode> ContainersByKey { get; set; } =
        new(StringComparer.Ordinal);
}

public sealed class SpotifyItemPlaybackSettings
{
    public ResumePositionMode ResumePositionMode { get; set; } = ResumePositionMode.Inherit;

    /// <summary>
    /// Zapamietana pozycja odtwarzania w tickach. Zero oznacza brak pozycji.
    /// Zapisywana w tickach, a nie w sekundach, zeby powrot po restarcie
    /// trafial w to samo miejsce, w ktorym uzytkownik przerwal.
    /// </summary>
    public long PositionTicks { get; set; }
}

/// <summary>
/// Pelny zapis danych starej, osobnej sesji Librespot ("spotifyLibrespot")
/// zachowany PRZED scaleniem. Migracja nie usuwa niczego: wszystko, czego nie
/// dalo sie bezkonfliktowo wniesc do kanonicznej sesji "spotify", zostaje tutaj
/// i nadal daje sie odczytac. To jest archiwum, nie zrodlo dla odtwarzacza.
/// </summary>
public sealed class SpotifyLegacySessionArchive
{
    /// <summary>Identyfikator sesji, z ktorej pochodza dane ("spotifyLibrespot").</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>Numer slotu, ktory sesja zajmowala przed scaleniem, albo zero.</summary>
    public int Slot { get; set; }

    public long ArchivedUtcTicks { get; set; }

    /// <summary>Czy stara sesja Librespot byla ostatnio uzywana. To ona wygrywa prefsy.</summary>
    public bool WasLastSession { get; set; }

    public SpotifyPlaybackSettings Playback { get; set; } = new();
    public ResumePositionMode SessionResumePositionMode { get; set; } = ResumePositionMode.Inherit;
    public bool? Muted { get; set; }
    public string? OutputDeviceId { get; set; }
    public SessionPlaybackAudioOverrides? AudioOverrides { get; set; }
    public string? DeviceName { get; set; }

    public List<RemoteQueueItemSettings> RemoteQueue { get; set; } = [];
    public SessionNavigationState? Navigation { get; set; }
    public Dictionary<string, List<string>> CollectionOrders { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public List<string> PlaybackHistoryItemIds { get; set; } = [];
    public List<string> SearchHistory { get; set; } = [];
    public List<SessionPresetEntry> Presets { get; set; } = [];
    public List<PlaylistEntry> Playlists { get; set; } = [];
    public List<BookmarkEntry> Bookmarks { get; set; } = [];
    public List<PlaybackVolumeMemoryEntry> Volumes { get; set; } = [];

    /// <summary>
    /// Czytelny zapis tego, co scalenie odrzucilo z powodu konfliktu. Sluzy do
    /// odpowiedzi na pytanie "gdzie sie podziala moja pozycja", bez zagadywania.
    /// </summary>
    public List<string> ConflictNotes { get; set; } = [];
}

public sealed class TidalCachedCollectionItemSettings
{
    public string Id { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public MediaItemKind Kind { get; set; } = MediaItemKind.Track;
    // Source trzyma adres dla odtwarzacza (np. "spotify:track:..."). Bez zapisu
    // tego pola zapamietana biblioteka wraca po restarcie bez tego, czym gra:
    // pozycje sa widoczne na liscie, ale nie da sie ich odtworzyc.
    public string? Source { get; set; }
    public long DurationTicks { get; set; }
    public int? BitrateKbps { get; set; }
    public int? SampleRateHz { get; set; }
    public string? PublicUri { get; set; }
    public string? HomepageUri { get; set; }
    public string? Country { get; set; }
    public string? Language { get; set; }
    public string? Tags { get; set; }
    public string? Codec { get; set; }
    public string? RelatedAlbumExternalId { get; set; }
    public string? RelatedAlbumTitle { get; set; }
    public string? RelatedArtistExternalId { get; set; }
    public string? RelatedArtistName { get; set; }
    public long? CollectionAddedUtcTicks { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsInLibrary { get; set; }
    public bool IsAvailable { get; set; } = true;

    public static TidalCachedCollectionItemSettings FromMediaItem(MediaItem item) => new()
    {
        Id = item.Id,
        ExternalId = item.ExternalId,
        Title = item.Title,
        Artist = item.Artist,
        Kind = item.Kind,
        Source = item.Source,
        DurationTicks = item.Duration.Ticks,
        BitrateKbps = item.BitrateKbps,
        SampleRateHz = item.SampleRateHz,
        PublicUri = item.PublicUri,
        HomepageUri = item.HomepageUri,
        Country = item.Country,
        Language = item.Language,
        Tags = item.Tags,
        Codec = item.Codec,
        RelatedAlbumExternalId = item.RelatedAlbumExternalId,
        RelatedAlbumTitle = item.RelatedAlbumTitle,
        RelatedArtistExternalId = item.RelatedArtistExternalId,
        RelatedArtistName = item.RelatedArtistName,
        CollectionAddedUtcTicks = item.CollectionAddedUtcTicks,
        IsFavorite = item.IsFavorite,
        IsInLibrary = item.IsInLibrary,
        IsAvailable = item.IsAvailable
    };

    public MediaItem ToMediaItem() => new()
    {
        Id = Id,
        ExternalId = ExternalId,
        Title = Title,
        Artist = Artist,
        Kind = Kind,
        Source = Source,
        Duration = TimeSpan.FromTicks(Math.Max(0, DurationTicks)),
        BitrateKbps = BitrateKbps,
        SampleRateHz = SampleRateHz,
        PublicUri = PublicUri,
        HomepageUri = HomepageUri,
        Country = Country,
        Language = Language,
        Tags = Tags,
        Codec = Codec,
        RelatedAlbumExternalId = RelatedAlbumExternalId,
        RelatedAlbumTitle = RelatedAlbumTitle,
        RelatedArtistExternalId = RelatedArtistExternalId,
        RelatedArtistName = RelatedArtistName,
        CollectionAddedUtcTicks = CollectionAddedUtcTicks,
        IsFavorite = IsFavorite,
        IsInLibrary = IsInLibrary,
        IsAvailable = IsAvailable
    };
}

public sealed class WiiMSettings
{
    public List<WiiMDeviceSettings> Devices { get; set; } = [];
    public List<WiiMNetworkStreamSettings> NetworkStreams { get; set; } = [];
    public string? SelectedDeviceId { get; set; }
}

public sealed class WiiMNetworkStreamSettings
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
    public long AddedUtcTicks { get; set; }
}

public sealed class WiiMDeviceSettings
{
    public string Id { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Firmware { get; set; } = string.Empty;
    public long LastSeenUtcTicks { get; set; }
    public int LastActivatedPresetNumber { get; set; }
    public string? LastActivatedNetworkStreamId { get; set; }
}

public sealed class PlaybackVolumeMemorySettings
{
    public List<PlaybackVolumeMemoryEntry> Entries { get; set; } = [];
}

public sealed class PlaybackVolumeMemoryEntry
{
    public string SessionId { get; set; } = string.Empty;
    public string ContextId { get; set; } = string.Empty;
    public string OutputDeviceId { get; set; } = string.Empty;
    public int Volume { get; set; } = 35;
}

public sealed class PodcastSettings
{
    public List<PodcastSubscriptionSettings> Subscriptions { get; set; } = [];
    public List<PodcastEpisodeSettings> Episodes { get; set; } = [];
    public string? DownloadsFolder { get; set; }
    public string? CurrentItemId { get; set; }
    public int Volume { get; set; } = 35;
    public double PlaybackRate { get; set; } = 1d;

    /// <summary>
    /// Domyslny odstep automatycznego odswiezania nowo dodawanych kanalow RSS,
    /// w minutach; 0 wylacza automat. Podcasty sa tanie w sprawdzaniu (jedno
    /// pobranie XML), wiec domyslnie co godzine.
    /// </summary>
    public int RssRefreshIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// To samo dla kanalow i playlist YouTube. Trzymane OSOBNO, bo sprawdzenie
    /// zrodla YouTube uruchamia yt-dlp i jest wielokrotnie drozsze niz RSS -
    /// uzytkownik musi moc je rozrzedzic bez rozrzedzania podcastow.
    /// </summary>
    public int YouTubeRefreshIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Nadaje odstep odswiezania zrodlom w bibliotece, ktore go nie maja.
    /// Wersje do 353 ustawialy odstep TYLKO kanalom YouTube, wiec kanaly RSS
    /// dodane wczesniej zostawaly z zerem i automat nigdy ich nie sprawdzal.
    /// Zwraca liczbe naprawionych zrodel; nie rusza tych, ktorym uzytkownik
    /// swiadomie ustawil wlasny odstep.
    /// </summary>
    public int ApplyDefaultRefreshIntervals()
    {
        var repaired = 0;
        foreach (var subscription in Subscriptions)
        {
            if (!subscription.IsInLibrary || subscription.RefreshIntervalMinutes > 0) continue;
            var interval = subscription.SourceKind switch
            {
                PodcastSourceKind.Rss => RssRefreshIntervalMinutes,
                PodcastSourceKind.YouTubeChannel or PodcastSourceKind.YouTubePlaylist =>
                    YouTubeRefreshIntervalMinutes,
                _ => 0
            };
            if (interval <= 0) continue;
            subscription.RefreshIntervalMinutes = interval;
            repaired++;
        }

        return repaired;
    }

    /// <summary>
    /// Ile zrodel wolno sprawdzic w jednym przebiegu automatu. Chroni interfejs
    /// przed dluga kolejka yt-dlp; pozostale zrodla biora kolejne przebiegi.
    /// </summary>
    public int AutomaticRefreshBatchSize { get; set; } = 4;
}

public enum PodcastSourceKind
{
    Rss,
    PublicInternetMedia,
    YouTubeChannel,
    YouTubePlaylist
}

public sealed class PodcastSubscriptionSettings
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool HasCustomTitle { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FeedUrl { get; set; } = string.Empty;
    public PodcastSourceKind SourceKind { get; set; }
    public string? HomepageUrl { get; set; }
    public long LastRefreshUtcTicks { get; set; }
    public int RefreshIntervalMinutes { get; set; }
    public string? DownloadsFolder { get; set; }
    public ResumePositionMode ResumePositionMode { get; set; } = ResumePositionMode.Inherit;
    public double? PlaybackRateOverride { get; set; }
    public bool? LoudnessNormalizationOverride { get; set; }
    public bool? SmoothTrackTransitionsOverride { get; set; }
    public int? InterTrackSilenceMillisecondsOverride { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsInLibrary { get; set; } = true;
}

public sealed class PodcastEpisodeSettings
{
    public string Id { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string SourceIdentifier { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string MediaUrl { get; set; } = string.Empty;
    public string? PageUrl { get; set; }
    public string? MediaType { get; set; }
    public long? MediaLength { get; set; }
    public string? ProviderChaptersUrl { get; set; }
    public string? ProviderChaptersLoadedUrl { get; set; }
    public string? EmbeddedChaptersSignature { get; set; }
    public bool HasFeedChapters { get; set; }
    public long PublishedUtcTicks { get; set; }
    public int? FeedOrdinal { get; set; }
    public long DurationTicks { get; set; }
    public long ResumePositionTicks { get; set; }
    public ResumePositionMode ResumePositionMode { get; set; } = ResumePositionMode.Inherit;
    public double? PlaybackRateOverride { get; set; }
    public bool? LoudnessNormalizationOverride { get; set; }
    public bool? SmoothTrackTransitionsOverride { get; set; }
    public int? InterTrackSilenceMillisecondsOverride { get; set; }
    public string? DownloadPath { get; set; }
    public bool IsNew { get; set; } = true;
    public bool IsStarted { get; set; }
    public bool IsPlayed { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsInQueue { get; set; }
    public bool IsPlayNext { get; set; }
    public long? ClipStartTicks { get; set; }
    public long? ClipEndTicks { get; set; }
}

public sealed class SessionPresetSettings
{
    public Dictionary<string, List<SessionPresetEntry>> EntriesBySession { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SessionPresetEntry
{
    public int Slot { get; set; }
    public string TargetId { get; set; } = string.Empty;
    public string TargetKind { get; set; } = string.Empty;
    public string TargetTitle { get; set; } = string.Empty;
    public string? TargetLocation { get; set; }
}

public sealed class SearchHistorySettings
{
    public Dictionary<string, List<string>> Entries { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PlaybackHistorySettings
{
    public Dictionary<string, List<string>> ItemIdsBySession { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class BookmarkSettings
{
    public List<BookmarkEntry> Entries { get; set; } = [];
}

[Flags]
public enum BookmarkPurpose
{
    None = 0,
    Bookmark = 1,
    Chapter = 2
}

public enum ChapterOrigin
{
    User,
    Provider
}

public sealed class BookmarkEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SessionId { get; set; } = string.Empty;
    public string SessionName { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string ItemTitle { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long PositionTicks { get; set; }
    public long CreatedUtcTicks { get; set; } = DateTime.UtcNow.Ticks;
    public BookmarkPurpose Purpose { get; set; } = BookmarkPurpose.Bookmark;
    public ChapterOrigin ChapterOrigin { get; set; } = ChapterOrigin.User;
    public string? ChapterSourceId { get; set; }
}

public sealed class SessionNavigationSettings
{
    public Dictionary<string, SessionNavigationState> Sessions { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SessionNavigationState
{
    public string CurrentView { get; set; } = "Multimedia";
    public string LastLibraryView { get; set; } = "Biblioteka";
    public Dictionary<string, string?> SelectedItemIds { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Filters { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, CollectionSortMode> CollectionSortModes { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public string PlaybackContextView { get; set; } = "Multimedia";
    public List<string> PlaybackContextItemIds { get; set; } = [];
    public bool PlayerActive { get; set; }
}

public sealed class CollectionOrderSettings
{
    public Dictionary<string, List<string>> FavoriteAddedItemIdsBySession { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> FavoriteItemIdsBySession { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> LibraryAddedItemIdsBySession { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> LibraryItemIdsBySession { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> QueueItemIdsBySession { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> QueueRegularItemIdsBySession { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> QueuePlayNextItemIdsBySession { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PlaylistSettings
{
    public List<PlaylistEntry> Entries { get; set; } = [];
}

public sealed class PlaylistEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SessionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long CreatedUtcTicks { get; set; } = DateTime.UtcNow.Ticks;
    public List<string> ItemIds { get; set; } = [];
}

public sealed class LocalMediaSettings
{
    public List<LocalMediaItemSettings> Items { get; set; } = [];
    public List<LocalFolderSourceSettings> FolderSources { get; set; } = [];
    public List<LocalFolderPlaybackSettings> FolderPlaybackOptions { get; set; } = [];
    public List<string> ExcludedPaths { get; set; } = [];
    public List<string> CustomOrderItemIds { get; set; } = [];
    public string LibraryView { get; set; } = "Foldery";
    public string? CurrentFolderPath { get; set; }
    public string? CurrentItemId { get; set; }
    public int Volume { get; set; } = 35;
    public double PlaybackRate { get; set; } = 1d;
}

public sealed class LocalFolderSourceSettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Path { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ResumePositionMode ResumePositionMode { get; set; } = ResumePositionMode.Inherit;
}

public sealed class LocalFolderPlaybackSettings
{
    public string Path { get; set; } = string.Empty;
    public ResumePositionMode ResumePositionMode { get; set; } = ResumePositionMode.Inherit;
    public double? PlaybackRateOverride { get; set; }
    public string? OutputDeviceId { get; set; }
    public bool? LoudnessNormalizationOverride { get; set; }
    public bool? SmoothTrackTransitionsOverride { get; set; }
    public int? InterTrackSilenceMillisecondsOverride { get; set; }
}

public sealed class LocalMediaItemSettings
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool HasCustomTitle { get; set; }
    public string Path { get; set; } = string.Empty;
    public long DurationTicks { get; set; }
    public int? BitrateKbps { get; set; }
    public bool IsBitrateEstimated { get; set; }
    public int? SampleRateHz { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsInLibrary { get; set; } = true;
    public bool IsAvailable { get; set; } = true;
    public bool IsInQueue { get; set; }
    public bool IsPlayNext { get; set; }
    public ResumePositionMode ResumePositionMode { get; set; } = ResumePositionMode.Inherit;
    public double? PlaybackRateOverride { get; set; }
    public string? OutputDeviceId { get; set; }
    public bool? LoudnessNormalizationOverride { get; set; }
    public bool? SmoothTrackTransitionsOverride { get; set; }
    public int? InterTrackSilenceMillisecondsOverride { get; set; }
    public long ResumePositionTicks { get; set; }
    public long? ClipStartTicks { get; set; }
    public long? ClipEndTicks { get; set; }
    public long? FileLength { get; set; }
    public long? LastWriteUtcTicks { get; set; }
    public bool IsRadioRecording { get; set; }
    public long RadioRecordingCompletedUtcTicks { get; set; }
}

public sealed class RadioSettings
{
    public List<RadioStationSettings> Stations { get; set; } = [];
    public List<RadioPresetSettings> Presets { get; set; } = [];
    public string? CurrentItemId { get; set; }
    public int Volume { get; set; } = 35;
    public int TimeshiftMinutes { get; set; } = 10;
    public string RecordingsFolder { get; set; } = string.Empty;
    public bool UsePodcastDownloadsFolderForRecordings { get; set; }

    /// <summary>
    /// Ktory folder podpowiadac w NOWYM harmonogramie, gdy stacja ma wlasny folder:
    /// <c>true</c> = folder tej stacji, <c>false</c> = folder domyslny.
    /// DECYZJA Michala 17.09.2026 - o tym ma decydowac uzytkownik w ustawieniach,
    /// a nie program na sztywno.
    /// </summary>
    public bool PreferStationFolderInNewSchedules { get; set; } = true;
    public RadioRecordingFormat RecordingFormat { get; set; } = RadioRecordingFormat.Mp3;
    public int RecordingBitrateKbps { get; set; } = 192;
    public bool WakeScheduledRecordings { get; set; }
    public bool AutomaticTrackRecognitionEnabled { get; set; }
    public RadioRecognitionScope AutomaticTrackRecognitionScope { get; set; } =
        RadioRecognitionScope.CurrentStation;
    public List<RadioRecordingScheduleSettings> RecordingSchedules { get; set; } = [];
    public List<RadioRecognizedTrackSettings> RecognizedTracks { get; set; } = [];
    public List<RadioRecordingHistorySettings> RecordingHistory { get; set; } = [];
}

/// <summary>
/// Jeden wpis historii nagrywania. Nagranie nieudane nie zostawia pliku, więc
/// bez takiego wpisu przepadałoby bez śladu i użytkownik nie wiedziałby, że
/// próba w ogóle się odbyła. Dlatego historia jest osobna od biblioteki.
/// </summary>
public sealed class RadioRecordingHistorySettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string StationId { get; set; } = string.Empty;
    public string StationName { get; set; } = string.Empty;
    /// <summary>Pusta dla nagrań nieudanych, które nie utworzyły pliku.</summary>
    public string Path { get; set; } = string.Empty;
    public RadioRecordingOutcome Outcome { get; set; } = RadioRecordingOutcome.Completed;
    /// <summary>Powód niepowodzenia albo przerwania; puste dla nagrań udanych.</summary>
    public string Reason { get; set; } = string.Empty;
    /// <summary>Nazwa harmonogramu, jeśli nagranie pochodziło z terminarza.</summary>
    public string ScheduleName { get; set; } = string.Empty;
    public long StartedUtcTicks { get; set; }
    public long FinishedUtcTicks { get; set; } = DateTime.UtcNow.Ticks;
    /// <summary>Liczba plików zapisanych w tej próbie; 0 gdy nic nie powstało.</summary>
    public int SavedFileCount { get; set; }
}

public enum RadioRecordingOutcome
{
    /// <summary>Nagranie zakończone poprawnie, plik istnieje.</summary>
    Completed,
    /// <summary>Użytkownik zatrzymał nagranie; zapisano to, co odebrano.</summary>
    Stopped,
    /// <summary>Nagranie przerwane w trakcie, plik może być niepełny.</summary>
    Interrupted,
    /// <summary>Nagranie nie powstało w ogóle.</summary>
    Failed
}

public sealed class RadioRecognizedTrackSettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string StationId { get; set; } = string.Empty;
    public string StationName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string ReleaseDate { get; set; } = string.Empty;
    public string? ProviderUri { get; set; }
    public long RecognizedUtcTicks { get; set; } = DateTime.UtcNow.Ticks;
}

public enum RadioRecordingFormat
{
    Mp3,
    Aac,
    Flac,
    Original,
    Wav
}

public enum RadioScheduleRecurrence
{
    Once,
    Daily,
    SelectedDays
}

public sealed class RadioRecordingScheduleSettings
{
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string StationId { get; set; } = string.Empty;
    public string StationName { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
    public long NextStartUtcTicks { get; set; }
    public string TimeZoneId { get; set; } = TimeZoneInfo.Local.Id;
    public int DurationMinutes { get; set; } = 60;
    public int SegmentMinutes { get; set; }
    public RadioScheduleRecurrence Recurrence { get; set; }
    public List<DayOfWeek> ActiveDays { get; set; } = [];
    public string OutputFolder { get; set; } = string.Empty;
    public string FileNameTemplate { get; set; } = RadioRecordingFileNameTemplate.DefaultTemplate;
    public RadioRecordingFormat? RecordingFormat { get; set; }
    public int? RecordingBitrateKbps { get; set; }
    public bool? WakeComputer { get; set; }
    public bool Enabled { get; set; } = true;
    /// <summary>
    /// Start bieżącego wystąpienia, które użytkownik zatrzymał ręcznie.
    /// Wartość jest zapisywana, aby po ponownym uruchomieniu AMC nagranie
    /// nie rozpoczęło się ponownie w tym samym oknie czasowym.
    /// </summary>
    public long? SuppressedOccurrenceStartUtcTicks { get; set; }
    public long? LastFailureUtcTicks { get; set; }
    public string LastFailureMessage { get; set; } = string.Empty;
    public bool LastFailureAcknowledged { get; set; } = true;
}

public sealed class RadioPresetSettings
{
    public int Slot { get; set; }
    public string StationId { get; set; } = string.Empty;
}

public static class RadioPresetSlots
{
    public const int Count = 12;

    public static string Label(int slot)
    {
        if (slot is < 1 or > Count) throw new ArgumentOutOfRangeException(nameof(slot));
        return slot.ToString(CultureInfo.InvariantCulture);
    }

    public static string ShortcutLabel(int slot) => slot switch
    {
        >= 1 and <= 9 => slot.ToString(CultureInfo.InvariantCulture),
        10 => "0",
        11 => "-",
        12 => "=",
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    public static string SpokenShortcutLabel(int slot) => slot switch
    {
        >= 1 and <= 10 => ShortcutLabel(slot),
        11 => "minus",
        12 => "znak równości",
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    public static IReadOnlyList<RadioPresetSettings> Normalize(
        IEnumerable<RadioPresetSettings>? presets,
        IReadOnlySet<string> stationIds) =>
        (presets ?? [])
            .Where(preset => preset.Slot is >= 1 and <= Count
                && !string.IsNullOrWhiteSpace(preset.StationId)
                && stationIds.Contains(preset.StationId.Trim()))
            .Select(preset => new RadioPresetSettings
            {
                Slot = preset.Slot,
                StationId = preset.StationId.Trim()
            })
            .GroupBy(preset => preset.Slot)
            .Select(group => group.First())
            .OrderBy(preset => preset.Slot)
            .ToArray();
}

public sealed class RadioStationSettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string StreamUrl { get; set; } = string.Empty;
    public string? HomepageUrl { get; set; }
    public string? Country { get; set; }
    public string? Language { get; set; }
    public string? Tags { get; set; }
    public string? Codec { get; set; }
    public string? DirectoryId { get; set; }
    public int? BitrateKbps { get; set; }
    public bool IsBitrateEstimated { get; set; }
    public int? SampleRateHz { get; set; }
    public int? Volume { get; set; }
    public bool HasCustomTitle { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsInLibrary { get; set; }
    public bool IsInQueue { get; set; }
    public bool IsPlayNext { get; set; }
    public bool IsCustom { get; set; }

    /// <summary>
    /// Zapasowy adres strumienia. Gdy glowny nie odpowiada, program probuje tego.
    /// ZGLOSZENIE Michala 15.09.2026 - stacje czasem zmieniaja adres albo maja
    /// drugi serwer; bez tego trzeba bylo poprawiac stacje recznie.
    /// </summary>
    public string? BackupStreamUrl { get; set; }

    /// <summary>
    /// Wlasny folder nagran tylko dla tej stacji. Puste = folder ogolny
    /// z ustawien nagrywania. ZGLOSZENIE Michala 15.09.2026.
    /// </summary>
    public string? RecordingFolder { get; set; }
}
