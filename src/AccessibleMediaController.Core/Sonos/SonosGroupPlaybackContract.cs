using System;
using System.Collections.Generic;
using System.Globalization;

namespace AccessibleMediaController.Core.Sonos;

/// <summary>
/// Kontrakt ODCZYTU stanu odtwarzania, metadanych i glosnosci GRUPY oraz
/// PODSTAWOWYCH polecen grupy z OFICJALNEGO Sonos Control API.
///
/// Zrodlo: definicje OpenAPI "Sonos Control API (cloud)" zachowane lokalnie
/// (playback-getplaybackstatus-groupid, playbackmetadata-getmetadatastatus-groupid,
/// groupvolume-getvolume-groupid, playback-play/pause/togglePlayPause/
/// skipToNextTrack/skipToPreviousTrack/seek/seekRelative,
/// groupvolume-setvolume/setmute/setrelativevolume) oraz SONOS_CONTROL_API_PL.md i
/// docs/SONOS_CONTROL_READ_PL.md. Wszystkie limity dlugosci i zakresy liczbowe
/// pochodza z pol maxLength/minimum/maximum tych definicji, nie z domyslow.
///
/// Granice tego etapu: transport + modele + walidacja argumentow. NIE ma tu
/// koordynatora uwierzytelnienia zapisu, pollingu, subskrypcji, sesji
/// odtwarzania, ulubionych, kolejki ani EQ.
/// </summary>
public static class SonosGroupIdPolicy
{
    /// <summary>group.id: maxLength 35 w definicji OpenAPI (schema "group").</summary>
    public const int MaxLength = 35;

    public static bool IsAcceptable(string? groupId) => TryEncode(groupId, out _);

    /// <summary>
    /// Zwraca bezpieczna postac groupId do WSTAWIENIA jako jeden segment sciezki.
    ///
    /// Identyfikatory grup Sonos zawieraja dwukropek (przewodnik "Control" pokazuje
    /// RINCON_00012345678001400:0), a dwukropek jest legalnym znakiem segmentu
    /// sciezki (pchar w RFC 3986), wiec zostaje LITERALNIE - procent-kodowanie
    /// zmienialoby adres, ktorego Sonos oczekuje. Bezpieczenstwo daje bialy lista
    /// znakow: wszystko poza nia (w szczegolnosci '/', '\', '%', '?', '#', '@')
    /// jest odrzucane, zamiast naprawiane.
    /// </summary>
    public static bool TryEncode(string? groupId, out string? encoded)
    {
        encoded = null;
        if (string.IsNullOrEmpty(groupId) || groupId.Length > MaxLength)
        {
            return false;
        }

        var onlyDots = true;
        foreach (var character in groupId)
        {
            var allowed = character is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '_'
                or '-'
                or '.'
                or ':';
            if (!allowed)
            {
                return false;
            }

            if (character != '.')
            {
                onlyDots = false;
            }
        }

        // Segment z samych kropek to przejscie po katalogach.
        if (onlyDots)
        {
            return false;
        }

        encoded = groupId;
        return true;
    }
}

/// <summary>
/// Dozwolone akcje transportu zgloszone przez gloshnik (schema playbackAction).
/// Kazde pole jest bool?, bo BRAK pola i jawne false to rozne informacje:
/// brak znaczy "gloshnik nie powiedzial", false znaczy "powiedzial, ze nie wolno".
/// UI ma bramkowac przyciski, wiec nie wolno tego zlepiac w jedna wartosc.
/// </summary>
public sealed class SonosPlaybackActions
{
    public SonosPlaybackActions(
        bool? canPlay,
        bool? canSkip,
        bool? canSkipBack,
        bool? canSkipToPrevious,
        bool? canSeek,
        bool? canPause,
        bool? canStop,
        bool? canRepeat,
        bool? canRepeatOne,
        bool? canCrossfade,
        bool? canShuffle)
    {
        CanPlay = canPlay;
        CanSkip = canSkip;
        CanSkipBack = canSkipBack;
        CanSkipToPrevious = canSkipToPrevious;
        CanSeek = canSeek;
        CanPause = canPause;
        CanStop = canStop;
        CanRepeat = canRepeat;
        CanRepeatOne = canRepeatOne;
        CanCrossfade = canCrossfade;
        CanShuffle = canShuffle;
    }

    public bool? CanPlay { get; }

    public bool? CanSkip { get; }

    /// <summary>
    /// canSkipBack - PRZETERMINOWANE od 1.36.0 (deprecated:true w definicji),
    /// zastapione canSkipToPrevious. Zachowane tylko do odczytu starszych
    /// odpowiedzi; nowy kod pyta o <see cref="SkipToPreviousAllowed"/>.
    /// </summary>
    public bool? CanSkipBack { get; }

    public bool? CanSkipToPrevious { get; }

    public bool? CanSeek { get; }

    public bool? CanPause { get; }

    public bool? CanStop { get; }

    public bool? CanRepeat { get; }

    public bool? CanRepeatOne { get; }

    public bool? CanCrossfade { get; }

    public bool? CanShuffle { get; }

    /// <summary>
    /// Czy wolno wywolac skipToPreviousTrack (PRZEJSCIE DO POPRZEDNIEGO utworu).
    /// Nowe pole ma pierwszenstwo; stare canSkipBack sluzy tylko jako zapas dla
    /// gloshnikow przed 1.36.0. Null znaczy "nie wiadomo" i zostaje nullem.
    ///
    /// To NIE jest to samo co polecenie skipBack (powrot na POCZATEK biezacego
    /// utworu) - tego polecenia ten etap nie implementuje.
    /// </summary>
    public bool? SkipToPreviousAllowed => CanSkipToPrevious ?? CanSkipBack;

    public override string ToString() =>
        "Akcje Sonos: play=" + Describe(CanPlay)
        + ", skip=" + Describe(CanSkip)
        + ", poprzedni=" + Describe(SkipToPreviousAllowed)
        + ", seek=" + Describe(CanSeek)
        + ", pauza=" + Describe(CanPause);

    private static string Describe(bool? value) => value switch
    {
        true => "tak",
        false => "nie",
        _ => "brak informacji"
    };
}

/// <summary>Tryby odtwarzania (schema playMode). Kazde pole nullable w definicji.</summary>
public sealed class SonosPlayModes
{
    public SonosPlayModes(bool? repeat, bool? repeatOne, bool? shuffle, bool? crossfade)
    {
        Repeat = repeat;
        RepeatOne = repeatOne;
        Shuffle = shuffle;
        Crossfade = crossfade;
    }

    public bool? Repeat { get; }

    public bool? RepeatOne { get; }

    public bool? Shuffle { get; }

    public bool? Crossfade { get; }
}

/// <summary>
/// Stan odtwarzania grupy (schema playbackStatus). NIEZMIENNY.
/// playbackState jest JEDYNYM polem wymaganym; positionMillis, itemId,
/// queueVersion, playModes i availablePlaybackActions sa nullable.
///
/// DTO NIE WYMYSLA czasu: nie ma tu zegara, nie ma "teraz" i nie ma
/// ekstrapolacji pozycji. Brak pozycji to null, a nie zero.
/// </summary>
public sealed class SonosGroupPlaybackStatus
{
    public SonosGroupPlaybackStatus(
        SonosPlaybackState playbackState,
        bool? isDucking,
        string? queueVersion,
        string? itemId,
        int? positionMillis,
        string? previousItemId,
        int? previousPositionMillis,
        SonosPlayModes? playModes,
        SonosPlaybackActions? availablePlaybackActions)
    {
        PlaybackState = playbackState;
        IsDucking = isDucking;
        QueueVersion = queueVersion;
        ItemId = itemId;
        PositionMillis = positionMillis;
        PreviousItemId = previousItemId;
        PreviousPositionMillis = previousPositionMillis;
        PlayModes = playModes;
        AvailablePlaybackActions = availablePlaybackActions;
    }

    public SonosPlaybackState PlaybackState { get; }

    /// <summary>isDucking - null znaczy brak pola, nie "nie przycisza".</summary>
    public bool? IsDucking { get; }

    /// <summary>queueVersion - maxLength 64, pomijane gdy kolejka bez wersji.</summary>
    public string? QueueVersion { get; }

    /// <summary>itemId biezacego elementu - tylko dla kolejki w chmurze.</summary>
    public string? ItemId { get; }

    /// <summary>
    /// positionMillis - nullable w definicji. NIEZNANA pozycja to null;
    /// zero znaczy "poczatek utworu". Te dwa przypadki nie moga sie zlac.
    /// </summary>
    public int? PositionMillis { get; }

    public string? PreviousItemId { get; }

    public int? PreviousPositionMillis { get; }

    public SonosPlayModes? PlayModes { get; }

    public SonosPlaybackActions? AvailablePlaybackActions { get; }

    public bool HasPosition => PositionMillis.HasValue;

    public override string ToString() =>
        "Stan grupy Sonos: "
        + PlaybackState
        + ", pozycja "
        + (PositionMillis.HasValue
            ? PositionMillis.Value.ToString(CultureInfo.InvariantCulture) + " ms"
            : "nieznana");
}

/// <summary>Usluga muzyczna (schema service): name maxLength 31, id maxLength 10.</summary>
public sealed class SonosMetadataService
{
    public SonosMetadataService(string? name, string? id)
    {
        Name = name;
        Id = id;
    }

    public string? Name { get; }

    public string? Id { get; }
}

/// <summary>
/// Metadane utworu (schema track). Wszystkie pola nullable w definicji.
/// artist i album to OBIEKTY z wymaganym polem name (maxLength 127) - nie napisy,
/// wiec nie wolno czytac ich jako stringow.
/// durationMillis jest OPCJONALNE (minimum 1): radio i strumienie go nie maja.
/// </summary>
public sealed class SonosTrackMetadata
{
    public SonosTrackMetadata(
        string? type,
        string? name,
        string? artistName,
        string? albumName,
        string? albumArtistName,
        SonosMetadataService? service,
        int? durationMillis)
    {
        Type = type;
        Name = name;
        ArtistName = artistName;
        AlbumName = albumName;
        AlbumArtistName = albumArtistName;
        Service = service;
        DurationMillis = durationMillis;
    }

    /// <summary>track.type - maxLength 32.</summary>
    public string? Type { get; }

    /// <summary>track.name - maxLength 100.</summary>
    public string? Name { get; }

    /// <summary>track.artist.name - maxLength 127, wymagane WEWNATRZ obiektu artist.</summary>
    public string? ArtistName { get; }

    /// <summary>track.album.name - maxLength 127, wymagane WEWNATRZ obiektu album.</summary>
    public string? AlbumName { get; }

    /// <summary>track.album.artist.name - wykonawca albumu, gdy podany osobno.</summary>
    public string? AlbumArtistName { get; }

    public SonosMetadataService? Service { get; }

    /// <summary>
    /// track.durationMillis - OPCJONALNE. Null znaczy "dlugosc nieznana"
    /// (typowo radio); nie podstawiamy zera ani wyliczonego czasu.
    /// </summary>
    public int? DurationMillis { get; }

    public bool HasDuration => DurationMillis.HasValue;

    public override string ToString() =>
        "Utwór Sonos: " + (Name ?? "bez nazwy")
        + (ArtistName is null ? string.Empty : " - " + ArtistName);
}

/// <summary>Element kolejki (schema queueItem): id maxLength 128, track opcjonalny.</summary>
public sealed class SonosQueueItem
{
    public SonosQueueItem(string? id, SonosTrackMetadata? track, bool? deleted)
    {
        Id = id;
        Track = track;
        Deleted = deleted;
    }

    public string? Id { get; }

    public SonosTrackMetadata? Track { get; }

    public bool? Deleted { get; }
}

/// <summary>
/// Zrodlo odtwarzania (schema container): stacja, playlista, linia wejsciowa.
/// name maxLength 100, type maxLength 48. Brak containera jest legalny.
/// </summary>
public sealed class SonosMetadataContainer
{
    public SonosMetadataContainer(string? name, string? type, SonosMetadataService? service)
    {
        Name = name;
        Type = type;
        Service = service;
    }

    public string? Name { get; }

    public string? Type { get; }

    public SonosMetadataService? Service { get; }

    /// <summary>container.type = "station" wskazuje radio - wtedy currentItem czesto nie ma.</summary>
    public bool IsStation => string.Equals(Type, "station", StringComparison.Ordinal);
}

/// <summary>
/// Metadane odtwarzania grupy (schema metadataStatus). NIEZMIENNE.
/// WSZYSTKIE pola sa opcjonalne: BRAK currentItem jest LEGALNY dla radia i dla
/// urzadzenia, ktore nic nie zaladowalo. Nie zastepujemy go pustym utworem.
/// </summary>
public sealed class SonosGroupMetadata
{
    public SonosGroupMetadata(
        SonosMetadataContainer? container,
        SonosQueueItem? currentItem,
        SonosQueueItem? nextItem,
        string? currentShowName,
        string? streamInfo)
    {
        Container = container;
        CurrentItem = currentItem;
        NextItem = nextItem;
        CurrentShowName = currentShowName;
        StreamInfo = streamInfo;
    }

    public SonosMetadataContainer? Container { get; }

    /// <summary>currentItem - null jest poprawny (radio, pusty gloshnik).</summary>
    public SonosQueueItem? CurrentItem { get; }

    public SonosQueueItem? NextItem { get; }

    /// <summary>currentShow.name - zwykle tylko dla stacji radiowych.</summary>
    public string? CurrentShowName { get; }

    /// <summary>streamInfo - maxLength 256, nieustrukturyzowany opis strumienia.</summary>
    public string? StreamInfo { get; }

    public SonosTrackMetadata? CurrentTrack => CurrentItem?.Track;

    /// <summary>Czy w ogole cokolwiek wiemy o tym, co leci.</summary>
    public bool IsEmpty =>
        Container is null
        && CurrentItem is null
        && NextItem is null
        && string.IsNullOrEmpty(CurrentShowName)
        && string.IsNullOrEmpty(StreamInfo);

    public override string ToString() =>
        "Metadane grupy Sonos: "
        + (CurrentTrack?.ToString() ?? StreamInfo ?? Container?.Name ?? "brak danych o treści");
}

/// <summary>
/// Glosnosc grupy (schema groupVolume). volume jest WYMAGANE i miesci sie w
/// 0..100; muted i fixed sa opcjonalne - null znaczy brak pola, nie false.
/// fixed:true bedzie bramka UI (nie wolno zmieniac glosnosci).
/// </summary>
public sealed class SonosGroupVolume
{
    public const int MinVolume = 0;
    public const int MaxVolume = 100;

    public SonosGroupVolume(int volume, bool? muted, bool? fixedVolume)
    {
        if (volume < MinVolume || volume > MaxVolume)
        {
            throw new ArgumentOutOfRangeException(nameof(volume));
        }

        Volume = volume;
        Muted = muted;
        FixedVolume = fixedVolume;
    }

    public int Volume { get; }

    public bool? Muted { get; }

    /// <summary>fixed - true znaczy, ze aplikacja NIE MOZE zmieniac glosnosci grupy.</summary>
    public bool? FixedVolume { get; }

    /// <summary>Czy Sonos wprost pozwolil zmieniac glosnosc (null traktujemy jak brak zakazu).</summary>
    public bool Adjustable => FixedVolume != true;

    public override string ToString() =>
        "Głośność grupy Sonos: "
        + Volume.ToString(CultureInfo.InvariantCulture)
        + (Muted == true ? ", wyciszona" : Muted == false ? ", bez wyciszenia" : ", wyciszenie nieznane")
        + (FixedVolume == true ? ", stała" : FixedVolume == false ? ", regulowana" : ", regulacja nieznana");
}

/// <summary>
/// Podstawowe polecenia GRUPY z definicji OpenAPI. Kazde jest POST na adres
/// grupy; zadne nie wymaga sesji odtwarzania (playbackSession) - w
/// szczegolnosci seek i seekRelative dzialaja bez sesji.
/// Na tej liscie NIE MA /stop ani ladowania URI - takich operacji definicja nie
/// potwierdza dla tego etapu.
/// </summary>
public enum SonosGroupCommand
{
    Play,
    Pause,
    TogglePlayPause,
    SkipToNextTrack,
    SkipToPreviousTrack,
    Seek,
    SeekRelative,
    SetVolume,
    SetMute,
    SetRelativeVolume,

    /// <summary>
    /// POST /groups/{groupId}/favorites - zaladowanie ULUBIONEGO do wspolnej
    /// kolejki grupy (operacja Favorites-LoadFavorite-GroupId). Dopisane na
    /// KONCU listy bez zmiany numeracji wczesniejszych pozycji, bo wartosci
    /// tego enuma wystepuja w istniejacych atrapach i asercjach.
    ///
    /// To polecenie NIE nalezy do namespace playback, dlatego jego sciezka nie
    /// ma przedrostka "playback/".
    /// </summary>
    LoadFavorite
}

/// <summary>Sciezki i charakter polecen - jedno zrodlo prawdy dla transportu i wyniku.</summary>
public static class SonosGroupCommands
{
    /// <summary>Wzgledna sciezka polecenia pod adresem grupy, bez segmentu groupId.</summary>
    public static string PathSuffix(SonosGroupCommand command) => command switch
    {
        SonosGroupCommand.Play => "playback/play",
        SonosGroupCommand.Pause => "playback/pause",
        SonosGroupCommand.TogglePlayPause => "playback/togglePlayPause",
        SonosGroupCommand.SkipToNextTrack => "playback/skipToNextTrack",
        SonosGroupCommand.SkipToPreviousTrack => "playback/skipToPreviousTrack",
        SonosGroupCommand.Seek => "playback/seek",
        SonosGroupCommand.SeekRelative => "playback/seekRelative",
        SonosGroupCommand.SetVolume => "groupVolume",
        SonosGroupCommand.SetMute => "groupVolume/mute",
        SonosGroupCommand.SetRelativeVolume => "groupVolume/relative",
        SonosGroupCommand.LoadFavorite => "favorites",
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };

    /// <summary>
    /// Polecenie PRZELACZAJACE albo WZGLEDNE: jego skutek zalezy od stanu w
    /// chwili wykonania, wiec po utracie odpowiedzi nie wiadomo, czy zadzialalo.
    /// Takiego polecenia NIE WOLNO ponawiac automatycznie.
    /// </summary>
    public static bool IsStateDependent(SonosGroupCommand command) => command switch
    {
        SonosGroupCommand.TogglePlayPause => true,
        SonosGroupCommand.SkipToNextTrack => true,
        SonosGroupCommand.SkipToPreviousTrack => true,
        SonosGroupCommand.SeekRelative => true,
        SonosGroupCommand.SetRelativeVolume => true,
        // Zaladowanie ulubionego dopisuje albo zastepuje WSPOLNA kolejke, ktorej
        // zawartosci transport nie zna. Skutek zalezy wiec od stanu, polecenie
        // nie jest idempotentne i nie wolno go ponawiac automatycznie.
        SonosGroupCommand.LoadFavorite => true,
        _ => false
    };
}

/// <summary>
/// Wynik POLECENIA grupy. Rozdziela trzy rozne rzeczy, ktore mylenie ze soba
/// jest zwyklym zrodlem falszywych komunikatow:
///  * ZADANIE NIE POSZLO (zla konfiguracja albo zly argument - lokalnie),
///  * SONOS PRZYJAL polecenie (HTTP 200) - to NIE dowod skutku,
///  * SKUTEK NIEZNANY (zerwane polaczenie albo deadline przy poleceniu
///    przelaczajacym lub wzglednym).
///
/// Weryfikacje skutku wykonuje PÓZNIEJ warstwa koordynatora przez JAWNY odczyt
/// stanu. Transport niczego nie ponawia i nie udaje potwierdzenia.
/// </summary>
public sealed class SonosGroupCommandOutcome
{
    private SonosGroupCommandOutcome(SonosGroupCommand command, SonosControlApiStatus status, bool sent)
    {
        Command = command;
        Status = status;
        Sent = sent;
    }

    public SonosGroupCommand Command { get; }

    public SonosControlApiStatus Status { get; }

    /// <summary>Czy zadanie HTTP w ogole opuscilo aplikacje.</summary>
    public bool Sent { get; }

    /// <summary>
    /// Sonos PRZYJAL polecenie (HTTP 200 wg definicji: pusty obiekt "ok").
    /// To przyjecie zlecenia, NIE potwierdzenie, ze muzyka zmienila stan.
    /// </summary>
    public bool Accepted => Status == SonosControlApiStatus.Success;

    /// <summary>
    /// Zawsze true: samo przyjecie polecenia nie dowodzi skutku. Nazwane wprost,
    /// zeby zaden wolajacy nie ogloszil uzytkownikowi wykonania po HTTP 200.
    /// </summary>
    public bool EffectConfirmed => false;

    /// <summary>
    /// Skutek NIEROZSTRZYGNIETY: polecenie przelaczajace albo wzgledne poszlo w
    /// siec, a odpowiedz nie wrocila (deadline, zerwanie, anulowanie). Mogl sie
    /// wykonac raz, moglo go nie byc. Bez odczytu stanu nie wiadomo.
    /// </summary>
    public bool EffectAmbiguous =>
        Sent
        && SonosGroupCommands.IsStateDependent(Command)
        && Status is SonosControlApiStatus.Unreachable or SonosControlApiStatus.Canceled;

    /// <summary>Czy warto, by koordynator zrobil jawny odczyt stanu po tym poleceniu.</summary>
    public bool StateReadRecommended => Accepted || EffectAmbiguous;

    /// <summary>
    /// Tekst dla uzytkownika opisujacy POLECENIE, nie odczyt. Wprost z tabeli
    /// polecen: slownik odczytu klamalby tu o tym, co sie stalo (po POST 200
    /// ogloszony "zakonczony odczyt", po 400 "zapytanie o urzadzenia", a przy
    /// lokalnym odrzuceniu argumentu obwiniony identyfikator domu).
    /// </summary>
    public string Message => SonosGroupCommandMessages.Describe(Status, Sent, EffectAmbiguous);

    internal static SonosGroupCommandOutcome FromStatus(
        SonosGroupCommand command, SonosControlApiStatus status, bool sent) =>
        new(command, status, sent);

    public override string ToString() =>
        "Polecenie Sonos "
        + Command
        + ": "
        + Status
        + (Accepted ? ", przyjęte (skutek niepotwierdzony)" : string.Empty)
        + (EffectAmbiguous ? ", skutek nierozstrzygnięty" : string.Empty)
        + ", "
        + Message;
}

/// <summary>
/// Stale, bezpieczne komunikaty WYNIKU POLECENIA grupy. Osobne od slownika
/// ODCZYTU, bo opisuja inne zdarzenie: tu nic nie zostalo odczytane, a HTTP 200
/// jest tylko PRZYJECIEM zlecenia. Zadny tekst nie zawiera tokenu, klucza API
/// ani fragmentu ciala odpowiedzi.
///
/// Granice slow sa scisle:
///  * "przyjete" nigdy nie znaczy "wykonane" - stad jawne "wykonanie niepotwierdzone",
///  * "nie zostalo wyslane" opisuje przypadki bez zadnego zadania HTTP,
///  * "skutek nieznany" opisuje polecenie przelaczajace/wzgledne, dla ktorego
///    odpowiedz nie wrocila; nie twierdzimy ani ze dotarlo, ani ze przepadlo.
/// Sent mowi tylko, ze zadanie zostalo przekazane do HttpClient, wiec zaden
/// komunikat nie oglasza dostarczenia do Sonosa.
/// </summary>
public static class SonosGroupCommandMessages
{
    /// <summary>
    /// Skutek nierozstrzygniety - ma pierwszenstwo nad tekstem samego statusu.
    /// Nie oglasza wyslania: Sent to proba przekazania do HttpClient, a nie dowod,
    /// ze zadanie opuscilo maszyne.
    /// </summary>
    public const string EffectAmbiguousText =
        "Nie otrzymano odpowiedzi na polecenie Sonos: skutek nieznany. Sprawdź stan odtwarzania.";

    /// <summary>
    /// Anulowanie PO przekazaniu zadania: anulowano oczekiwanie wolajacego, a nie
    /// dzialanie Sonosa - polecenie moglo sie wykonac, tylko odpowiedzi nie czekamy.
    /// </summary>
    public const string CanceledAfterSendText =
        "Anulowano oczekiwanie na odpowiedź Sonos; wynik polecenia niepotwierdzony.";

    private static readonly IReadOnlyDictionary<SonosControlApiStatus, string> Texts =
        new Dictionary<SonosControlApiStatus, string>
        {
            [SonosControlApiStatus.Success] =
                "Sonos przyjął polecenie; wykonanie niepotwierdzone.",
            [SonosControlApiStatus.InvalidConfiguration] =
                "Polecenie Sonos nie zostało wysłane: brak klucza integracji albo niepoprawny argument polecenia.",
            [SonosControlApiStatus.Unauthorized] =
                "Sonos nie przyjął dostępu do konta, więc polecenie nie zostało wykonane. Odśwież dostęp albo zaloguj się ponownie.",
            [SonosControlApiStatus.Forbidden] =
                "Konto Sonos nie ma uprawnień do tego polecenia.",
            [SonosControlApiStatus.NotFound] =
                "Sonos nie znalazł wskazanej grupy; polecenie nie zostało wykonane.",
            [SonosControlApiStatus.RequestRejected] =
                "Sonos odrzucił polecenie jako niepoprawne.",
            [SonosControlApiStatus.RateLimited] =
                "Sonos chwilowo ogranicza liczbę żądań; polecenie nie zostało przyjęte. Spróbuj później.",
            [SonosControlApiStatus.CommandFailed] =
                "Sonos przyjął polecenie, ale zgłosił, że go nie wykonał.",
            // 5xx, zerwane polaczenie i odrzucona odpowiedz NIE dowodza, ze Sonos
            // polecenia nie przyjal: odpowiedz mogla zginac PO jego wykonaniu.
            [SonosControlApiStatus.ServiceError] =
                "Usługa Sonos zgłosiła błąd; wynik polecenia niepotwierdzony.",
            [SonosControlApiStatus.Unreachable] =
                "Nie otrzymano potwierdzenia przyjęcia polecenia Sonos; wykonanie niepotwierdzone.",
            [SonosControlApiStatus.Canceled] =
                "Polecenie anulowano.",
            [SonosControlApiStatus.InvalidResponse] =
                "Odpowiedź Sonos na polecenie była niezgodna z oczekiwaną; przyjęcie niepotwierdzone.",
            [SonosControlApiStatus.RedirectRefused] =
                "Odrzucono nieoczekiwaną odpowiedź na polecenie Sonos; wynik niepotwierdzony."
        };

    /// <summary>
    /// Tekst wyniku polecenia. <paramref name="sent"/> rozdziela przypadki, w
    /// ktorych zadania NIE BYLO wcale (anulowanie przed wyslaniem, lokalne
    /// odrzucenie argumentu) od tych, w ktorych poszlo i zawiodlo pozniej -
    /// dwa rozne zdarzenia nie moga mowic tym samym zdaniem.
    /// </summary>
    public static string Describe(SonosControlApiStatus status, bool sent, bool effectAmbiguous)
    {
        if (effectAmbiguous)
        {
            return EffectAmbiguousText;
        }

        // Anulowanie PO przekazaniu zadania dotyczy naszego oczekiwania, nie
        // dzialania Sonosa; anulowanie PRZED wyslaniem to zupelnie inne zdarzenie
        // i zostaje przy tekscie "nie zostalo wyslane".
        if (sent && status == SonosControlApiStatus.Canceled)
        {
            return CanceledAfterSendText;
        }

        var text = Texts.TryGetValue(status, out var found)
            ? found
            : Texts[SonosControlApiStatus.InvalidResponse];
        return sent || status == SonosControlApiStatus.Success
            ? text
            : NotSent(status, text);
    }

    /// <summary>
    /// Zadnego zadania nie bylo: mowimy to wprost, zamiast sugerowac, ze Sonos
    /// cokolwiek zobaczyl. Tekst InvalidConfiguration juz to zawiera.
    /// </summary>
    private static string NotSent(SonosControlApiStatus status, string text) =>
        status == SonosControlApiStatus.InvalidConfiguration
            ? text
            : "Polecenie Sonos nie zostało wysłane. " + text;
}

/// <summary>Wynik GET /groups/{groupId}/playback.</summary>
public sealed class SonosGroupPlaybackOutcome
{
    private SonosGroupPlaybackOutcome(SonosControlApiStatus status, SonosGroupPlaybackStatus? playback)
    {
        Status = status;
        Playback = playback;
    }

    public SonosControlApiStatus Status { get; }

    public SonosGroupPlaybackStatus? Playback { get; }

    public bool Succeeded => Status == SonosControlApiStatus.Success && Playback is not null;

    public string Message => SonosControlApiMessages.Describe(Status);

    internal static SonosGroupPlaybackOutcome Ok(SonosGroupPlaybackStatus playback) =>
        new(SonosControlApiStatus.Success, playback);

    internal static SonosGroupPlaybackOutcome Failure(SonosControlApiStatus status) =>
        new(status == SonosControlApiStatus.Success ? SonosControlApiStatus.InvalidResponse : status, null);

    public override string ToString() =>
        "Odczyt stanu grupy Sonos: " + Status + ", "
        + (Playback is null ? "brak danych" : Playback.ToString()) + ", " + Message;
}

/// <summary>Wynik GET /groups/{groupId}/playbackMetadata.</summary>
public sealed class SonosGroupMetadataOutcome
{
    private SonosGroupMetadataOutcome(SonosControlApiStatus status, SonosGroupMetadata? metadata)
    {
        Status = status;
        Metadata = metadata;
    }

    public SonosControlApiStatus Status { get; }

    public SonosGroupMetadata? Metadata { get; }

    public bool Succeeded => Status == SonosControlApiStatus.Success && Metadata is not null;

    public string Message => SonosControlApiMessages.Describe(Status);

    internal static SonosGroupMetadataOutcome Ok(SonosGroupMetadata metadata) =>
        new(SonosControlApiStatus.Success, metadata);

    internal static SonosGroupMetadataOutcome Failure(SonosControlApiStatus status) =>
        new(status == SonosControlApiStatus.Success ? SonosControlApiStatus.InvalidResponse : status, null);

    public override string ToString() =>
        "Odczyt metadanych grupy Sonos: " + Status + ", "
        + (Metadata is null ? "brak danych" : Metadata.ToString()) + ", " + Message;
}

/// <summary>Wynik GET /groups/{groupId}/groupVolume.</summary>
public sealed class SonosGroupVolumeOutcome
{
    private SonosGroupVolumeOutcome(SonosControlApiStatus status, SonosGroupVolume? volume)
    {
        Status = status;
        Volume = volume;
    }

    public SonosControlApiStatus Status { get; }

    public SonosGroupVolume? Volume { get; }

    public bool Succeeded => Status == SonosControlApiStatus.Success && Volume is not null;

    public string Message => SonosControlApiMessages.Describe(Status);

    internal static SonosGroupVolumeOutcome Ok(SonosGroupVolume volume) =>
        new(SonosControlApiStatus.Success, volume);

    internal static SonosGroupVolumeOutcome Failure(SonosControlApiStatus status) =>
        new(status == SonosControlApiStatus.Success ? SonosControlApiStatus.InvalidResponse : status, null);

    public override string ToString() =>
        "Odczyt głośności grupy Sonos: " + Status + ", "
        + (Volume is null ? "brak danych" : Volume.ToString()) + ", " + Message;
}
