"""Nawigacja AMC-wx-Lite: dwie sesje, widok listy i widok odtwarzacza.

Cala logika przejsc jest TUTAJ, bez wx. Okno tylko odwzorowuje ten stan.
Dzieki temu zachowanie Enter/Escape/F6/Ctrl+cyfra testujemy w WSL.

Zgodnosc skrotow z pelnym AMC zostala odczytana ze ZRODEL (KeyboardProfile.cs,
CommandIds.cs), nie wymyslona - patrz mapa w shortcuts.py.
"""

from __future__ import annotations

import random

from dataclasses import dataclass, field, replace
from enum import Enum

from . import i18n
from .list_model import ListModel, Row


class View(Enum):
    """Ktory widok jest na wierzchu. Odpowiada _playerViewActive w pelnym AMC."""

    LIST = "list"
    PLAYER = "player"


class SessionId(Enum):
    FILES = "files"
    RADIO = "radio"
    PODCASTS = "podcasts"
    TIDAL = "tidal"
    WIIM = "wiim"
    SONOS = "sonos"
    SPOTIFY = "spotify"
    FRONTIER_SMART = "frontierSmart"
    RADIOARCHIWISTA = "radioarchiwista"


class LibraryView(Enum):
    """Nazwany widok lokalnej Biblioteki, OBOK przegladania folderow.

    Nazwy widokow sa te same, co w zwyklym AMC:
    ``MainWindow.xaml.cs:65`` ``AllLocalFilesViewName = "Wszystkie pliki"``,
    ``MainWindow.xaml:426-427`` Ulubione i Playlisty,
    ``MainWindow.xaml.cs:67`` ``PlaylistContentsViewPrefix = "Playlista:"``.
    """

    ALL_FILES = "allFiles"
    FAVORITES = "favorites"
    PLAYLISTS = "playlists"
    PLAYLIST_CONTENTS = "playlistContents"
    #: Widoki AKTYWNOSCI. Zrodlo danych: ``library_activity.py`` (tylko odczyt
    #: utrwalonego profilu), a nie stan grajacego silnika.
    HISTORY = "history"
    SAVED_QUEUE = "savedQueue"
    #: ZYWA kolejka GRAJACEGO hosta, czytana z ``queue.status``. Osobna od
    #: ``SAVED_QUEUE``, bo to dwa rozne zjawiska: tam utrwalony zapis profilu
    #: (dostepny takze bez odtwarzania), tutaj stan silnika, ktory zmienia sie
    #: po kazdym przejsciu, Nastepnym i Poprzednim. Jeden widok na oba
    #: klamalby w jednym z dwoch stanow.
    LIVE_QUEUE = "liveQueue"
    #: Zakladki JEDNEGO zaznaczonego elementu (``BookmarkIndex.GetForItem``).
    #: NIE jest to zbiorczy widok ``ViewBookmarks``/``GetForDisplay``.
    ITEM_BOOKMARKS = "itemBookmarks"
    #: ZBIORCZY widok wszystkich zakladek (``BookmarkIndex.GetForDisplay``,
    #: ``BookmarkIndex.cs:19-28``) -- ten, ktory w pelnym AMC siedzi pod
    #: ``CommandIds.ViewBookmarks`` i Ctrl+B. Szerszy zbior niz
    #: ``ITEM_BOOKMARKS``, wiec osobna wartosc, a nie przelacznik.
    ALL_BOOKMARKS = "allBookmarks"
    #: Biezacy stan nagrywania radia z hosta. To nie jest utrwalona historia
    #: ani lista plikow na dysku, dlatego ma osobna tozsamosc widoku.
    ACTIVE_RADIO_RECORDINGS = "activeRadioRecordings"
    #: Utrwalona historia nagrywania radia: udane, zatrzymane, przerwane i
    #: nieudane proby. Widok mieszka w sesji Pliki lokalne jak w pelnym AMC.
    RECORDED_RADIO_FILES = "recordedRadioFiles"
    DOWNLOADED_FILES = "downloadedFiles"
    #: Plany nagrywania z profilu AMC. Python tylko je pokazuje; nie uruchamia
    #: drugiego harmonogramu obok wlasciciela C#.
    RADIO_RECORDING_SCHEDULES = "radioRecordingSchedules"
    #: Glowna lista podcastow/kanalow oraz zawartosc jednego zrodla.
    #: Osobne wartosci pozwalaja Backspace i filtrom rozpoznac poziom bez
    #: ujawniania technicznego Id podcastu w nazwie dostepnej.
    PODCAST_LIBRARY = "podcastLibrary"
    PODCAST_EPISODES = "podcastEpisodes"
    #: Chwilowa lista trwających i zaplanowanych transmisji jednego kanału
    #: YouTube. Oddzielna od zwykłych filmów kanału, bo ma inny cykl życia.
    YOUTUBE_BROADCASTS = "youtubeBroadcasts"
    #: Globalny, chwilowy widok transmisji wyłącznie z kanałów jawnie
    #: wybranych przez użytkownika. Nie skanuje całej biblioteki ani nie
    #: zmienia subskrypcji Google.
    YOUTUBE_BROADCASTS_AGGREGATE = "youtubeBroadcastsAggregate"
    #: Wspólny widok Ctrl+U: ulubione źródła oraz ulubione odcinki.
    PODCAST_FAVORITES = "podcastFavorites"
    PODCAST_HISTORY = "podcastHistory"
    PODCAST_QUEUE = "podcastQueue"
    PODCAST_PLAYLISTS = "podcastPlaylists"
    PODCAST_PLAYLIST_CONTENTS = "podcastPlaylistContents"
    #: Zbiorcze widoki z glownego AMC. ``PODCAST_INBOX`` jest globalnym,
    #: chwilowym podgladem pod Ctrl+I.
    PODCAST_INBOX = "podcastInbox"
    PODCAST_DOWNLOADS = "podcastDownloads"
    #: Pierwszy etap sesji TIDAL: trwale zapisany cache kolekcji z profilu
    #: AMC. Osobne wartosci zapobiegaja pomyleniu ich z lokalna Biblioteka.
    TIDAL_LIBRARY = "tidalLibrary"
    TIDAL_FAVORITES = "tidalFavorites"
    TIDAL_PLAYLISTS = "tidalPlaylists"
    TIDAL_QUEUE = "tidalQueue"
    #: Zawartosc albumu, playlisty albo jednej kategorii wykonawcy. To nadal
    #: sesja TIDAL, lecz Backspace ma tu odtworzyc dokladna poprzednia liste.
    TIDAL_CONTAINER = "tidalContainer"
    #: Jedna sesja Spotify: korzeń zapisanej Biblioteki i dowolny jawnie
    #: otwarty kontener katalogu. Techniczny typ kontenera pozostaje w DTO.
    SPOTIFY_LIBRARY = "spotifyLibrary"
    SPOTIFY_CONTAINER = "spotifyContainer"
    #: Własna historia potwierdzonych odtworzeń AMC Python. Nie jest to
    #: kompletna historia konta Spotify ani kolejny kontener katalogu.
    SPOTIFY_HISTORY = "spotifyHistory"
    #: Prywatna, osobna historia sesji WiiM albo Sonos. Nie jest to lokalna
    #: tabela plików i nie może przełączać aktywnej sesji.
    SERVICE_HISTORY = "serviceHistory"
    #: Uczciwy korzeń źródeł WiiM. Lokalny interfejs nie udaje katalogu
    #: serwisów urządzenia: pokazuje wyłącznie sprzętowe presety i strumienie
    #: zapisane przez użytkownika w profilu AMC.
    WIIM_SOURCE_LIBRARY = "wiimSourceLibrary"
    #: Własne strumienie zapisane w profilu WiiM. To podwidok wybranego
    #: urządzenia, a nie lokalna Biblioteka plików.
    WIIM_NETWORK_STREAMS = "wiimNetworkStreams"
    #: Biblioteka treści Sonosa. Grupy są celem sterowania pod Ctrl+F5,
    #: a nie materiałem i dlatego mają osobny widok poza tymi wartościami.
    SONOS_LIBRARY = "sonosLibrary"
    SONOS_FAVORITES = "sonosFavorites"
    SONOS_PLAYLISTS = "sonosPlaylists"
    SONOS_OWN_STREAMS = "sonosOwnStreams"
    #: Poziomy katalogu nagrań Radio Archiwisty. Osobne wartości pozwalają
    #: Backspace i filtrom rozpoznać poziom bez wstawiania identyfikatora
    #: stacji do nazwy dostępnej.
    RADIOARCHIWISTA_STATIONS = "radioarchiwistaStations"
    RADIOARCHIWISTA_YEARS = "radioarchiwistaYears"
    RADIOARCHIWISTA_MONTHS = "radioarchiwistaMonths"
    RADIOARCHIWISTA_DAYS = "radioarchiwistaDays"
    RADIOARCHIWISTA_HOURS = "radioarchiwistaHours"
    RADIOARCHIWISTA_FAVORITES = "radioarchiwistaFavorites"
    RADIOARCHIWISTA_QUEUE = "radioarchiwistaQueue"


#: Wartosci ``_state.LocalMedia.LibraryView`` (``AppSettings.cs:1236``, domyslnie
#: "Foldery"); nazwy widokow z ``MainWindow.xaml.cs:64-66``. To NIE sa wartosci
#: ``LibraryView``: zapis profilu zna tylko te trzy nazwy i Ctrl+L
#: (``MainWindow.xaml.cs:701-705``) podstawia je za nazwe "Biblioteka".
LIBRARY_VIEW_FOLDERS = "Foldery"
LIBRARY_VIEW_ALL_FILES = "Wszystkie pliki"
#: Przeniesione docele Ctrl+L. "Kolejność własna" (cs:66) NIE jest przeniesiona,
#: wiec jej nie udajemy -- brak wpisu znaczy odmowe z nazwa widoku.
LIBRARY_RETURN_TARGETS: dict[str, "LibraryView | None"] = {
    LIBRARY_VIEW_FOLDERS: None,
    LIBRARY_VIEW_ALL_FILES: LibraryView.ALL_FILES,
}


@dataclass(slots=True)
class OpenLibraryView:
    """Zlecenie: wczytaj dane nazwanego widoku Biblioteki.

    Okno samo nic nie wymysla -- idzie po dane do ``library_views`` i wraca
    przez ``apply_library_view``. ``playlist_id`` wypelniamy wylacznie dla
    zawartosci playlisty.
    """

    # None oznacza glowna Biblioteke stacji, tylko dla target_session_id=RADIO.
    view: LibraryView | None
    playlist_id: str | None = None
    preferred_id: str | None = None
    #: RZECZYWISTE Id elementu dla ``ITEM_BOOKMARKS``. ``bookmark_rows``
    #: inaczej zawolac sie nie da -- w C# tez przyjmuje Id, nie indeks listy.
    item_id: str | None = None
    #: Kontekst AKTUALNIE ODTWARZANEGO materialu dla ``ALL_BOOKMARKS``, tak
    #: jak ``_sessions.Current.Id`` i ``_sessions.Current.CurrentItem.Id``
    #: (``MainWindow.xaml.cs:12530-12532``). To NIE zaznaczony wiersz. Puste
    #: napisy sa legalne: sesja moze nic nie odtwarzac, a wtedy zaden wpis nie
    #: jest "biezacy" i nikt nie wedruje na gore listy.
    current_session_id: str = ""
    current_item_id: str = ""
    # Cel odczytu, nie sesja aktualnie ogladana po odpowiedzi asynchronicznej.
    target_session_id: SessionId = SessionId.FILES


@dataclass(slots=True)
class SessionState:
    """Stan JEDNEJ sesji. Kazda sesja pamieta swoja liste i swoj widok,
    zeby przelaczenie Ctrl+1/Ctrl+2 wracalo dokladnie tam, gdzie bylismy."""

    session_id: SessionId
    model: ListModel = field(default_factory=ListModel)
    view: View = View.LIST
    folder_path: str | None = None
    # Stos powrotu: sciezka rodzica -> ID, na ktorym mamy stanac po Backspace.
    breadcrumb: list[tuple[str, str]] = field(default_factory=list)
    now_playing_id: str | None = None
    now_playing_title: str = ""
    # ID wybrany w chwili wejscia do odtwarzacza. Escape wraca DOKLADNIE tu.
    list_anchor_id: str | None = None
    #: Pierwotny fokus listy przy wejsciu do odtwarzacza. ``list_anchor_id``
    #: moze potem sledzic Page Up/Page Down; ta wartosc pozwala opcji
    #: ``FollowPlaybackOnPlayerExit = false`` wrocic do miejsca startu.
    player_entry_anchor_id: str | None = None
    #: Ktory nazwany widok Biblioteki jest na liscie. ``None`` = przegladamy
    #: foldery (dotychczasowe zachowanie, nietkniete).
    library_view: "LibraryView | None" = None
    #: Id playlisty, ktorej zawartosc ogladamy.
    library_playlist_id: str | None = None
    #: Wiersz, na ktory wraca Backspace z zawartosci playlisty.
    library_return_id: str | None = None
    #: RZECZYWISTE Id pliku, ktorego zakladki ogladamy. Osobne od
    #: ``library_return_id``, bo powrot z zakladek celuje w TEN plik.
    library_item_id: str | None = None
    #: ``Row.item_id`` zakladki -> (sciezka pliku, pozycja w sekundach, tytul).
    #: Bez tej mapy wiersz zakladki nie ma czym odtworzyc: jego ``item_id`` to
    #: ``bookmark:<id>``, ktore NIE jest ani plikiem, ani sciezka.
    bookmark_targets: dict[str, tuple[str, float, str]] = field(default_factory=dict)
    #: ``Row.item_id`` zakladki -> kontekst (sesja, Id materialu, czy lokalna).
    #: Obowiazuje TYLKO w widoku zbiorczym; bez niej nie da sie odmowic obcej
    #: sesji inaczej niz po braku sciezki, a to dwie rozne rzeczy.
    bookmark_contexts: dict[str, object] = field(default_factory=dict)
    #: ``Row.item_id`` -> (``is_in_queue``, ``is_play_next``) w widoku kolejki.
    #: Czlonkostwo policzyl ``library_activity`` z ZAPISU profilu; okno go nie
    #: przelicza i nie wymusza. Pusta mapa = nie ogladamy kolejki.
    queue_flags: dict[str, tuple[bool, bool]] = field(default_factory=dict)
    #: PROFILOWE Id materialu, ktory sesja faktycznie odtwarza (odpowiednik
    #: ``_sessions.Current.CurrentItem.Id``). Osobne od ``now_playing_id``,
    #: bo tamto bywa ``file:<path>`` z hosta, a ``file:``/``bookmark:`` NIE sa
    #: zamiennikami Id profilowego. Puste = sesja nie ma biezacego materialu.
    current_material_id: str = ""
    #: Kandydat na ``current_material_id``, czekajacy na POTWIERDZENIE startu.
    #: Samo zaznaczenie i nieudany start nie moga zmienic kontekstu.
    pending_material_id: str = ""
    #: Folder Biblioteki, z ktorego weszlismy w nazwany widok. ``folder_path``
    #: tego nie udzwignie: nazwany widok musi je czyscic (Backspace), a Ctrl+L
    #: ma wrocic DOKLADNIE tam, gdzie uzytkownik byl.
    library_folder_path: str | None = None
    # Ostatni faktycznie otwarty tryb w tym oknie; wspolny profil jest read-only.
    library_return_view: str | None = None
    #: Nazwa widoku -> ostatnio na nim zaznaczony wiersz. Odpowiednik
    #: ``SessionNavigationState.SelectedItemIds`` (``MainWindow.xaml.cs:18095``).
    view_selected_ids: dict[str, str] = field(default_factory=dict)
    #: Migawka listy, z ktorej uruchomiono biezacy material. Page Up/Page Down
    #: w pelnym AMC chodza po tej liscie zrodlowej, a NIE zawsze po trwalej
    #: kolejce. Trzymamy wiersze osobno, bo po uruchomieniu uzytkownik moze
    #: otworzyc inny widok, a nastepny element nadal ma pochodzic ze zrodla.
    playback_source_rows: tuple[Row, ...] = ()
    #: Nielosowa migawka tego samego zrodla. Po wylaczeniu losowania wracamy
    #: do porzadku katalogu, nie do przypadkowej kolejnosci z pamieci.
    unshuffled_playback_source_rows: tuple[Row, ...] = ()
    #: Stan odtwarzania losowego nalezy do sesji, a nie do aktualnego widoku.
    #: W GUI jest zapisywany w prywatnym stanie AMC Test.
    shuffle_enabled: bool = False
    #: ``True`` tylko wtedy, gdy odtwarzanie rzeczywiscie prowadzi kolejka
    #: hosta (Zapisana kolejka albo Zywa kolejka). Zwykly plik, zakladka i
    #: stacja nie moga pytac ``queue.previous``/``queue.next``.
    playback_uses_queue: bool = False
    #: Biezacy naglowek i stos nawigacji katalogu TIDAL sa stanem jednej
    #: sesji. Przelaczenie Ctrl+1/Ctrl+4 nie moze wyrzucac uzytkownika z
    #: albumu ani zmieniac zaznaczenia.
    tidal_heading: str = "Biblioteka TIDAL"
    tidal_context: "OpenTidalContainer | None" = None
    tidal_history: list["TidalNavigationSnapshot"] = field(default_factory=list)
    #: Położenie w katalogu Radio Archiwisty: stacja, rok, miesiąc, dzień.
    #: Sama hierarchia nie mieści się w ``folder_path``; te pola mówią,
    #: który shard katalogu ma być widoczny i dokąd wraca Backspace.
    radioarchiwista_slug: str = ""
    radioarchiwista_year: int | None = None
    radioarchiwista_month: int | None = None
    radioarchiwista_day: int | None = None
    #: Stos powrotu: (widok, slug, rok, miesiąc, dzień, Id wiersza).
    radioarchiwista_history: list[tuple[object, str, int | None, int | None, int | None, str | None]] = field(default_factory=list)
    radioarchiwista_queue_active: bool = False
    #: Stan jednej sesji Spotify. Jest niezależny od TIDAL-a mimo wspólnego
    #: kontraktu DTO: przełączenie sesji nie może zgubić listy ani fokusu.
    spotify_heading: str = "Biblioteka Spotify"
    spotify_context: "OpenSpotifyContainer | OpenSpotifyCollectionView | None" = None
    spotify_history: list["SpotifyNavigationSnapshot"] = field(default_factory=list)
    #: Korzeń sesji WiiM i miejsce powrotu ze Strumieni sieciowych. Adresy
    #: urządzeń ani strumieni nie należą do tych wierszy.
    wiim_device_rows: tuple[Row, ...] = ()
    wiim_device_return_id: str | None = None
    wiim_streams_return_to_sources: bool = False
    #: Ostatnia pełna topologia grup Sonos. Zostaje poza aktualnym modelem
    #: listy, gdy użytkownik przegląda Bibliotekę, aby preset stałego zestawu
    #: można było rozwiązać po logicznych playerIds, a nie po kruchej nazwie.
    sonos_target_rows: tuple[Row, ...] = ()


@dataclass(frozen=True, slots=True)
class TransientNavigationSnapshot:
    """Miejsce powrotu ze wspolnego podgladu Alt+R/Alt+Shift+R.

    Stan odtwarzania nie jest migawka: muzyka moze isc dalej i zdarzenia hosta
    nadal aktualizuja ``now_playing``. Zachowujemy tylko nawigacje, liste,
    filtr powiazany z widokiem i fokus logiczny.
    """

    session_id: SessionId
    view: View
    rows: tuple[Row, ...]
    selected_id: str | None
    list_anchor_id: str | None
    player_entry_anchor_id: str | None
    folder_path: str | None
    breadcrumb: tuple[tuple[str, str], ...]
    library_view: LibraryView | None
    library_playlist_id: str | None
    library_return_id: str | None
    library_item_id: str | None
    bookmark_targets: dict[str, tuple[str, float, str]]
    bookmark_contexts: dict[str, object]
    queue_flags: dict[str, tuple[bool, bool]]
    library_folder_path: str | None
    library_return_view: str | None
    view_selected_ids: dict[str, str]
    tidal_heading: str
    tidal_context: "OpenTidalContainer | None"
    tidal_history: tuple["TidalNavigationSnapshot", ...]
    spotify_heading: str
    spotify_context: "OpenSpotifyContainer | OpenSpotifyCollectionView | None"
    spotify_history: tuple["SpotifyNavigationSnapshot", ...]
    wiim_device_rows: tuple[Row, ...]
    wiim_device_return_id: str | None
    wiim_streams_return_to_sources: bool
    sonos_target_rows: tuple[Row, ...]


@dataclass(slots=True)
class OpenFolder:
    """Zlecenie otwarcia poziomu listy.

    ``path=None`` NIE znaczy "brak sciezki" -- to KORZEN Biblioteki AMC,
    ktory nie jest katalogiem na dysku, a lista ``folder_sources`` plus
    rekordy nie nalezace do zadnego zrodla.
    """

    path: str | None
    preferred_id: str | None = None


@dataclass(slots=True)
class OpenPodcastView:
    """Load the podcast library or episodes of one selected source."""

    subscription_id: str | None = None
    subscription_title: str = ""
    preferred_id: str | None = None
    load_more: bool = False


@dataclass(slots=True)
class OpenRadioarchiwistaStations:
    """Lista stacji publicznego katalogu Radio Archiwisty."""

    preferred_id: str | None = None
    refresh: bool = False


@dataclass(slots=True)
class OpenRadioarchiwistaLatest:
    """Nagrania ostatniej opublikowanej godziny, ze wszystkich stacji."""

    refresh: bool = False


@dataclass(slots=True)
class OpenRadioarchiwistaLevel:
    """Jeden poziom katalogu nagrań: lata, miesiące, dni albo godziny."""

    view: "LibraryView"
    slug: str
    year: int | None = None
    month: int | None = None
    day: int | None = None
    load_more: bool = False
    refresh: bool = False
    preferred_id: str | None = None


@dataclass(slots=True)
class OpenYouTubeBroadcasts:
    """Load public live/upcoming broadcasts for one saved YouTube channel."""

    subscription_id: str
    subscription_title: str
    preferred_id: str | None = None
    force_refresh: bool = False


@dataclass(slots=True)
class OpenTidalView:
    """Load one persisted TIDAL collection view without writing the profile."""

    view: LibraryView
    preferred_id: str | None = None
    #: Jednorazowy, gotowy komunikat po zmianie sortowania. Nie zawiera
    #: wartości modelu ani identyfikatora polecenia.
    announcement: str = ""


@dataclass(frozen=True, slots=True)
class OpenTidalContainer:
    """Read one online TIDAL container through the existing C# integration."""

    item_id: str
    service_id: str
    title: str
    kind: str
    artist: str = ""
    public_uri: str | None = None
    artist_section: str | None = None
    preferred_id: str | None = None


@dataclass(frozen=True, slots=True)
class TidalRelationChoice:
    """One deliberate user-facing destination available from a TIDAL row."""

    label: str
    target: OpenTidalContainer
    open_artist_overview: bool = False


@dataclass(frozen=True, slots=True)
class ShowTidalRelations:
    """User-facing destination choice requested by Right Arrow in TIDAL."""

    title: str
    choices: tuple[TidalRelationChoice, ...]


@dataclass(frozen=True, slots=True)
class TidalNavigationSnapshot:
    """Exact accessible list state restored by Backspace in TIDAL."""

    view: LibraryView
    heading: str
    rows: tuple[Row, ...]
    selected_id: str | None
    context: OpenTidalContainer | None


@dataclass(frozen=True, slots=True)
class OpenSpotifyView:
    """Request the read-only root of the single Spotify session."""

    preferred_id: str | None = None


@dataclass(frozen=True, slots=True)
class OpenSpotifyCollectionView:
    """Open one named, read-only part of the Spotify collection."""

    item_id: str
    view: str
    title: str
    load_more: bool = False
    preferred_id: str | None = None
    #: Zmiana sortowania odświeża ten sam widok i nie tworzy sztucznego
    #: poziomu, z którego trzeba byłoby potem wychodzić Escape.
    replace_current: bool = False
    #: Jednorazowe potwierdzenie wypowiadane dopiero po zastosowaniu nowego
    #: porządku. Nie należy do zapamiętanego kontekstu nawigacji.
    announcement: str = ""


@dataclass(frozen=True, slots=True)
class OpenSpotifyContainer:
    """Narrow catalogue request; opaque identifiers are never spoken."""

    item_id: str
    service_id: str
    title: str
    kind: str
    artist: str = ""
    public_uri: str | None = None
    section: str | None = None
    load_more: bool = False
    preferred_id: str | None = None


@dataclass(frozen=True, slots=True)
class SpotifyRelationChoice:
    """One user-facing relation command for the selected Spotify row."""

    label: str
    target: OpenSpotifyContainer
    open_artist_overview: bool = False


@dataclass(frozen=True, slots=True)
class ShowSpotifyRelations:
    """User-facing destination choice requested by Right Arrow in Spotify."""

    title: str
    choices: tuple[SpotifyRelationChoice, ...]


@dataclass(frozen=True, slots=True)
class SpotifyNavigationSnapshot:
    """Exact Spotify list and focus restored by Backspace."""

    view: LibraryView
    heading: str
    rows: tuple[Row, ...]
    selected_id: str | None
    context: OpenSpotifyContainer | OpenSpotifyCollectionView | None


@dataclass(slots=True)
class OpenPodcastAggregateView:
    """Load one top-level aggregate without exposing a technical source ID."""

    view: LibraryView
    preferred_id: str | None = None
    load_more: bool = False
    #: Jednorazowy kontekst po zmianie sortowania; zawiera wylacznie jawna,
    #: polska etykiete, nigdy wartosc modelu ani identyfikator komendy.
    announcement: str = ""


@dataclass(slots=True)
class OpenPodcastPlaylistView:
    """Open podcast playlists or the ordered contents of one playlist."""

    playlist_id: str | None = None
    playlist_title: str = ""
    preferred_id: str | None = None
    load_more: bool = False


@dataclass(slots=True)
class PlayTrack:
    path: str
    item_id: str
    title: str
    #: Pozycja startowa w SEKUNDACH. ``play.file`` w LiteHost przyjmuje
    #: ``positionSeconds`` i podaje je prosto do ``_files.Play(item, position,
    #: ...)`` (``LiteEngineHandlers.cs:290``, ``:314``), wiec skok zakladki
    #: idzie JEDNYM wywolaniem. Osobne ``seek`` po ``play`` scigaloby sie z
    #: rozruchem nowego strumienia -- ten wlasnie zeruje czas
    #: (``LiteEngineHandlers.cs:309``), wiec pozycja mogla by zginac.
    #: ``None`` oznacza zwykly start i pozwala hostowi wznowic pozycje z
    #: profilu. Jawne ``0.0`` nadal znaczy „od poczatku” (np. zakladka na
    #: zerze) i musi trafic do protokolu, zamiast zostac pomylone z brakiem.
    position_seconds: float | None = None


@dataclass(slots=True)
class PlayStation:
    url: str
    item_id: str
    title: str


@dataclass(slots=True)
class PlayMedia:
    """Play a local or HTTP media source while preserving its stable AMC Id."""

    source: str
    item_id: str
    title: str
    position_seconds: float = 0.0


@dataclass(slots=True)
class PlayTidalTrack:
    """Przekaz jeden utwor do oryginalnej aplikacji TIDAL.

    To nie jest zrodlo dzwieku dla silnika AMC. Id albumu pozostaje polem
    modelu potrzebnym wspolnemu kontrolerowi C#; nie wolno skladac go do
    komunikatu ani nazwy dostepnej.
    """

    item_id: str
    title: str
    service_id: str
    related_album_service_id: str
    previous_item_id: str = ""
    previous_title: str = ""


@dataclass(slots=True)
class PlayTidalContainer:
    """Przekaz caly album albo playliste do oryginalnego TIDALa."""

    item_id: str
    title: str
    service_id: str
    kind: str
    previous_item_id: str = ""
    previous_title: str = ""


@dataclass(slots=True)
class PlaySpotifyItem:
    """Play one Spotify track or episode through the native controller."""

    item_id: str
    title: str
    service_id: str
    kind: str
    previous_item_id: str = ""
    previous_title: str = ""
    artist: str = ""
    preserve_navigation: bool = False
    success_announcement: str = ""


@dataclass(slots=True)
class ActivateWiiMDevice:
    """Wybierz zapisane urzadzenie i odczytaj jego rzeczywisty stan."""

    item_id: str
    title: str
    device_id: str


@dataclass(slots=True)
class ActivateFrontierSmartDevice:
    """Otwórz odtwarzacz zapisanego urządzenia zgodnego z FSAPI."""

    item_id: str
    title: str
    device_id: str


@dataclass(slots=True)
class PlayWiiMNetworkStream:
    """Otwórz zapisany strumień na wybranym urządzeniu WiiM."""

    item_id: str
    title: str
    device_id: str
    stream_id: str


@dataclass(slots=True)
class OpenWiiMDevicePresets:
    """Otwórz listę presetów wybranego urządzenia WiiM."""


@dataclass(slots=True)
class OpenWiiMNetworkStreams:
    """Otwórz strumienie zapisane w AMC dla wybranego urządzenia WiiM."""


@dataclass(slots=True)
class ActivateSonosGroup:
    """Wybierz grupę Sonos; identyfikatory nie są tekstem dostępnym."""

    item_id: str
    title: str
    household_id: str
    group_id: str


@dataclass(slots=True)
class OpenSonosAccount:
    """Otwórz konto Sonos z celowego wiersza pustej sesji."""


@dataclass(slots=True)
class OpenSonosView:
    """Otwórz korzeń Biblioteki albo jedną listę materiałów Sonosa."""

    view: LibraryView
    household_id: str = ""
    preferred_id: str | None = None


@dataclass(slots=True)
class PlaySonosMaterial:
    """Uruchom ulubiony, playlistę albo zapisaną stację w aktywnej grupie."""

    item_id: str
    title: str
    household_id: str
    kind: str
    material_id: str


@dataclass(slots=True)
class PlayFromQueue:
    """Zlecenie: uruchom ZYWA kolejke hosta od wskazanego wiersza.

    Rozni sie od ``PlayTrack`` tym, co dzieje sie PO koncu utworu. ``PlayTrack``
    odtwarza jedna pozycje i nic dalej; tutaj host dostaje cala kolejke, a
    nastepstwo liczy sesja Core (``DemoMediaSession``) -- ten sam silnik, ktory
    prowadzi kolejke w pelnym AMC.

    ``rows`` jest w KOLEJNOSCI WIDOKU, bo taka jest kolejnosc zapisanej kolejki.
    Nawigacja nie sortuje jej ponownie i nie zgaduje kolejnosci z tytulow.

    ``flags`` oddaje per wiersz ``(is_in_queue, is_play_next)`` ODCZYTANE z
    profilu. Nie wymuszamy ``True`` wszystkim pozycjom: host odroznia zwykla
    kolejke od bloku "odtworz nastepne" (``DemoMediaSession.SynchronizeQueueOrder``),
    a gole wymuszenie skasowaloby te roznice i parytet z pelnym AMC.
    """

    item_id: str
    rows: tuple[Row, ...]
    title: str
    flags: dict[str, tuple[bool, bool]] = field(default_factory=dict)


@dataclass(slots=True)
class PlayQueueAt:
    """Zlecenie: zacznij od tego wiersza kolejki, ktora host JUZ trzyma.

    Rozni sie od ``PlayFromQueue`` tym, czego NIE robi: nie wysyla kolejki od
    nowa. W zywym widoku kolejka jest wlasnie tym, co host ma w sesji, wiec
    ponowne ``queue.set`` skasowaloby jej stan (pozycje skonsumowane, blok
    "odtworz nastepne") i podmienilo go na migawke z ekranu. ``queue.status``
    nie oddaje sciezek, wiec odtworzenie tej kolejki w ogole nie byloby mozliwe
    -- i nie jest potrzebne.
    """

    item_id: str
    title: str


@dataclass(slots=True)
class OpenQueueView:
    """Ctrl+Q: najpierw ZAPYTAJ host, co faktycznie jest w kolejce.

    Host rozróżnia brak inicjalizacji od kolejki opróżnionej po odtwarzaniu.
    Tylko przed inicjalizacją sięgamy do zapisu profilu. Pusta żywa kolejka
    pozostaje pusta: nie przywracamy zużytych pozycji z dysku.

    Zlecenie jest osobne od ``OpenLibraryView``, bo tamto idzie po dane do
    profilu (SQLite), a to po stan do silnika. Zlanie ich w jedno zmusiloby
    warstwe danych do znajomosci hosta.
    """


@dataclass(slots=True)
class Announce:
    """Krotki komunikat dla czytnika. JEDNA brama komunikatow w calej aplikacji."""

    text: str


#: Klucz sesji plikow lokalnych. Ta sama wartosc co
#: ``library_activity.LOCAL_SESSION``; test ``test_all_bookmarks_gui_wiring``
#: pilnuje, zeby obie nie rozjechaly sie po cichu. Nawigacja nie importuje
#: warstwy danych, zeby zostac wolna od SQLite.
LOCAL_SESSION = "local"

#: Prefiksy, ktore NIE sa profilowym Id materialu. ``file:<path>`` sklada host
#: przy przegladaniu folderow (``LiteEngineHandlers.cs:269``), a
#: ``bookmark:<id>`` to Id ZAPISU zakladki (``MainWindow.xaml.cs:13756``).
#: Podanie ktoregokolwiek jako ``currentItemId`` dalo by kontekst, ktory do
#: niczego nie pasuje, a wygladalby poprawnie.
_NON_PROFILE_ID_PREFIXES = ("file:", "bookmark:", "dir:", "playlist:", "station:")


def profile_material_id(item_id: str) -> str:
    """Profilowe Id albo pusty napis. Zadnego zgadywania po dlugosci ani nazwie."""
    if not item_id or item_id.startswith(_NON_PROFILE_ID_PREFIXES):
        return ""
    return item_id


def view_context(state: SessionState) -> tuple:
    """TOZSAMOSC WIDOKU, ktory lista ma pokazywac. Z istniejacych pol sesji.

    PO CO TO JEST. Kontrolka musi rozpoznac RZECZYWISTA zmiane calego widoku
    (Foldery -> Wszystkie pliki, wejscie w folder, zawartosc playlisty,
    zakladki pliku) i odrozniac ja od zwyklej zmiany danych W TYM SAMYM
    widoku (usuniecie jednego wiersza, dopisanie, zmiana tekstu). Bez tego
    jedyna dostepna przeslanka byla heurystyka "plan usuwa wiersz z fokusem i
    cos wstawia" -- a ona nie odrozniala usuniecia pozycji z biezacej listy od
    przejscia do innego widoku.

    Zadnego nowego systemu stanu: to sa te same pola ``SessionState``, ktorymi
    nawigacja juz opisuje widok. ``session_id`` jest w kluczu, bo kazda sesja
    ma wlasna liste.
    """
    return (
        state.session_id,
        state.library_view,
        state.library_playlist_id,
        state.library_item_id,
        state.folder_path,
    )


def _items_word(count: int) -> str:
    """Polska odmiana po liczbie: 1 pozycja, 2-4 pozycje, 5+ pozycji.

    Czytnik wymawia to doslownie, wiec "1 pozycji" brzmi jak blad programu.
    """
    if count == 1:
        return "pozycja"
    if count % 10 in (2, 3, 4) and count % 100 not in (12, 13, 14):
        return "pozycje"
    return "pozycji"


def _radioarchiwista_period(row: Row) -> tuple[str, int | None, int | None, int | None, "LibraryView"] | None:
    """Poziom katalogu ukryty w wierszu roku/miesiąca/dnia.

    Format ``item_id`` jest wewnętrznym kontraktem modułu źródła
    (``rarch:years:{slug}:{rok}`` itd.); slug nie zawiera dwukropka, więc
    podział od końca jest jednoznaczny.
    """

    kind = row.service_kind or ""
    parts = row.item_id.split(":")
    try:
        if kind == "rarchYear" and row.service_id and len(parts) == 4:
            return (
                row.service_id,
                int(parts[-1]),
                None,
                None,
                LibraryView.RADIOARCHIWISTA_MONTHS,
            )
        if kind == "rarchMonth" and row.service_id and len(parts) == 5:
            return (
                row.service_id,
                int(parts[-2]),
                int(parts[-1]),
                None,
                LibraryView.RADIOARCHIWISTA_DAYS,
            )
        if kind == "rarchDay" and row.service_id and len(parts) == 6:
            return (
                row.service_id,
                int(parts[-3]),
                int(parts[-2]),
                int(parts[-1]),
                LibraryView.RADIOARCHIWISTA_HOURS,
            )
    except ValueError:
        return None
    return None


_RADIOARCHIWISTA_HEADING_KEYS = {
    LibraryView.RADIOARCHIWISTA_STATIONS: "rarch.heading.stations",
    LibraryView.RADIOARCHIWISTA_YEARS: "rarch.heading.years",
    LibraryView.RADIOARCHIWISTA_MONTHS: "rarch.heading.months",
    LibraryView.RADIOARCHIWISTA_DAYS: "rarch.heading.days",
    LibraryView.RADIOARCHIWISTA_HOURS: "rarch.heading.hours",
    LibraryView.RADIOARCHIWISTA_FAVORITES: "rarch.heading.favorites",
}


def radioarchiwista_heading(view: LibraryView) -> str:
    if view is LibraryView.RADIOARCHIWISTA_QUEUE:
        return "Kolejka Radio Archiwisty"
    key = _RADIOARCHIWISTA_HEADING_KEYS.get(view)
    if key is None:
        return i18n.tr("session.radioarchiwista")
    return i18n.tr(key)


class Navigator:
    """Maszyna stanow calego interfejsu.

    Metody zwracaja LISTE zadan (otworz folder, zagraj, powiedz), ktore okno
    wykonuje. Zaden przeplyw nie siega stad do wx ani do dysku.
    """

    def __init__(self) -> None:
        self.sessions: dict[SessionId, SessionState] = {
            SessionId.FILES: SessionState(SessionId.FILES),
            SessionId.RADIO: SessionState(SessionId.RADIO),
            SessionId.PODCASTS: SessionState(SessionId.PODCASTS),
            SessionId.TIDAL: SessionState(SessionId.TIDAL),
            SessionId.WIIM: SessionState(SessionId.WIIM),
            SessionId.SONOS: SessionState(SessionId.SONOS),
            SessionId.SPOTIFY: SessionState(SessionId.SPOTIFY),
            SessionId.FRONTIER_SMART: SessionState(SessionId.FRONTIER_SMART),
            SessionId.RADIOARCHIWISTA: SessionState(SessionId.RADIOARCHIWISTA),
        }
        self.active = SessionId.FILES
        # Plik z ``play_local_file``, na którego folder jeszcze czekamy.
        # Zużywa go pierwsze ``apply_folder``.
        self._opened_file_id: str | None = None
        # Osobny punkt podmiany pozwala testom sprawdzic dokladna kolejnosc
        # bez uzalezniania ich od globalnego generatora liczb losowych.
        self._shuffle_rows = random.shuffle

    @property
    def session(self) -> SessionState:
        return self.sessions[self.active]

    @property
    def view(self) -> View:
        return self.session.view

    @staticmethod
    def supports_shuffle(session_id: SessionId) -> bool:
        return session_id in {
            SessionId.FILES,
            SessionId.PODCASTS,
            SessionId.SPOTIFY,
        }

    def _shuffled_source(
        self,
        rows: tuple[Row, ...],
        current_id: str | None,
    ) -> tuple[Row, ...]:
        """Losuj bez gubienia biezacego elementu i bez duplikatow."""

        current = next(
            (row for row in rows if row.item_id == current_id),
            None,
        )
        remaining = [
            row for row in rows
            if current is None or row.item_id != current.item_id
        ]
        self._shuffle_rows(remaining)
        return tuple(([current] if current is not None else []) + remaining)

    def _set_playback_source(
        self,
        state: SessionState,
        rows: tuple[Row, ...],
        *,
        uses_queue: bool = False,
    ) -> None:
        """Ustaw jedno zrodlo dla Enter, Page Up/Down i losowania."""

        normalized = tuple(rows)
        state.unshuffled_playback_source_rows = normalized
        state.playback_uses_queue = uses_queue
        state.playback_source_rows = (
            self._shuffled_source(normalized, state.now_playing_id)
            if state.shuffle_enabled and not uses_queue
            else normalized
        )

    def set_shuffle_enabled(
        self,
        session_id: SessionId,
        enabled: bool,
    ) -> bool | None:
        """Przelacz losowanie jednej wspieranej sesji bez ruszania kolejki."""

        if not self.supports_shuffle(session_id):
            return None
        state = self.sessions[session_id]
        enabled = bool(enabled)
        if state.shuffle_enabled == enabled:
            return enabled
        state.shuffle_enabled = enabled
        if state.playback_uses_queue:
            # Jawna kolejka (w tym blok „odtworz nastepne”) ma pierwszenstwo.
            return enabled
        base = (
            state.unshuffled_playback_source_rows
            or state.playback_source_rows
        )
        state.unshuffled_playback_source_rows = tuple(base)
        state.playback_source_rows = (
            self._shuffled_source(tuple(base), state.now_playing_id)
            if enabled
            else tuple(base)
        )
        return enabled

    def toggle_shuffle(self, session_id: SessionId) -> bool | None:
        state = self.sessions[session_id]
        return self.set_shuffle_enabled(session_id, not state.shuffle_enabled)

    def continue_shuffled_after_end(
        self,
        session_id: SessionId,
    ) -> list[object] | None:
        """Po naturalnym koncu uruchom kolejny element losowej sekwencji.

        Funkcja dostaje sesje zdarzenia, a nie aktualnie ogladana sesje.
        Dzieki temu muzyka moze kontynuowac w tle bez przenoszenia fokusu.
        ``None`` oznacza: losowanie nie prowadzi tego transportu albo sekwencja
        sie skonczyla i zwykly komunikat konca ma zostac zachowany.
        """

        if not self.supports_shuffle(session_id):
            return None
        state = self.sessions[session_id]
        if (
            not state.shuffle_enabled
            or state.playback_uses_queue
            or not state.playback_source_rows
            or not state.now_playing_id
        ):
            return None
        current_index = next(
            (
                index for index, row in enumerate(state.playback_source_rows)
                if row.item_id == state.now_playing_id
            ),
            -1,
        )
        target_index = current_index + 1
        if current_index < 0 or target_index >= len(state.playback_source_rows):
            return None
        target = state.playback_source_rows[target_index]
        previous_title = state.now_playing_title
        state.now_playing_id = target.item_id
        state.now_playing_title = target.title
        if session_id is SessionId.SPOTIFY:
            intent = self._spotify_play_intent(target)
            if isinstance(intent, Announce):
                return [intent]
            intent.previous_item_id = state.current_material_id
            intent.previous_title = previous_title
            intent.preserve_navigation = True
            state.pending_material_id = target.item_id
            return [intent, Announce(target.title)]
        if session_id is SessionId.PODCASTS:
            state.pending_material_id = target.item_id
            return [
                PlayMedia(
                    target.path or "",
                    target.item_id,
                    target.title,
                    position_seconds=target.position_seconds,
                ),
                Announce(target.title),
            ]
        state.pending_material_id = profile_material_id(target.item_id)
        return [
            PlayTrack(target.path or "", target.item_id, target.title),
            Announce(target.title),
        ]

    # ---------------------------------------------------------------- sesje

    def switch_session(self, session_id: SessionId) -> list[object]:
        """Ctrl+1 / Ctrl+2. Lista i zaznaczenie DRUGIEJ sesji zostaja nietkniete."""
        if session_id == self.active:
            # Ponowne wejscie w te sama sesje tylko potwierdza nazwe - bez
            # przeladowania listy i bez zmiany zaznaczenia (zwyczaj AMC).
            return [Announce(self._session_name(session_id))]
        self.active = session_id
        state = self.sessions[session_id]
        row = state.model.selected_row
        where = "odtwarzacz" if state.view is View.PLAYER else "lista"
        detail = f", {row.title}" if row is not None and state.view is View.LIST else ""
        return [Announce(f"{self._session_name(session_id)}, {where}{detail}")]

    def apply_wiim_devices(
        self, rows: list[Row], preferred_id: str | None = None
    ) -> list[object]:
        """Wstaw liste urzadzen bez zmiany stanu innych sesji."""
        state = self.sessions[SessionId.WIIM]
        state.wiim_device_rows = tuple(rows)
        state.model.replace(rows, preferred_id=preferred_id or state.list_anchor_id)
        selected = state.model.selected_row
        if (
            selected is not None
            and selected.kind == "device"
            and selected.service_kind == "device"
        ):
            # To jest osobna kotwica korzenia WiiM. ``model.selected_id``
            # zaraz po Enter zmieni sie na pozycje "Presety" albo
            # "Strumienie", ale po restarcie mamy wrocic do URZADZENIA, a
            # nie probowac zaznaczyc technicznie niepasujacego podwidoku.
            state.wiim_device_return_id = selected.item_id
            state.list_anchor_id = selected.item_id
        state.view = View.LIST
        state.library_view = None
        count = len(rows)
        return [Announce(
            f"WiiM, {count} {_items_word(count)}"
            if count
            else "WiiM, brak zapisanych urządzeń"
        )]

    def apply_frontier_smart_devices(
        self, rows: list[Row], preferred_id: str | None = None
    ) -> list[object]:
        """Wstaw zapisane urządzenia FSAPI bez ujawniania ich adresów."""
        state = self.sessions[SessionId.FRONTIER_SMART]
        state.model.replace(rows, preferred_id=preferred_id or state.list_anchor_id)
        selected = state.model.selected_row
        if selected is not None:
            state.list_anchor_id = selected.item_id
        state.view = View.LIST
        state.library_view = None
        count = len(rows)
        return [Announce(
            f"Frontier Smart, {count} {_items_word(count)}"
            if count
            else "Frontier Smart, brak zapisanych urządzeń"
        )]

    def apply_wiim_network_streams(
        self,
        rows: list[Row],
        *,
        device_id: str,
        return_id: str | None,
        preferred_id: str | None = None,
        return_to_sources: bool = False,
    ) -> list[object]:
        """Pokaż zapisane strumienie jako podwidok wybranego urządzenia."""
        state = self.sessions[SessionId.WIIM]
        remembered = (
            state.model.selected_id
            if state.library_view is LibraryView.WIIM_NETWORK_STREAMS
            else None
        )
        if state.library_view not in (
            LibraryView.WIIM_SOURCE_LIBRARY,
            LibraryView.WIIM_NETWORK_STREAMS,
        ):
            state.wiim_device_rows = tuple(state.model.rows)
        state.wiim_device_return_id = return_id
        state.wiim_streams_return_to_sources = return_to_sources
        state.library_item_id = device_id
        state.library_view = LibraryView.WIIM_NETWORK_STREAMS
        state.model.replace(rows, preferred_id=preferred_id or remembered)
        state.view = View.LIST
        count = len(rows)
        return [Announce(
            f"Strumienie WiiM, {count} {_items_word(count)}"
            if count else "Strumienie WiiM, zero elementów"
        )]

    def open_wiim_source_library(
        self,
        *,
        device_id: str | None = None,
        return_id: str | None = None,
    ) -> list[object]:
        """Ctrl+L: pokaż dwa rzeczywiste źródła dostępne w sesji WiiM."""

        state = self.sessions[SessionId.WIIM]
        if state.library_view not in (
            LibraryView.WIIM_SOURCE_LIBRARY,
            LibraryView.WIIM_NETWORK_STREAMS,
        ):
            state.wiim_device_rows = tuple(state.model.rows)
        state.wiim_device_return_id = return_id or state.wiim_device_return_id
        state.library_item_id = device_id or state.library_item_id
        state.library_view = LibraryView.WIIM_SOURCE_LIBRARY
        state.model.replace([
            Row(
                "wiim-source:device-presets",
                "Presety urządzenia",
                "wiimSource",
                service_kind="devicePresets",
            ),
            Row(
                "wiim-source:amc-streams",
                "Strumienie zapisane w AMC",
                "wiimSource",
                service_kind="networkStreams",
            ),
        ])
        state.view = View.LIST
        return [Announce("Biblioteka źródeł WiiM, 2 pozycje")]

    def apply_sonos_targets(
        self, rows: list[Row], preferred_id: str | None = None
    ) -> list[object]:
        """Wstaw grupy Sonos bez zmiany stanu pozostałych sesji."""
        state = self.sessions[SessionId.SONOS]
        state.sonos_target_rows = tuple(rows)
        state.library_view = None
        state.library_item_id = None
        state.model.replace(rows, preferred_id=preferred_id or state.list_anchor_id)
        state.view = View.LIST
        count = len(rows)
        return [Announce(
            f"Sonos, {count} {_items_word(count)}"
            if count
            else "Sonos, brak dostępnych grup"
        )]

    def apply_sonos_account_prompt(self) -> list[object]:
        """Zostaw w pustej sesji jedną dostępną drogę do logowania."""

        state = self.sessions[SessionId.SONOS]
        state.sonos_target_rows = ()
        state.library_view = None
        state.library_item_id = None
        state.model.replace([
            Row(
                "sonos:account-login",
                "Rozpocznij logowanie do Sonos",
                "sonosAccount",
                detail="Enter otwiera konto Sonos",
                service_kind="sonosAccount",
            )
        ])
        state.view = View.LIST
        return [Announce(
            "Sonos. Konto nie jest połączone. "
            "Naciśnij Enter, aby się zalogować"
        )]

    def apply_sonos_account_pending_prompt(self) -> list[object]:
        """Po przejściu do przeglądarki zostaw dostępny punkt powrotu."""

        state = self.sessions[SessionId.SONOS]
        state.sonos_target_rows = ()
        state.library_view = None
        state.library_item_id = None
        state.model.replace([
            Row(
                "sonos:account-check",
                "Sprawdź logowanie do Sonos",
                "sonosAccount",
                detail="Enter otwiera konto Sonos",
                service_kind="sonosAccount",
            )
        ])
        state.view = View.LIST
        return [Announce(
            "Strona logowania Sonos została otwarta. Po powrocie naciśnij "
            "Enter, aby sprawdzić wynik"
        )]

    def apply_sonos_library(
        self,
        rows: list[Row],
        *,
        household_id: str,
        preferred_id: str | None = None,
    ) -> list[object]:
        state = self.sessions[SessionId.SONOS]
        state.library_view = LibraryView.SONOS_LIBRARY
        state.library_playlist_id = None
        state.library_item_id = household_id
        state.model.replace(rows, preferred_id=preferred_id)
        state.view = View.LIST
        return [Announce("Biblioteka Sonos" if rows else "Zero elementów")]

    def apply_sonos_materials(
        self,
        view: LibraryView,
        rows: list[Row],
        *,
        household_id: str,
        preferred_id: str | None = None,
        return_id: str | None = None,
    ) -> list[object]:
        if view not in (
            LibraryView.SONOS_FAVORITES,
            LibraryView.SONOS_PLAYLISTS,
            LibraryView.SONOS_OWN_STREAMS,
        ):
            raise ValueError("Nieznany widok materiałów Sonos")
        state = self.sessions[SessionId.SONOS]
        state.library_view = view
        state.library_item_id = household_id
        state.library_return_id = return_id or state.library_return_id
        state.model.replace(rows, preferred_id=preferred_id)
        state.view = View.LIST
        return []

    @staticmethod
    def _session_name(session_id: SessionId) -> str:
        # Ta mapa musi pokrywac caly enum SessionId: switch_session oglasza
        # nazwe przy KAZDYM przelaczeniu, a brak wpisu ubijal GUI (KeyError).
        return {
            SessionId.FILES: "Pliki lokalne",
            SessionId.RADIO: "Radio",
            SessionId.PODCASTS: "Podcasty i YouTube",
            SessionId.TIDAL: "TIDAL",
            SessionId.WIIM: "WiiM",
            SessionId.SONOS: "Sonos",
            SessionId.SPOTIFY: "Spotify",
            SessionId.FRONTIER_SMART: "Frontier Smart",
            SessionId.RADIOARCHIWISTA: "Radio Archiwista",
        }[session_id]

    def capture_transient_navigation(self) -> TransientNavigationSnapshot:
        """Zapamietaj dokladne miejsce przed wspolnym podgladem."""
        state = self.session
        return TransientNavigationSnapshot(
            session_id=self.active,
            view=state.view,
            rows=tuple(state.model.rows),
            selected_id=state.model.selected_id,
            list_anchor_id=state.list_anchor_id,
            player_entry_anchor_id=state.player_entry_anchor_id,
            folder_path=state.folder_path,
            breadcrumb=tuple(state.breadcrumb),
            library_view=state.library_view,
            library_playlist_id=state.library_playlist_id,
            library_return_id=state.library_return_id,
            library_item_id=state.library_item_id,
            bookmark_targets=dict(state.bookmark_targets),
            bookmark_contexts=dict(state.bookmark_contexts),
            queue_flags=dict(state.queue_flags),
            library_folder_path=state.library_folder_path,
            library_return_view=state.library_return_view,
            view_selected_ids=dict(state.view_selected_ids),
            tidal_heading=state.tidal_heading,
            tidal_context=state.tidal_context,
            tidal_history=tuple(state.tidal_history),
            spotify_heading=state.spotify_heading,
            spotify_context=state.spotify_context,
            spotify_history=tuple(state.spotify_history),
            wiim_device_rows=tuple(state.wiim_device_rows),
            wiim_device_return_id=state.wiim_device_return_id,
            wiim_streams_return_to_sources=state.wiim_streams_return_to_sources,
            sonos_target_rows=tuple(state.sonos_target_rows),
        )

    def restore_transient_navigation(
        self, snapshot: TransientNavigationSnapshot
    ) -> list[object]:
        """Escape z podgladu oddaje sesje, widok, wiersz i odtwarzacz.

        Powrot jest celowo cichy. Po podmianie danych natywna lista Windows
        sama wypowiada odzyskany wiersz pod fokusem. Osobne zdanie
        ``Powrot: Radio internetowe, lista`` dublowalo te informacje i
        opoznialo dojscie do nazwy stacji, ktora jest dla uzytkownika wazna.
        """
        self.active = snapshot.session_id
        state = self.sessions[snapshot.session_id]
        spotify_history_selected_id = (
            state.model.selected_id
            if state.library_view is LibraryView.SPOTIFY_HISTORY
            else None
        )
        state.view = snapshot.view
        state.list_anchor_id = snapshot.list_anchor_id
        state.player_entry_anchor_id = snapshot.player_entry_anchor_id
        state.folder_path = snapshot.folder_path
        state.breadcrumb = list(snapshot.breadcrumb)
        state.library_view = snapshot.library_view
        state.library_playlist_id = snapshot.library_playlist_id
        state.library_return_id = snapshot.library_return_id
        state.library_item_id = snapshot.library_item_id
        state.bookmark_targets = dict(snapshot.bookmark_targets)
        state.bookmark_contexts = dict(snapshot.bookmark_contexts)
        state.queue_flags = dict(snapshot.queue_flags)
        state.library_folder_path = snapshot.library_folder_path
        state.library_return_view = snapshot.library_return_view
        state.view_selected_ids = dict(snapshot.view_selected_ids)
        if spotify_history_selected_id:
            state.view_selected_ids[
                LibraryView.SPOTIFY_HISTORY.value
            ] = spotify_history_selected_id
        state.tidal_heading = snapshot.tidal_heading
        state.tidal_context = snapshot.tidal_context
        state.tidal_history = list(snapshot.tidal_history)
        state.spotify_heading = snapshot.spotify_heading
        state.spotify_context = snapshot.spotify_context
        state.spotify_history = list(snapshot.spotify_history)
        state.wiim_device_rows = tuple(snapshot.wiim_device_rows)
        state.wiim_device_return_id = snapshot.wiim_device_return_id
        state.wiim_streams_return_to_sources = snapshot.wiim_streams_return_to_sources
        state.sonos_target_rows = tuple(snapshot.sonos_target_rows)
        state.model.replace(list(snapshot.rows), preferred_id=snapshot.selected_id)
        return []

    # ---------------------------------------------------------------- widoki

    def show_player(self) -> list[object]:
        """F6 z listy. Zgodnie z MainWindow.xaml.cs:20778 (ShowPlayerView)."""
        state = self.session
        if state.view is View.PLAYER:
            # F6 w odtwarzaczu wraca na liste (MainWindow.xaml.cs:20776).
            return self.back_to_list()
        if state.now_playing_id is None:
            return [Announce("Nic nie jest odtwarzane")]
        state.player_entry_anchor_id = state.model.selected_id
        state.list_anchor_id = state.model.selected_id
        state.view = View.PLAYER
        return [Announce(f"Odtwarzacz, {state.now_playing_title}")]

    def toggle_view(self) -> list[object]:
        """Zachowane dla wygody: rownowazne show_player/back_to_list."""
        state = self.session
        if state.view is View.LIST:
            if state.now_playing_id is None:
                return [Announce("Nic nie jest odtwarzane")]
            state.player_entry_anchor_id = state.model.selected_id
            state.list_anchor_id = state.model.selected_id
            state.view = View.PLAYER
            return [Announce(f"Odtwarzacz, {state.now_playing_title}")]
        return self.back_to_list()

    def back_to_list(self, *, follow_playback: bool = True) -> list[object]:
        """Escape z odtwarzacza: TA SAMA lista i TO SAMO zaznaczenie.

        Gdy wiersz istnieje, powrot jest celowo cichy: po pokazaniu i
        zafokusowaniu natywna lista Windows sama czyta jego nazwe, kolumny i
        pozycje. Wlasne ``Lista, nazwa`` dublowalo ten odczyt i opoznialo
        najwazniejsza informacje. Pusta lista niczego sama nie wypowie, wiec
        tylko ona zachowuje jednoznaczny komunikat.
        """
        state = self.session
        if state.view is View.LIST:
            return []
        state.view = View.LIST
        target = state.list_anchor_id if follow_playback else state.player_entry_anchor_id
        if target is not None:
            state.model.select_id(target)
        row = state.model.selected_row
        return [] if row is not None else [Announce("Lista, zero elementów")]

    # ------------------------------------------------------------- aktywacja

    def activate_selected(
        self, *, open_player_on_activation: bool = True
    ) -> list[object]:
        """Enter otwiera folder albo uruchamia zaznaczony materiał.

        Jedna jawna decyzja widoku obowiązuje wszystkie materiały odtwarzane
        przez AMC. Kontenery i cele sterowania nadal prowadzą do swoich list.
        """
        state = self.session
        row = state.model.selected_row
        if row is None:
            return [Announce("Lista jest pusta")]

        if row.kind == "parent":
            return self.go_to_parent()

        if state.library_view is LibraryView.SERVICE_HISTORY and row.activation_message:
            return [Announce(row.activation_message)]

        if state.session_id is SessionId.WIIM:
            # Wpis Historii pozostaje czytelny, gdy urządzenie nie oddało
            # stabilnego celu potrzebnego do ponownego uruchomienia.
            if row.activation_message:
                return [Announce(row.activation_message)]
            if row.kind == "wiimSource":
                if row.service_kind == "devicePresets":
                    return [OpenWiiMDevicePresets()]
                if row.service_kind == "networkStreams":
                    return [OpenWiiMNetworkStreams()]
                return [Announce("To źródło WiiM nie jest dostępne")]
            if (
                row.kind == "wiimNetworkStream"
                and row.service_kind == "wiimNetworkStream"
                and row.service_id
                and row.parent_id
            ):
                state.player_entry_anchor_id = row.item_id
                state.list_anchor_id = row.item_id
                state.now_playing_id = row.item_id
                state.now_playing_title = row.title
                state.current_material_id = row.item_id
                state.view = (
                    View.PLAYER if open_player_on_activation else View.LIST
                )
                return [PlayWiiMNetworkStream(
                    item_id=row.item_id,
                    title=row.title,
                    device_id=row.parent_id,
                    stream_id=row.service_id,
                )]
            if (
                row.kind != "device"
                or row.service_kind != "device"
                or not row.service_id
            ):
                return [Announce("Nie można wybrać tego urządzenia WiiM")]
            activation = ActivateWiiMDevice(
                item_id=row.item_id,
                title=row.title,
                device_id=row.service_id,
            )
            # Urządzenie WiiM jest celem sterowania, a nie materiałem do
            # odtworzenia. Po Enter wybieramy je w hoscie, lecz pozostajemy w
            # nawigacji listowej i od razu pokazujemy dwie rzeczywiste drogi:
            # presety urządzenia oraz strumienie zapisane w AMC. Wcześniejsze
            # wejście do pustego odtwarzacza ukrywało obie listy i sprawiało
            # wrażenie, że Ctrl+L nie działa.
            sources = self.open_wiim_source_library(
                device_id=row.service_id,
                return_id=row.item_id,
            )
            return [activation, *sources]

        if state.session_id is SessionId.FRONTIER_SMART:
            if (
                row.kind != "frontierSmartDevice"
                or row.service_kind != "frontierSmartDevice"
                or not row.service_id
            ):
                return [Announce(
                    "Nie można wybrać tego urządzenia Frontier Smart"
                )]
            state.player_entry_anchor_id = row.item_id
            state.list_anchor_id = row.item_id
            state.now_playing_id = row.item_id
            state.now_playing_title = row.title
            state.view = View.PLAYER
            return [ActivateFrontierSmartDevice(
                item_id=row.item_id,
                title=row.title,
                device_id=row.service_id,
            )]

        if state.session_id is SessionId.SONOS:
            # Jak wyżej: nie udajemy możliwości odtworzenia na podstawie
            # samego tytułu zaobserwowanego na urządzeniu.
            if row.activation_message:
                return [Announce(row.activation_message)]
            if row.kind == "sonosAccount" and row.service_kind == "sonosAccount":
                return [OpenSonosAccount()]
            if (
                row.kind == "sonosCategory"
                and row.parent_id
                and row.service_kind in {"favorites", "playlists", "ownStreams"}
            ):
                target_view = {
                    "favorites": LibraryView.SONOS_FAVORITES,
                    "playlists": LibraryView.SONOS_PLAYLISTS,
                    "ownStreams": LibraryView.SONOS_OWN_STREAMS,
                }[row.service_kind]
                return [OpenSonosView(
                    target_view,
                    household_id=row.parent_id,
                    preferred_id=None,
                )]
            if (
                row.kind in {"sonosFavorite", "sonosPlaylist", "sonosOwnStream"}
                and row.parent_id
                and row.service_id
                and row.service_kind in {"favorite", "playlist", "ownStream"}
            ):
                state.player_entry_anchor_id = row.item_id
                state.list_anchor_id = row.item_id
                state.now_playing_id = row.item_id
                state.now_playing_title = row.title
                state.current_material_id = row.item_id
                state.view = (
                    View.PLAYER if open_player_on_activation else View.LIST
                )
                return [PlaySonosMaterial(
                    item_id=row.item_id,
                    title=row.title,
                    household_id=row.parent_id,
                    kind=row.service_kind,
                    material_id=row.service_id,
                )]
            if (
                row.kind != "sonosGroup"
                or row.service_kind != "sonosGroup"
                or not row.parent_id
                or not row.service_id
            ):
                return [Announce("Nie można wybrać tej grupy Sonos")]
            state.player_entry_anchor_id = row.item_id
            state.list_anchor_id = row.item_id
            state.now_playing_id = row.item_id
            state.now_playing_title = row.title
            state.current_material_id = row.item_id
            state.view = View.LIST
            return [ActivateSonosGroup(
                item_id=row.item_id,
                title=row.title,
                household_id=row.parent_id,
                group_id=row.service_id,
            )]

        if state.session_id is SessionId.TIDAL:
            target = self._tidal_target_from_row(row)
            if target is not None:
                if target.kind == "artist" and target.artist_section is None:
                    return self._open_tidal_artist_overview(row, target)
                return [target]
            if row.kind == "track":
                intent = self._tidal_track_intent(row)
                if isinstance(intent, Announce):
                    return [intent]
                intent.previous_item_id = state.current_material_id
                intent.previous_title = state.now_playing_title
                state.player_entry_anchor_id = row.item_id
                state.list_anchor_id = row.item_id
                state.now_playing_id = row.item_id
                state.now_playing_title = row.title
                state.pending_material_id = row.item_id
                state.playback_source_rows = tuple(
                    candidate for candidate in state.model.rows
                    if candidate.kind == "track"
                )
                state.playback_uses_queue = False
                # Oryginalny TIDAL odtwarza poza AMC. Pelne AMC pozostawia tu
                # fokus na liscie i nie udaje, ze ma wlasny widok odtwarzacza.
                state.view = View.LIST
                return [intent]

        if state.session_id is SessionId.RADIOARCHIWISTA:
            return self._activate_radioarchiwista_row(
                row, open_player_on_activation=open_player_on_activation
            )

        if state.session_id is SessionId.SPOTIFY:
            if row.activation_message:
                return [Announce(row.activation_message)]
            if row.kind == "loadMore":
                context = state.spotify_context
                if isinstance(context, OpenSpotifyCollectionView):
                    return [OpenSpotifyCollectionView(
                        context.item_id,
                        context.view,
                        context.title,
                        load_more=True,
                    )]
                if isinstance(context, OpenSpotifyContainer):
                    return [replace(context, load_more=True)]
                return [Announce("Nie wiadomo, dla którego widoku wczytać elementy")]
            collection = self._spotify_collection_target_from_row(row)
            if collection is not None:
                state.library_return_id = row.item_id
                return [collection]
            target = self._spotify_target_from_row(row)
            if target is not None:
                if target.kind == "artist" and target.section is None:
                    return self._open_spotify_artist_overview(row, target)
                return [target]
            if row.kind in ("track", "episode"):
                intent = self._spotify_play_intent(row)
                if isinstance(intent, Announce):
                    return [intent]
                intent.previous_item_id = state.current_material_id
                intent.previous_title = state.now_playing_title
                state.player_entry_anchor_id = row.item_id
                state.list_anchor_id = row.item_id
                state.now_playing_id = row.item_id
                state.now_playing_title = row.title
                state.pending_material_id = row.item_id
                self._set_playback_source(
                    state,
                    tuple(
                        candidate for candidate in state.model.rows
                        if candidate.kind in ("track", "episode")
                        and not candidate.activation_message
                    ),
                )
                state.view = (
                    View.PLAYER if open_player_on_activation else View.LIST
                )
                return [intent]
            return [Announce("Nie można otworzyć tego elementu Spotify")]

        if row.activation_message:
            # Wiersz zostaje zaznaczony i widoczny. Kontenery TIDAL sa
            # rozpoznane wyzej po jawnych polach modelu, dlatego zwykla
            # playlista lokalna nadal trafia do swojej dotychczasowej drogi.
            return [Announce(row.activation_message)]

        if row.kind == "playlist":
            return self._enter_playlist(row)

        if row.kind == "podcast":
            state.library_return_id = row.item_id
            return [OpenPodcastView(row.item_id, row.title)]

        if row.kind == "loadMore":
            if state.library_view is LibraryView.PODCAST_PLAYLIST_CONTENTS:
                if not state.library_playlist_id:
                    return [Announce("Nie wiadomo, dla której playlisty wczytać elementy")]
                return [OpenPodcastPlaylistView(
                    playlist_id=state.library_playlist_id,
                    playlist_title=state.library_return_view or "Playlista",
                    load_more=True,
                )]
            if state.library_view in (
                LibraryView.PODCAST_FAVORITES,
                LibraryView.PODCAST_HISTORY,
                LibraryView.PODCAST_QUEUE,
                LibraryView.PODCAST_INBOX,
                LibraryView.PODCAST_DOWNLOADS,
            ):
                return [OpenPodcastAggregateView(state.library_view, load_more=True)]
            if not state.library_playlist_id:
                return [Announce("Nie wiadomo, dla którego podcastu wczytać odcinki")]
            return [OpenPodcastView(
                state.library_playlist_id,
                state.library_return_view or "Podcast",
                load_more=True,
            )]

        if row.kind == "folder":
            if not row.path:
                return [Announce("Brak sciezki folderu")]
            if state.folder_path:
                state.breadcrumb.append((state.folder_path, row.item_id))
            return [OpenFolder(row.path)]

        if row.kind == "station":
            state.player_entry_anchor_id = row.item_id
            state.list_anchor_id = row.item_id
            state.now_playing_id = row.item_id
            state.now_playing_title = row.title
            # Kandydat, nie fakt: material potwierdzi dopiero udany start.
            state.pending_material_id = profile_material_id(row.item_id)
            state.playback_source_rows = tuple(
                candidate for candidate in state.model.rows
                if candidate.kind == "station" and candidate.url
            )
            state.playback_uses_queue = False
            state.view = View.PLAYER if open_player_on_activation else View.LIST
            return [PlayStation(row.url or "", row.item_id, row.title), Announce(row.title)]

        if row.kind == "episode":
            if not row.path:
                return [Announce("Ten odcinek nie ma adresu do odtworzenia")]
            state.player_entry_anchor_id = row.item_id
            state.list_anchor_id = row.item_id
            state.now_playing_id = row.item_id
            state.now_playing_title = row.title
            state.pending_material_id = row.item_id
            self._set_playback_source(
                state,
                tuple(
                    candidate for candidate in state.model.rows
                    if candidate.kind == "episode" and candidate.path
                ),
            )
            state.view = View.PLAYER if open_player_on_activation else View.LIST
            return [
                PlayMedia(
                    row.path,
                    row.item_id,
                    row.title,
                    position_seconds=row.position_seconds,
                ),
                Announce(row.title),
            ]

        # Żywa kolejka ma już ścieżki w hoście. Payload listy niesie tylko ID.
        if state.library_view is LibraryView.LIVE_QUEUE:
            return self._activate_live_queue_row(
                row, open_player=open_player_on_activation
            )

        if not row.path:
            # Wiersz ZAKLADKI nie ma sciezki i miec jej nie moze: jego
            # ``item_id`` to ``bookmark:<id>``, czyli identyfikator ZAPISU, nie
            # pliku. Prawdziwy plik i pozycja przyszly obok, w mapie celow.
            if row.item_id.startswith("bookmark:"):
                return self._activate_bookmark(
                    row, open_player=open_player_on_activation
                )
            return [Announce("Brak sciezki pliku")]

        # Widok Zapisanej kolejki prowadzi ZYWA kolejke hosta, a nie pojedyncze
        # odtworzenie. Zwykle ``files.play`` zerwaloby nastepstwo: po koncu
        # utworu nie byloby czym przejsc dalej.
        if state.library_view is LibraryView.SAVED_QUEUE:
            return self._activate_queue_row(
                row, open_player=open_player_on_activation
            )

        state.player_entry_anchor_id = row.item_id
        state.list_anchor_id = row.item_id
        state.now_playing_id = row.item_id
        state.now_playing_title = row.title
        state.pending_material_id = profile_material_id(row.item_id)
        self._set_playback_source(
            state,
            tuple(
                candidate for candidate in state.model.rows
                if candidate.kind == "track" and candidate.path
            ),
        )
        state.view = View.PLAYER if open_player_on_activation else View.LIST
        return [PlayTrack(row.path, row.item_id, row.title), Announce(row.title)]

    def play_local_file(self, path: str, title: str) -> list[object]:
        """Plik spoza listy: okno „Otwórz plik” albo Eksplorator i Total Commander.

        Ten sam stan co Enter na wierszu folderu. Wcześniej GUI wysyłało
        ``files.play`` z pominięciem nawigatora: plik grał, a F6 mówił „Nic
        nie jest odtwarzane” albo otwierał poprzedni utwór, a Page Down
        skakał po poprzednim folderze. Folder wczytuje się dopiero po starcie,
        dlatego źródłem jest na razie sam plik. ``apply_folder`` poszerza je
        o utwory folderu.
        """
        row = Row(f"file:{path}", title, "track", path=path)
        state = self.sessions[SessionId.FILES]
        state.player_entry_anchor_id = row.item_id
        state.list_anchor_id = row.item_id
        state.now_playing_id = row.item_id
        state.now_playing_title = row.title
        state.pending_material_id = profile_material_id(row.item_id)
        self._set_playback_source(state, (row,))
        self._opened_file_id = row.item_id
        return [PlayTrack(path, row.item_id, row.title)]

    def _tidal_target_from_row(self, row: Row) -> OpenTidalContainer | None:
        """Build a service request without putting its identifiers in speech."""
        if not row.service_id or row.service_kind not in ("album", "artist", "playlist"):
            return None
        if row.service_section not in (None, "albums", "tracks", "similarArtists"):
            return None
        title = row.artist_name if row.service_section else row.title
        if not title:
            return None
        return OpenTidalContainer(
            item_id=row.item_id,
            service_id=row.service_id,
            title=title,
            kind=row.service_kind,
            artist=row.artist_name,
            public_uri=row.url,
            artist_section=row.service_section,
        )

    def open_tidal_search_result(
        self,
        row: Row,
        *,
        relations: bool = False,
    ) -> list[object]:
        """Open an online search row without adding it to the collection.

        Search results do not have to exist in any saved TIDAL view.  Their
        explicit catalogue identity is sufficient to open a container or to
        start a track.  Right Arrow on an artist intentionally opens the same
        three-category overview as Enter on an artist in the regular browser.
        """

        state = self.sessions[SessionId.TIDAL]
        if self.active is not SessionId.TIDAL:
            return [Announce("Wynik wyszukiwania należy do TIDAL")]
        target = self._tidal_target_from_row(row)

        if relations:
            if target is not None and target.kind == "artist":
                return self._open_tidal_artist_overview(row, target)
            choices = self._tidal_relation_choices(row)
            if len(choices) == 1:
                choice = choices[0]
                if choice.open_artist_overview:
                    artist = Row(
                        item_id=choice.target.item_id,
                        title=choice.target.title,
                        kind="artist",
                        service_id=choice.target.service_id,
                        service_kind="artist",
                    )
                    return self._open_tidal_artist_overview(artist, choice.target)
                return [choice.target]
            if choices:
                return [Announce(
                    "Ten wynik prowadzi do kilku miejsc; "
                    "otwórz go i użyj prawej strzałki"
                )]
            return [Announce(
                "Ten wynik nie ma dostępnego albumu ani wykonawcy nadrzędnego"
            )]

        if target is not None:
            if target.kind == "artist" and target.artist_section is None:
                return self._open_tidal_artist_overview(row, target)
            return [target]

        if row.kind == "track":
            intent = self._tidal_track_intent(row)
            if isinstance(intent, Announce):
                return [intent]
            intent.previous_item_id = state.current_material_id
            intent.previous_title = state.now_playing_title
            state.player_entry_anchor_id = row.item_id
            state.list_anchor_id = row.item_id
            state.now_playing_id = row.item_id
            state.now_playing_title = row.title
            state.pending_material_id = row.item_id
            state.playback_source_rows = (row,)
            state.playback_uses_queue = False
            state.view = View.LIST
            return [intent]

        return [Announce("Nie można otworzyć tego wyniku TIDAL")]

    def activate_tidal_search_relation(
        self,
        row: Row,
        choice: TidalRelationChoice,
    ) -> list[object]:
        """Open a relation chosen inside the still-open search window."""

        if self.active is not SessionId.TIDAL:
            return [Announce("Wynik wyszukiwania należy do TIDAL")]
        if choice not in self._tidal_relation_choices(row):
            return [Announce("Wybrane miejsce nie jest już dostępne w TIDAL")]
        if choice.open_artist_overview:
            target = choice.target
            artist = Row(
                item_id=target.item_id,
                title=target.title,
                kind="artist",
                service_id=target.service_id,
                service_kind="artist",
            )
            return self._open_tidal_artist_overview(artist, target)
        return [choice.target]

    @staticmethod
    def _tidal_relation_choices(row: Row) -> tuple[TidalRelationChoice, ...]:
        """Build destinations from the full AMC model without speaking IDs."""
        choices: list[TidalRelationChoice] = []
        if (
            row.kind in ("track", "video")
            and row.related_album_service_id
        ):
            album_title = row.related_album_title or "Album"
            choices.append(TidalRelationChoice(
                "Przejdź do albumu",
                OpenTidalContainer(
                    item_id=f"{row.item_id}:related-album",
                    service_id=row.related_album_service_id,
                    title=album_title,
                    kind="album",
                    artist=row.artist_name,
                ),
            ))
        if row.kind == "artist" and row.service_id:
            choices.append(TidalRelationChoice(
                "Pokaż albumy wykonawcy",
                OpenTidalContainer(
                    item_id=row.item_id,
                    service_id=row.service_id,
                    title=row.title,
                    kind="artist",
                    artist_section="albums",
                ),
            ))
        if (
            row.kind in ("track", "video", "album")
            and row.related_artist_service_id
        ):
            artist_title = (
                row.related_artist_name
                or row.artist_name
                or "Wykonawca"
            )
            choices.append(TidalRelationChoice(
                "Przejdź do wykonawcy",
                OpenTidalContainer(
                    item_id=f"{row.item_id}:related-artist",
                    service_id=row.related_artist_service_id,
                    title=artist_title,
                    kind="artist",
                ),
                open_artist_overview=True,
            ))
        return tuple(choices)

    def tidal_relations(self) -> list[object]:
        """Right Arrow: show only relationships proved by the row model."""
        state = self.session
        if state.session_id is not SessionId.TIDAL:
            return [Announce(
                "Przechodzenie do albumu lub wykonawcy jest dostępne tylko w TIDAL"
            )]
        row = state.model.selected_row
        if row is None:
            return [Announce("Lista TIDAL jest pusta")]
        choices = self._tidal_relation_choices(row)
        if not choices:
            return [Announce(
                "Ten element nie ma dostępnego albumu ani wykonawcy nadrzędnego"
            )]
        return [ShowTidalRelations(
            "Przejdź do albumu lub wykonawcy TIDAL",
            choices,
        )]

    def activate_tidal_relation(
        self,
        choice: TidalRelationChoice,
    ) -> list[object]:
        """Execute one still-valid relation selected from the native menu."""
        state = self.session
        row = state.model.selected_row
        if (
            state.session_id is not SessionId.TIDAL
            or row is None
            or choice not in self._tidal_relation_choices(row)
        ):
            return [Announce("Wybrane miejsce nie jest już dostępne w TIDAL")]
        return self.activate_tidal_search_relation(row, choice)

    def play_selected_tidal_container(self) -> list[object]:
        """Ctrl+Enter: oddaj album lub playliste oryginalnemu TIDALowi."""
        state = self.session
        if state.session_id is not SessionId.TIDAL:
            return [Announce("To polecenie działa tylko w TIDAL")]
        row = state.model.selected_row
        if row is None:
            return [Announce("Lista jest pusta")]
        if row.kind not in ("album", "playlist"):
            return [Announce(
                "Ctrl+Enter odtwarza cały album albo playlistę TIDAL"
            )]
        if row.service_kind != row.kind or not row.service_id:
            return [Announce(
                "Ten element nie ma danych potrzebnych do odtworzenia "
                "w oryginalnym TIDALu"
            )]

        state.list_anchor_id = row.item_id
        state.pending_material_id = row.item_id
        return [PlayTidalContainer(
            item_id=row.item_id,
            title=row.title,
            service_id=row.service_id,
            kind=row.kind,
            previous_item_id=state.current_material_id,
            previous_title=state.now_playing_title,
        )]

    @staticmethod
    def _tidal_track_intent(row: Row) -> PlayTidalTrack | Announce:
        """Build an external-player request without ever speaking its IDs."""
        if not row.service_id:
            return Announce(
                "Ten utwór nie ma identyfikatora TIDAL. "
                "Nie można go przekazać do oryginalnego programu"
            )
        if not row.related_album_service_id:
            return Announce(
                "Ten utwór nie ma przypisanego albumu, a oryginalny TIDAL "
                "otwiera utwory tylko przez stronę albumu"
            )
        return PlayTidalTrack(
            item_id=row.item_id,
            title=row.title,
            service_id=row.service_id,
            related_album_service_id=row.related_album_service_id,
        )

    @staticmethod
    def _tidal_snapshot(state: SessionState) -> TidalNavigationSnapshot:
        return TidalNavigationSnapshot(
            view=state.library_view or LibraryView.TIDAL_LIBRARY,
            heading=state.tidal_heading,
            rows=tuple(state.model.rows),
            selected_id=state.model.selected_id,
            context=state.tidal_context,
        )

    def _open_tidal_artist_overview(
        self,
        row: Row,
        target: OpenTidalContainer,
    ) -> list[object]:
        from .tidal_source import artist_overview_rows

        state = self.session
        rows = list(artist_overview_rows(row))
        if not rows:
            return [Announce("Nie można otworzyć tego wykonawcy TIDAL")]
        state.tidal_history.append(self._tidal_snapshot(state))
        state.library_view = LibraryView.TIDAL_CONTAINER
        state.tidal_heading = f"Wykonawca, {row.title}"
        state.library_return_view = state.tidal_heading
        state.tidal_context = target
        state.model.replace(rows)
        state.view = View.LIST
        return [Announce(f"{state.tidal_heading}, {len(rows)} {_items_word(len(rows))}")]

    def _activate_queue_row(
        self, row: Row, *, open_player: bool
    ) -> list[object]:
        """Enter w widoku Zapisanej kolejki: start ZYWEJ kolejki od tego wiersza.

        Kolejnosc oddajemy hostowi w kolejnosci WIDOKU -- to kolejnosc zapisanej
        kolejki, czytana z profilu. Nawigacja jej nie przelicza.

        Pozycje bez sciezki wypadaja: host nie ma czego dla nich otworzyc, a
        wstawienie ich do kolejki konczyloby sie bledem w trakcie przejscia.
        """
        state = self.session
        playable = tuple(
            candidate for candidate in state.model.rows
            if candidate.kind == "track" and candidate.path
        )
        if not playable:
            return [Announce("Kolejka jest pusta")]

        state.player_entry_anchor_id = row.item_id
        state.list_anchor_id = row.item_id
        state.now_playing_id = row.item_id
        state.now_playing_title = row.title
        state.pending_material_id = profile_material_id(row.item_id)
        self._set_playback_source(state, (), uses_queue=True)
        state.view = View.PLAYER if open_player else View.LIST
        # Czlonkostwo bierzemy z ODCZYTU profilu. Wiersz bez wpisu w mapie to
        # starszy zapis (``legacyRegularQueue``): jest w kolejce zwyklej, nie w
        # bloku "odtworz nastepne" -- i tak go opisujemy, zamiast milczec.
        flags = {
            candidate.item_id: state.queue_flags.get(candidate.item_id, (True, False))
            for candidate in playable
        }
        return [
            PlayFromQueue(row.item_id, playable, row.title, flags),
            Announce(row.title),
        ]

    def _activate_live_queue_row(
        self, row: Row, *, open_player: bool
    ) -> list[object]:
        """Enter w ZYWYM widoku kolejki: start od tego wiersza, bez wysylki.

        Kolejka jest po stronie hosta i to ona jest prawda. Nie skladamy jej
        ponownie z wierszy ekranu: ``queue.status`` nie oddaje sciezek, a nawet
        gdyby oddawal, ponowne ``queue.set`` zastapiloby zyjacy stan sesji
        migawka widoku.
        """
        state = self.session
        state.player_entry_anchor_id = row.item_id
        state.list_anchor_id = row.item_id
        state.now_playing_id = row.item_id
        state.now_playing_title = row.title
        state.pending_material_id = profile_material_id(row.item_id)
        self._set_playback_source(state, (), uses_queue=True)
        state.view = View.PLAYER if open_player else View.LIST
        return [PlayQueueAt(row.item_id, row.title), Announce(row.title)]

    def _activate_bookmark(
        self, row: Row, *, open_player: bool
    ) -> list[object]:
        """Enter na wierszu zakladki: skok albo UCZCIWA odmowa.

        Kolejnosc sprawdzen jest istotna. NAJPIERW sesja, dopiero potem mapa
        celow: obca zakladka moze miec ``item_id`` rowne lokalnemu (zmierzone w
        prawdziwym profilu), wiec odwrotna kolejnosc odtworzylaby lokalny plik
        pod wpisem TIDAL. Kanal ``files.play`` istnieje tylko dla sesji
        lokalnej i nie udajemy, ze jest inaczej.
        """
        state = self.session
        context = state.bookmark_contexts.get(row.item_id)
        if context is not None and not getattr(context, "can_play_locally", False):
            # Zostajemy na LISCIE i na TYM wierszu -- odmowa nie moze zabrac
            # uzytkownikowi miejsca, w ktorym stoi.
            session_name = getattr(context, "session_name", "") or "zdalnej"
            return [
                Announce(
                    f"Ta zakładka należy do sesji {session_name}. "
                    # NIE "ten program odtwarza tylko pliki lokalne" -- to bylo
                    # nieprawda o programie: Radio internetowe dziala i gra
                    # strumienie zdalne. Ograniczenie dotyczy WYLACZNIE
                    # odtwarzania ZAKLADEK z obcych sesji (Spotify, TIDAL,
                    # podcasty): nie mamy dla nich ``files.play``. Nazwa sesji
                    # zostaje w zdaniu pierwszym, zeby bylo wiadomo czyja.
                    "Odtwarzanie zakładek z tej sesji nie jest jeszcze dostępne."
                )
            ]

        target = state.bookmark_targets.get(row.item_id)
        if target is None:
            # Zakladki NIE maja filtra ActiveLocalItems, wiec moga wskazywac
            # material, ktorego biezacy katalog nie zna. Nie udajemy, ze
            # gramy -- i nie wchodzimy do odtwarzacza.
            return [Announce("Nie znajduję pliku tej zakładki")]

        path, position_seconds, title = target
        state.player_entry_anchor_id = row.item_id
        state.list_anchor_id = row.item_id
        state.now_playing_id = f"file:{path}"
        state.now_playing_title = title
        # Po skoku biezacym materialem jest PLIK, nie zapis zakladki. Id
        # bierzemy z kontekstu danych, bo ``file:<path>`` nim nie jest.
        state.pending_material_id = (
            profile_material_id(getattr(context, "item_id", "")) if context else ""
        )
        # Zakladka uruchamia jeden konkretny material. Nie podpinamy pod nia
        # przypadkowo listy zakladek (jej wiersze nie sa plikami) ani kolejki.
        self._set_playback_source(state, ())
        state.view = View.PLAYER if open_player else View.LIST
        return [
            PlayTrack(path, f"file:{path}", title, position_seconds=position_seconds),
            # Komunikat mowi, ze to SKOK do zapisanej pozycji, a nie
            # zwykly start od zera.
            Announce(f"{title}, od zakładki"),
        ]

    def activate_radio_preset(
        self,
        target: Row,
        sequence: tuple[Row, ...],
        *,
        open_player: bool,
    ) -> list[object]:
        """Uruchom preset Radia, pozostawiajac liste albo pokazujac odtwarzacz."""
        state = self.sessions[SessionId.RADIO]
        was_player = state.view is View.PLAYER
        if not was_player:
            state.player_entry_anchor_id = state.model.selected_id
        state.list_anchor_id = target.item_id
        state.now_playing_id = target.item_id
        state.now_playing_title = target.title
        state.pending_material_id = profile_material_id(target.item_id)
        state.playback_source_rows = sequence
        state.playback_uses_queue = False
        if was_player or open_player:
            state.view = View.PLAYER
        return [
            PlayStation(target.url or "", target.item_id, target.title),
            Announce(target.title),
        ]

    def open_radio_search_result(
        self, row: Row, *, open_player: bool = True
    ) -> list[object]:
        """Play one public-directory station without saving it to the profile."""

        if self.active is not SessionId.RADIO or row.kind != "station" or not row.url:
            return [Announce("Nie można otworzyć tego wyniku radia")]
        state = self.sessions[SessionId.RADIO]
        state.player_entry_anchor_id = row.item_id
        state.list_anchor_id = row.item_id
        state.now_playing_id = row.item_id
        state.now_playing_title = row.title
        state.pending_material_id = row.item_id
        state.playback_source_rows = (row,)
        state.playback_uses_queue = False
        state.view = View.PLAYER if open_player else View.LIST
        return [
            PlayStation(row.url, row.item_id, row.title),
            Announce(row.title),
        ]

    def activate_local_preset(
        self,
        target: Row,
        *,
        open_player: bool,
    ) -> list[object]:
        """Uruchom preset pliku bez gubienia biezacego widoku Biblioteki.

        Preset moze grac w tle albo otworzyc odtwarzacz, dokladnie jak preset
        Radia. Nie budujemy z niego kolejki: to jeden jawnie przypisany plik.
        """
        if target.kind != "track" or not target.path:
            return [Announce("Ten preset nie wskazuje pliku do odtworzenia")]
        state = self.sessions[SessionId.FILES]
        was_player = state.view is View.PLAYER
        if not was_player:
            state.player_entry_anchor_id = state.model.selected_id
        state.list_anchor_id = target.item_id
        state.now_playing_id = target.item_id
        state.now_playing_title = target.title
        state.pending_material_id = profile_material_id(target.item_id)
        self._set_playback_source(state, (target,))
        if was_player or open_player:
            state.view = View.PLAYER
        return [PlayTrack(target.path, target.item_id, target.title), Announce(target.title)]

    def go_to_parent(self) -> list[object]:
        """Backspace albo Enter na "..". Wracamy i stajemy na opuszczonym folderze.

        Wiersz rodzica z ``path=None`` to KORZEN Biblioteki, nie blad. Dawniej
        warunek brzmial ``not parent_row.path`` i polykal wlasnie ten przypadek,
        wiec Backspace w zrodle folderowym mowil "To jest folder najwyzszego
        poziomu" i uzytkownik nie mial jak wrocic do listy zrodel.

        Brak wiersza rodzica NADAL znaczy prawdziwy szczyt (``C:\\`` przy
        zwyklym przegladaniu dysku pod Ctrl+O) -- wtedy komunikat zostaje.
        """
        state = self.session
        if state.session_id is SessionId.PODCASTS:
            if state.library_view in (
                LibraryView.PODCAST_EPISODES,
                LibraryView.PODCAST_PLAYLIST_CONTENTS,
                LibraryView.YOUTUBE_BROADCASTS,
                LibraryView.YOUTUBE_BROADCASTS_AGGREGATE,
            ):
                return self._leave_library_view()
            return []
        if state.session_id is SessionId.RADIOARCHIWISTA:
            return self._leave_radioarchiwista_view()
        if state.session_id is SessionId.TIDAL:
            if state.tidal_history:
                previous = state.tidal_history.pop()
                state.library_view = previous.view
                state.tidal_heading = previous.heading
                state.library_return_view = previous.heading
                state.tidal_context = previous.context
                state.model.replace(
                    list(previous.rows), preferred_id=previous.selected_id
                )
                state.view = View.LIST
            # Na korzeniu kolekcji Backspace jest celowo cichy.
            return []
        if state.session_id is SessionId.SPOTIFY:
            if state.spotify_history:
                previous = state.spotify_history.pop()
                state.library_view = previous.view
                state.spotify_heading = previous.heading
                state.library_return_view = previous.heading
                state.spotify_context = previous.context
                state.model.replace(
                    list(previous.rows), preferred_id=previous.selected_id
                )
                state.view = View.LIST
            # Korzeń jednej sesji Spotify jest końcem nawigacji.
            return []
        if state.session_id is SessionId.WIIM:
            if state.library_view is LibraryView.WIIM_NETWORK_STREAMS:
                if state.wiim_streams_return_to_sources:
                    return self.open_wiim_source_library(
                        device_id=state.library_item_id,
                        return_id=state.wiim_device_return_id,
                    )
                state.library_view = None
                state.library_item_id = None
                state.model.replace(
                    list(state.wiim_device_rows),
                    preferred_id=state.wiim_device_return_id,
                )
                state.view = View.LIST
            elif state.library_view is LibraryView.WIIM_SOURCE_LIBRARY:
                state.library_view = None
                state.library_item_id = None
                state.model.replace(
                    list(state.wiim_device_rows),
                    preferred_id=state.wiim_device_return_id,
                )
                state.view = View.LIST
            return []
        if state.session_id is SessionId.SONOS:
            if state.library_view in (
                LibraryView.SONOS_FAVORITES,
                LibraryView.SONOS_PLAYLISTS,
                LibraryView.SONOS_OWN_STREAMS,
            ):
                return [OpenSonosView(
                    LibraryView.SONOS_LIBRARY,
                    household_id=state.library_item_id or "",
                    preferred_id=state.library_return_id,
                )]
            # Biblioteka i lista grup są równorzędnymi korzeniami sesji.
            return []
        # W nazwanym widoku Biblioteki nie ma wiersza rodzica. Widoki glowne
        # (Biblioteka/Wszystkie pliki, Ulubione, Playlisty i Kolejka) sa
        # ROWNORZEDNE: Escape ani Backspace nie moze przelaczac jednego z nich
        # na drugi. Zmieniaja je wylacznie ich jawne skroty Ctrl+L/U/P/Q.
        # Poziom nadrzedny istnieje dopiero wewnatrz playlisty, zakladek albo
        # jednego z osobnych podgladow aktywnosci.
        if state.library_view is not None:
            return self._leave_library_view()
        if state.session_id is SessionId.RADIO:
            # Escape/Backspace na najwyzszym poziomie niczego nie zmienia.
            # Nie oglaszamy oczywistego naglowka po kazdym przypadkowym Esc.
            return []

        parent_row = next((r for r in state.model.rows if r.kind == "parent"), None)
        if parent_row is None:
            return [Announce("To jest folder najwyzszego poziomu")]

        preferred = None
        if state.breadcrumb and state.breadcrumb[-1][0] == parent_row.path:
            _, preferred = state.breadcrumb.pop()
        elif state.folder_path:
            preferred = f"dir:{state.folder_path}"
        return [OpenFolder(parent_row.path, preferred_id=preferred)]

    # ------------------------------------------------- widoki Biblioteki

    #: Naglowki widokow, slownictwo zwyklego AMC (MainWindow.xaml.cs:65,
    #: MainWindow.xaml:426-427).
    _VIEW_HEADINGS = {
        LibraryView.ALL_FILES: "Wszystkie pliki",
        LibraryView.FAVORITES: "Ulubione",
        LibraryView.PLAYLISTS: "Playlisty",
        LibraryView.PLAYLIST_CONTENTS: "Playlista",
        LibraryView.HISTORY: "Historia odtwarzania",
        # Slowo "zapisana" jest TU KONIECZNE: to utrwalony stan profilu, nie
        # kolejka grajacego silnika. Etykieta ma uczciwie powiedziec, jaki
        # zapis czytamy.
        LibraryView.SAVED_QUEUE: "Zapisana kolejka",
        # ZYWA kolejka: slowa "zapisana" tu BYC NIE MOZE, bo to nie zapis, a
        # stan grajacego silnika. Etykiety rozdzielone celowo -- uzytkownik ma
        # po nazwie wiedziec, ktora z dwoch rzeczy czyta.
        LibraryView.LIVE_QUEUE: "Kolejka odtwarzania",
        LibraryView.ITEM_BOOKMARKS: "Zakładki",
    }

    #: Widoki aktywnosci sa PLASKIE jak ALL_FILES/FAVORITES: Backspace z nich
    #: wychodzi do Folderow, nie szuka "folderu nadrzednego".
    _ACTIVITY_VIEWS = (
        LibraryView.HISTORY,
        LibraryView.SAVED_QUEUE,
        LibraryView.LIVE_QUEUE,
        LibraryView.ITEM_BOOKMARKS,
        LibraryView.ACTIVE_RADIO_RECORDINGS,
        LibraryView.RECORDED_RADIO_FILES,
        LibraryView.DOWNLOADED_FILES,
        LibraryView.RADIO_RECORDING_SCHEDULES,
    )

    def open_item_bookmarks(self) -> list[object]:
        """Ctrl+Shift+B. Zakladki ZAZNACZONEGO pliku, nie wszystkie.

        Bez zaznaczenia nie ma czyich zakladek pokazac -- mowimy to wprost,
        zamiast otwierac pusty widok bez powodu. Id zapamietujemy, zeby powrot
        wrocil na TEN plik, a nie na pierwszy wiersz listy.
        """
        state = self.session
        row = state.model.selected_row
        if row is None:
            return [Announce("Nie ma zaznaczonego pliku, nie wiem czyich zakładek szukać")]
        if row.kind not in ("track", "station"):
            return [Announce("Zakładki dotyczą pliku, nie tego wiersza")]
        # Id wiersza w widoku zakladek to ``bookmark:<id>`` -- wchodzac z
        # TAKIEGO wiersza nie mamy Id pliku, wiec uzywamy zapamietanego.
        item_id = (
            state.library_item_id
            if row.item_id.startswith("bookmark:")
            else row.item_id
        )
        if not item_id:
            return [Announce("Nie znam identyfikatora pliku dla zakładek")]
        state.library_return_id = item_id
        return [OpenLibraryView(view=LibraryView.ITEM_BOOKMARKS, item_id=item_id)]

    def open_all_bookmarks(self) -> list[object]:
        """Ctrl+B / menu "Zakładki". ZBIORCZY widok, osobno od Ctrl+Shift+B.

        Kontekst bierzemy z SESJI (``_sessions.Current``), nie z zaznaczenia --
        tak jak ``MainWindow.xaml.cs:12530-12532``. Zaznaczony wiersz nie ma z
        tym nic wspolnego: uzytkownik moze sluchac A, stojac na B.

        Nie ma tu "braku zaznaczenia" jako bledu: pusty kontekst jest legalnym
        stanem (nic nie gra) i oznacza tylko tyle, ze zaden wpis nie jest
        biezacy. Widok otwiera sie zawsze.
        """
        state = self.session
        current = self.sessions[self.active]
        current_material = current.current_material_id
        # Zapamietujemy miejsce powrotu z TEJ sesji listy, zeby Backspace z
        # widoku zakladek wrocil na wiersz, z ktorego przyszlismy.
        selected = state.model.selected_row
        if selected is not None and not selected.item_id.startswith("bookmark:"):
            state.library_return_id = selected.item_id
        return [
            OpenLibraryView(
                view=LibraryView.ALL_BOOKMARKS,
                # Sesja PLIKOW tez ma Id; pusty material = nic nie gra.
                current_session_id=(LOCAL_SESSION if current_material else ""),
                current_item_id=current_material,
            )
        ]

    def note_playback_started(self, session_id: SessionId = SessionId.FILES) -> None:
        """Host POTWIERDZIL start. Dopiero teraz material jest biezacy.

        Rozdzielone od ``activate_selected``, bo samo wyslanie ``files.play``
        niczego nie dowodzi -- start moze sie nie udac.

        Id z hosta tu NIE wchodzi, i to jest celowe: ``playback.started``
        oddaje ``id`` tak, jak je dostal, wiec przy wejsciu z folderu jest to
        ``file:<path>``, a przy skoku zakladki ``file:<path>`` rowniez. Zadne
        z nich nie jest profilowym Id, ktorego potrzebuje ``GetForDisplay``.
        Tozsamosc znamy po swojej stronie (``pending_material_id``); z hosta
        bierzemy tylko FAKT udanego startu.
        """
        state = self.sessions[session_id]
        if not state.pending_material_id:
            # Host potrafi zwrocic ``file:<path>``, ktore profilowym Id nie
            # jest. Bez kandydata nie ma z czego zlozyc tozsamosci -- i lepiej
            # nie miec kontekstu niz miec zmyslony.
            state.current_material_id = ""
            return
        state.current_material_id = state.pending_material_id
        state.pending_material_id = ""

    def note_tidal_playback_started(
        self, item_id: str, title: str, *, uses_external_queue: bool = False
    ) -> None:
        """Commit external TIDAL state only after its controller confirms play."""
        if not item_id:
            return
        state = self.sessions[SessionId.TIDAL]
        state.now_playing_id = item_id
        state.now_playing_title = title
        state.current_material_id = item_id
        state.pending_material_id = ""
        state.list_anchor_id = item_id
        state.model.select_id(item_id)
        if uses_external_queue:
            # Ctrl+Enter oddaje TIDALowi caly album/playliste. AMC nie zna
            # wtedy kolejnosci utworow, wiec Page Up/Down musi isc do kolejki
            # oryginalnego odtwarzacza, nie do starej listy zrodlowej AMC.
            state.playback_source_rows = ()
            state.playback_uses_queue = True

    def note_spotify_playback_started(
        self, item_id: str, title: str, *, preserve_navigation: bool = False
    ) -> Row | None:
        """Commit Spotify state only after Librespot confirms audible playback."""

        if not item_id:
            return None
        state = self.sessions[SessionId.SPOTIFY]
        confirmed_row = next(
            (
                row for row in state.playback_source_rows
                if row.item_id == item_id and row.kind in ("track", "episode")
            ),
            None,
        )
        state.now_playing_id = item_id
        state.now_playing_title = title
        state.current_material_id = item_id
        state.pending_material_id = ""
        if not preserve_navigation:
            state.list_anchor_id = item_id
            state.model.select_id(item_id)
        return confirmed_row

    def prepare_spotify_preset_playback(
        self,
        row: Row,
        *,
        source_rows: tuple[Row, ...] | None = None,
        announcement: str = "",
    ) -> PlaySpotifyItem | Announce:
        """Prepare a preset without moving the current list selection.

        Preset playback has its own source sequence for Page Up/Page Down, but
        unlike Enter it must not replace the visible list, anchor or focus.
        """

        intent = self._spotify_play_intent(row)
        if isinstance(intent, Announce):
            return intent
        state = self.sessions[SessionId.SPOTIFY]
        intent.previous_item_id = state.current_material_id
        intent.previous_title = state.now_playing_title
        intent.artist = row.artist_name
        intent.preserve_navigation = True
        intent.success_announcement = announcement or row.title
        state.pending_material_id = row.item_id
        self._set_playback_source(state, source_rows or (row,))
        return intent

    def restore_spotify_playback_after_failed_handoff(
        self,
        previous_id: str,
        previous_title: str,
        *,
        preserve_navigation: bool = False,
    ) -> None:
        """Keep the prior item after a rejected controller handoff."""

        state = self.sessions[SessionId.SPOTIFY]
        state.pending_material_id = ""
        state.current_material_id = previous_id
        state.now_playing_id = previous_id or None
        state.now_playing_title = previous_title
        if not preserve_navigation:
            state.view = View.LIST

    def restore_tidal_playback_after_failed_handoff(
        self, previous_id: str, previous_title: str
    ) -> None:
        """Keep the already playing external track when a new handoff fails."""
        state = self.sessions[SessionId.TIDAL]
        state.pending_material_id = ""
        state.current_material_id = previous_id
        state.now_playing_id = previous_id or None
        state.now_playing_title = previous_title

    def _remember_view_selection(self, state: SessionState) -> None:
        """Zapisz zaznaczenie BIEZACEGO widoku, zanim lista sie zmieni.

        Odpowiednik ``CaptureCurrentSessionNavigationState`` (cs:18080):
        ``SelectedItemIds[widok]``. Bez tego Ctrl+L wracalby na pierwszy wiersz.
        """
        selected = state.model.selected_id
        if not selected:
            return
        if state.library_view is None:
            klucz = LIBRARY_VIEW_FOLDERS
        elif state.library_view is LibraryView.ALL_FILES:
            klucz = LIBRARY_VIEW_ALL_FILES
        else:
            return
        state.view_selected_ids[klucz] = selected

    def return_to_library(self, saved_view: str) -> list[object]:
        """Ctrl+L. ``CommandRouter.cs:390`` -> ``ShowView("Biblioteka")``, a
        ``MainWindow.xaml.cs:701-705`` podmienia te nazwe na ZAPAMIETANY widok
        (``_state.LocalMedia.LibraryView``) -- NIE zawsze na korzen Folderow.

        Docelowe zaznaczenie bierzemy z ``view_selected_ids`` (cs:18095).
        Nazwa widoku, ktorej port nie ma, konczy sie ODMOWA z jej nazwa:
        udawanie zgodnosci byloby tu gorsze niz cisza.
        """
        if self.active is SessionId.RADIO:
            return [OpenLibraryView(view=None, target_session_id=SessionId.RADIO)]
        if saved_view not in LIBRARY_RETURN_TARGETS:
            return [
                Announce(
                    f"Zapamiętany widok Biblioteki „{saved_view}” nie jest "
                    "jeszcze przeniesiony"
                )
            ]
        state = self.sessions[SessionId.FILES]
        self._remember_view_selection(state)
        target = LIBRARY_RETURN_TARGETS[state.library_return_view or saved_view]
        if target is None:
            folder = state.library_folder_path or state.folder_path
            return [
                OpenFolder(
                    folder,
                    preferred_id=state.view_selected_ids.get(LIBRARY_VIEW_FOLDERS),
                )
            ]
        return [
            OpenLibraryView(
                view=target,
                preferred_id=state.view_selected_ids.get(LIBRARY_VIEW_ALL_FILES),
            )
        ]

    def open_library_view(
        self,
        view: LibraryView,
        *,
        playlist_id: str | None = None,
        preferred_id: str | None = None,
    ) -> list[object]:
        """Ctrl+U / Ctrl+P / Alt+2 oraz Enter na playliscie.

        Zwracamy SAMO zlecenie: listy nie przestawiamy, dopoki dane nie
        przyjda. Inaczej po bledzie odczytu czytnik czytalby widok, ktorego
        nie ma.
        """
        return [
            OpenLibraryView(view=view, playlist_id=playlist_id, preferred_id=preferred_id,
                            target_session_id=self.active)
        ]

    def apply_library_view(
        self,
        view: LibraryView,
        heading: str,
        rows: list[Row],
        *,
        preferred_id: str | None = None,
        playlist_id: str | None = None,
        order_matches_amc: bool = True,
        fallback_to_playlists: bool = False,
        item_id: str | None = None,
        bookmark_targets: dict[str, tuple[str, float, str]] | None = None,
        bookmark_contexts: dict[str, object] | None = None,
        queue_flags: dict[str, tuple[bool, bool]] | None = None,
        announce_count: bool = True,
        announce_position: bool = False,
    ) -> list[object]:
        """Skutek udanego odczytu widoku. Zawsze w sesji PLIKOW.

        ``fallback_to_playlists`` oddaje ``LibraryViewResult.fallback_view``:
        playlista zniknela, wiec AMC przestawia widok na "Playlisty"
        (``MainWindow.xaml.cs:12468``). Zamiast pokazywac pusta liste bez
        powodu, mowimy co sie stalo i zlecamy wlasciwy widok.
        """
        state = self.sessions[SessionId.FILES]
        if fallback_to_playlists:
            state.library_playlist_id = None
            return [
                Announce("Tej playlisty już nie ma, wracam do playlist"),
                OpenLibraryView(view=LibraryView.PLAYLISTS),
            ]

        # Zapamietaj, gdzie uzytkownik byl PRZED wejsciem w nazwany widok.
        # Ctrl+L (cs:701-705) wraca do zapamietanego widoku Biblioteki, a
        # ``folder_path`` ponizej musi byc wyczyszczone dla Backspace.
        self._remember_view_selection(state)
        if state.library_view in (
            LibraryView.RECORDED_RADIO_FILES, LibraryView.DOWNLOADED_FILES
        ) and state.model.selected_id is not None:
            state.view_selected_ids[state.library_view.value] = state.model.selected_id
        if state.library_view is None and state.folder_path is not None:
            state.library_folder_path = state.folder_path

        state.library_view = view
        if view is LibraryView.ALL_FILES:
            state.library_return_view = LIBRARY_VIEW_ALL_FILES
        state.library_playlist_id = playlist_id
        state.library_item_id = item_id
        # Mapa celow zakladek obowiazuje TYLKO w swoim widoku. Zostawienie jej
        # przy wejsciu w inny widok groziloby odtworzeniem pozycji ze starej
        # zakladki na niepowiazanym wierszu.
        state.bookmark_targets = dict(bookmark_targets or {})
        # To samo dotyczy kontekstow: stary kontekst na nowym widoku
        # odmawialby albo zezwalal wedle nieistniejacej juz zakladki.
        state.bookmark_contexts = dict(bookmark_contexts or {})
        # Flagi kolejki tez naleza do SWOJEGO widoku: zostawienie ich przy
        # wejsciu w Ulubione kazaloby pozniejszemu ``queue.set`` opisac
        # czlonkostwo wedle listy, ktorej uzytkownik juz nie oglada.
        state.queue_flags = dict(queue_flags or {})
        # Widok Biblioteki NIE jest folderem: zadna sciezka nie opisuje
        # "Ulubionych", a zostawienie starej mylilo by Backspace.
        state.folder_path = None
        state.breadcrumb = []
        remembered_id = (
            state.view_selected_ids.get(view.value)
            if view in (LibraryView.RECORDED_RADIO_FILES, LibraryView.DOWNLOADED_FILES)
            else state.library_return_id
        )
        state.model.replace(rows, preferred_id=preferred_id or remembered_id)
        state.view = View.LIST

        row = state.model.selected_row
        # Czytnik dostaje to, czego natywna lista NIE powie: nazwe widoku i
        # rozmiar. Tytulu wiersza tu NIE MA -- kontrolka wymawia go sama, i to
        # pelniej (kolumny + "1 z N"). Powtarzanie go wlasnymi slowami bylo tym
        # drugim odczytem tego samego elementu, ktory zglosil uzytkownik.
        # Puste widoki to wyjatek: tam kontrolka nie ma czego wymowic.
        if row is None:
            # Jednoznaczny wynik wspólny dla pustych widoków. Nazwę widoku ma
            # natywna lista, a dokładnie ten krótki komunikat idzie po jej
            # aktualizacji, więc NVDA nie składa go z systemowym „nieznane”.
            parts = ["zero elementów"]
        elif announce_position:
            parts = [
                f"{heading}, {state.model.selected_index + 1} z {len(rows)}"
            ]
        elif announce_count:
            parts = [f"{heading}, {len(rows)} {_items_word(len(rows))}"]
        else:
            parts = [heading]
        if not order_matches_amc:
            # Uczciwie, tak jak LibrarySnapshot: bez kluczy hosta kolejnosc
            # jest zastepcza i nie udajemy zgodnosci 1:1.
            parts.append("kolejność zastępcza")
        return [Announce(", ".join(parts))]

    def open_queue_view(self) -> list[object]:
        """Ctrl+Q. Pytamy host o kolejke, zamiast z gory czytac zapis.

        Samo zlecenie, bez przestawiania listy: dopoki odpowiedz nie przyjdzie,
        nie wiemy jeszcze, ktory z dwoch widokow jest prawdziwy.
        """
        return [OpenQueueView()]

    def apply_live_queue(
        self,
        rows: list[Row],
        *,
        current_id: str | None = None,
    ) -> list[object]:
        """Skutek odczytu ZYWEJ kolejki hosta (``queue.status``).

        Zaznaczenie stawiamy na BIEZACYM materiale, gdy host go podal. To nie
        jest "skakanie wyboru" z ``queue.advanced``: tam lista juz byla na
        ekranie i fokus uzytkownika nalezal do niego. Tutaj widok wlasnie sie
        otwiera, wiec pierwszy wiersz pod kursorem ma byc tym, co gra.
        """
        state = self.sessions[SessionId.FILES]
        state.library_view = LibraryView.LIVE_QUEUE
        state.library_playlist_id = None
        state.library_item_id = None
        state.bookmark_targets = {}
        state.bookmark_contexts = {}
        # Zywa kolejka NIE pochodzi z zapisu profilu, wiec nie ma dla niej mapy
        # czlonkostwa do przeniesienia. Enter w tym widoku i tak nie sklada
        # kolejki od nowa (``PlayQueueAt``).
        state.queue_flags = {}
        state.folder_path = None
        state.breadcrumb = []
        state.model.replace(rows, preferred_id=current_id)
        state.view = View.LIST

        heading = self._VIEW_HEADINGS[LibraryView.LIVE_QUEUE]
        if not rows:
            # Pusto NIE jest tu bledem: Core konsumuje odegrane pozycje, wiec po
            # ostatnim utworze kolejka jest naprawde pusta. Mowimy to wprost,
            # zamiast podstawiac zapisany porzadek i udawac, ze cos zostalo.
            return [Announce(f"{heading}, pusto")]
        return [Announce(f"{heading}, {len(rows)} {_items_word(len(rows))}")]

    def _enter_playlist(self, row: Row) -> list[object]:
        """Enter na ``Row(kind="playlist")``: NAWIGACJA, nie odtwarzanie.

        Wiersz playlisty nie ma ``path`` -- dawna sciezka kodu konczyla sie
        wiec na "Brak sciezki pliku". Id wiersza to ``"playlist:<id>"``
        (``library_views.playlist_rows``), a sam ``<id>`` idzie do danych.
        """
        state = self.session
        playlist_id = row.item_id.split(":", 1)[1] if ":" in row.item_id else row.item_id
        # Zapamietujemy, na czym stanac po Backspace -- po Id wiersza, nie po
        # numerze pozycji, bo lista moze sie w miedzyczasie przeladowac.
        state.library_return_id = row.item_id
        if state.session_id is SessionId.PODCASTS:
            return [OpenPodcastPlaylistView(
                playlist_id=playlist_id,
                playlist_title=row.title,
            )]
        return [
            OpenLibraryView(
                view=LibraryView.PLAYLIST_CONTENTS,
                playlist_id=playlist_id,
                target_session_id=state.session_id,
            )
        ]

    def _leave_library_view(self) -> list[object]:
        """Backspace/Escape w nazwanym widoku Biblioteki.

        Z zawartosci playlisty wracamy na liste playlist i stajemy na TEJ
        playliscie, z ktorej weszlismy (po Id wiersza, nie po numerze).
        Rownorzedne widoki glowne pozostaja bez zmian i bez komunikatu.
        Osobne podglady aktywnosci nadal zamykaja sie do swojej listy bazowej.
        """
        state = self.session
        if state.session_id is SessionId.RADIOARCHIWISTA:
            return self._leave_radioarchiwista_view()
        if state.session_id is SessionId.PODCASTS:
            if state.library_view is LibraryView.PODCAST_EPISODES:
                return [OpenPodcastView(preferred_id=state.library_return_id)]
            if state.library_view is LibraryView.YOUTUBE_BROADCASTS:
                return [OpenPodcastView(preferred_id=state.library_return_id)]
            if state.library_view is LibraryView.YOUTUBE_BROADCASTS_AGGREGATE:
                return [OpenPodcastView()]
            if state.library_view is LibraryView.PODCAST_PLAYLIST_CONTENTS:
                return [OpenPodcastPlaylistView(
                    preferred_id=state.library_return_id,
                )]
            return []
        if state.library_view in (
            LibraryView.ALL_FILES,
            LibraryView.FAVORITES,
            LibraryView.PLAYLISTS,
            LibraryView.SAVED_QUEUE,
            LibraryView.LIVE_QUEUE,
        ):
            # To nie sa poziomy zagniezdzone. Escape/Backspace nie oznacza tu
            # "wroc do Biblioteki", bo sama Biblioteka jest widokiem obok.
            return []
        if state.session_id is SessionId.RADIO:
            if state.library_view is LibraryView.PLAYLIST_CONTENTS:
                return [
                    OpenLibraryView(
                        view=LibraryView.PLAYLISTS,
                        preferred_id=state.library_return_id,
                        target_session_id=SessionId.RADIO,
                    )
                ]
            return [OpenLibraryView(view=None, target_session_id=SessionId.RADIO)]
        if state.library_view is LibraryView.PLAYLIST_CONTENTS:
            return [
                OpenLibraryView(
                    view=LibraryView.PLAYLISTS,
                    preferred_id=state.library_return_id,
                )
            ]
        # Z zakladek wracamy na liste PLIKOW i stajemy na tym pliku, ktorego
        # zakladki ogladalismy. Samo "Foldery" zgubiloby wybor uzytkownika.
        if state.library_view is LibraryView.ITEM_BOOKMARKS:
            target = state.library_item_id
            state.library_view = None
            state.library_item_id = None
            state.bookmark_targets = {}
            state.bookmark_contexts = {}
            state.library_return_id = None
            return [
                OpenLibraryView(
                    view=LibraryView.ALL_FILES,
                    preferred_id=target,
                )
            ]
        state.library_view = None
        state.library_playlist_id = None
        state.library_return_id = None
        state.library_item_id = None
        state.bookmark_targets = {}
        state.bookmark_contexts = {}
        return [OpenFolder(state.folder_path)]

    # -------------------------------------------------------- wynik operacji

    def apply_folder(self, path: str, rows: list[Row], preferred_id: str | None = None) -> list[object]:
        """Skutek udanego ``files.listFolder``. Wywolywane w watku GUI."""
        state = self.sessions[SessionId.FILES]
        self._remember_view_selection(state)
        state.folder_path = path
        # Wejscie w folder konczy nazwany widok Biblioteki: od tej chwili
        # Backspace znow znaczy "folder nadrzedny".
        state.library_view = None
        # ...a Ctrl+L z nazwanego widoku ma wrocic TUTAJ.
        state.library_folder_path = path
        state.library_return_view = LIBRARY_VIEW_FOLDERS
        state.library_playlist_id = None
        state.library_return_id = None
        state.library_item_id = None
        state.bookmark_targets = {}
        state.bookmark_contexts = {}
        state.model.replace(rows, preferred_id=preferred_id)
        # Plik z ``play_local_file`` gra już, a teraz znamy jego folder. Page
        # Up i Page Down mają chodzić po utworach folderu, jak po Enter. Tylko
        # raz i tylko dla tego pliku: Ctrl+L i odświeżenia Biblioteki też tu
        # wracają z grającym wierszem, a źródła z Enter, presetu czy playlisty
        # nie wolno podmieniać. Pliku spoza listy folderu (np. .m3u) też nie.
        opened, self._opened_file_id = self._opened_file_id, None
        tracks = tuple(
            candidate for candidate in state.model.rows
            if candidate.kind == "track" and candidate.path
        )
        if (
            opened is not None
            and opened == preferred_id == state.now_playing_id
            and any(candidate.item_id == opened for candidate in tracks)
        ):
            self._set_playback_source(state, tracks)
        state.view = View.LIST
        row = state.model.selected_row
        name = path.rstrip("/\\").rsplit("/", 1)[-1].rsplit("\\", 1)[-1] or path
        count = sum(1 for r in state.model.rows if r.kind != "parent")
        # Tytul wiersza wymawia natywna lista (gest C00d pokazal go az 3 razy
        # po jednym Ctrl+O). Mowimy tylko to, czego kontrolka nie powie.
        suffix = "" if row is not None else ", pusty"
        return [Announce(f"{name}, {count} elementow{suffix}")]

    def apply_radio_view(
        self, view: LibraryView | None, heading: str, rows: list[Row],
        *, preferred_id: str | None = None,
        order_matches_amc: bool = True,
        playlist_id: str | None = None,
        fallback_to_playlists: bool = False,
        announce_position: bool = False,
    ) -> list[object]:
        """Odpowiedz loadera Radia nie rusza listy ani wyboru Plikow."""
        if view not in (
            None,
            LibraryView.FAVORITES,
            LibraryView.HISTORY,
            LibraryView.PLAYLISTS,
            LibraryView.PLAYLIST_CONTENTS,
            LibraryView.ACTIVE_RADIO_RECORDINGS,
            LibraryView.RADIO_RECORDING_SCHEDULES,
        ):
            raise ValueError("Nieobslugiwany widok Radia")
        state = self.sessions[SessionId.RADIO]
        if fallback_to_playlists:
            state.library_playlist_id = None
            return [
                Announce("Tej playlisty już nie ma, wracam do playlist"),
                OpenLibraryView(
                    view=LibraryView.PLAYLISTS,
                    target_session_id=SessionId.RADIO,
                ),
            ]
        old_key = (
            state.library_playlist_id
            if state.library_view is LibraryView.PLAYLIST_CONTENTS
            and state.library_playlist_id
            else state.library_view.value if state.library_view else "library"
        )
        if state.model.selected_id is not None:
            state.view_selected_ids[old_key] = state.model.selected_id
        key = (
            playlist_id
            if view is LibraryView.PLAYLIST_CONTENTS and playlist_id
            else view.value if view else "library"
        )
        state.library_view = view
        state.library_playlist_id = (
            playlist_id if view is LibraryView.PLAYLIST_CONTENTS else None
        )
        state.model.replace(rows, preferred_id=preferred_id or state.view_selected_ids.get(key))
        state.view = View.LIST
        # Nazwe wiersza czyta natywna lista, nie powtarzamy jej w komunikacie.
        if rows:
            message = (
                f"{heading}, {state.model.selected_index + 1} z {len(rows)}"
                if announce_position
                else heading
            )
        else:
            # Nazwę widoku („Playlista”, „Nagrywane” itd.) ma sama kontrolka.
            # Pusty wynik ma być dokładnie tak krótki, jak zgłosił użytkownik.
            message = "zero elementów"
        if not order_matches_amc:
            # Ta sama formula, co w ``apply_library_view``: zastepcza kolejnosc
            # jest NAZWANA, a nie przemilczana.
            message = f"{message}, kolejność zastępcza"
        return [Announce(message)]

    def apply_stations(self, rows: list[Row], preferred_id: str | None = None) -> list[object]:
        state = self.sessions[SessionId.RADIO]
        state.model.replace(rows, preferred_id=preferred_id)
        row = state.model.selected_row
        suffix = f", {row.title}" if row is not None else ", lista pusta"
        return [Announce(f"Stacje: {len(rows)}{suffix}")]

    def apply_podcast_library(
        self,
        rows: list[Row],
        preferred_id: str | None = None,
        *,
        sort_label: str = "",
        order_matches_amc: bool = True,
    ) -> list[object]:
        """Apply the shared podcast/YouTube source list without touching files."""
        state = self.sessions[SessionId.PODCASTS]
        if state.library_view is LibraryView.PODCAST_EPISODES and state.model.selected_id:
            state.view_selected_ids[state.library_playlist_id or ""] = state.model.selected_id
        state.library_view = LibraryView.PODCAST_LIBRARY
        state.library_playlist_id = None
        state.library_return_view = None
        state.model.replace(rows, preferred_id=preferred_id or state.library_return_id)
        state.view = View.LIST
        state.library_return_id = state.model.selected_id
        if sort_label and state.model.selected_row is not None:
            selected = state.model.selected_row
            message = (
                f"{sort_label}. {selected.title}, "
                f"{state.model.selected_index + 1} z {len(rows)}"
            )
        else:
            message = (
                f"Podcasty i YouTube, {len(rows)} {_items_word(len(rows))}"
                if rows else "Podcasty i YouTube, pusto"
            )
        if not order_matches_amc:
            message = f"{message}, kolejność zastępcza"
        return [Announce(message)]

    def apply_podcast_episodes(
        self,
        subscription_id: str,
        subscription_title: str,
        rows: list[Row],
        *,
        preferred_id: str | None = None,
        total_count: int | None = None,
    ) -> list[object]:
        """Apply one podcast/channel while remembering the exact parent row."""
        state = self.sessions[SessionId.PODCASTS]
        if state.library_view is LibraryView.PODCAST_LIBRARY:
            state.library_return_id = state.model.selected_id or subscription_id
        state.library_view = LibraryView.PODCAST_EPISODES
        state.library_playlist_id = subscription_id
        # This field is normally a saved library view name.  In the podcast
        # session it holds only the intentional parent title for announcements.
        state.library_return_view = subscription_title
        remembered = state.view_selected_ids.get(subscription_id)
        state.model.replace(rows, preferred_id=preferred_id or remembered)
        state.view = View.LIST
        count = sum(1 for row in rows if row.kind == "episode")
        if count and total_count is not None and total_count > count:
            message = f"{subscription_title}, wczytano {count} z {total_count} odcinków"
        else:
            message = (
                f"{subscription_title}, {count} {_items_word(count)}"
                if count else f"{subscription_title}, pusto"
            )
        return [Announce(message)]

    def open_youtube_broadcasts(
        self,
        *,
        force_refresh: bool = False,
    ) -> list[object]:
        """Open or refresh broadcasts without exposing a channel identifier."""

        state = self.session
        if state.session_id is not SessionId.PODCASTS:
            return [Announce("Transmisje YouTube są dostępne w sesji Podcasty i YouTube")]
        if state.library_view is LibraryView.YOUTUBE_BROADCASTS:
            if not state.library_playlist_id:
                return [Announce("Nie wiadomo, dla którego kanału odświeżyć transmisje")]
            return [OpenYouTubeBroadcasts(
                state.library_playlist_id,
                state.library_return_view or "Kanał YouTube",
                preferred_id=state.model.selected_id,
                force_refresh=force_refresh,
            )]
        row = state.model.selected_row
        if row is None or row.kind != "podcast" or row.service_kind != "youtubeChannel":
            return [Announce("Wybierz kanał YouTube z Biblioteki")]
        state.library_return_id = row.item_id
        return [OpenYouTubeBroadcasts(
            row.item_id,
            row.title,
            force_refresh=force_refresh,
        )]

    def apply_radioarchiwista_rows(
        self,
        view: "LibraryView",
        rows: list[Row],
        *,
        slug: str = "",
        year: int | None = None,
        month: int | None = None,
        day: int | None = None,
        preferred_id: str | None = None,
        announcement: str = "",
    ) -> list[object]:
        """Pokaż jeden poziom katalogu nagrań Radio Archiwisty.

        Stan sesji pamięta, który shard jest widoczny, żeby F5 i „wczytaj
        więcej" znały kontekst bez składania go z tekstu wierszy.
        """

        state = self.sessions[SessionId.RADIOARCHIWISTA]
        self._remember_view_selection(state)
        state.library_view = view
        state.library_playlist_id = None
        state.radioarchiwista_slug = slug
        state.radioarchiwista_year = year
        state.radioarchiwista_month = month
        state.radioarchiwista_day = day
        state.model.replace(rows, preferred_id=preferred_id)
        state.view = View.LIST
        state.library_return_id = preferred_id or state.model.selected_id
        count = sum(1 for row in rows if row.kind != "loadMore")
        heading = radioarchiwista_heading(view)
        if announcement:
            message = announcement
        elif count:
            phrase = (
                i18n.recordings_phrase(count)
                if view is LibraryView.RADIOARCHIWISTA_HOURS
                else i18n.items_phrase(count)
            )
            message = i18n.tr("rarch.view.count", heading=heading, phrase=phrase)
        else:
            message = i18n.tr("rarch.view.empty", heading=heading)
        return [Announce(message)]

    def apply_radioarchiwista_message(self, message: str) -> list[object]:
        """Uczciwa odmowa: błąd sieci nie zmienia widoku ani zaznaczenia."""

        return [Announce(message)]

    def _activate_radioarchiwista_row(
        self, row: Row, *, open_player_on_activation: bool = True
    ) -> list[object]:
        state = self.session
        if row.activation_message:
            return [Announce(row.activation_message)]
        if row.kind == "recording":
            if not row.url:
                return [Announce(i18n.tr("rarch.recording.noAudio"))]
            state.player_entry_anchor_id = row.item_id
            state.list_anchor_id = row.item_id
            state.now_playing_id = row.item_id
            state.now_playing_title = row.title
            state.pending_material_id = profile_material_id(row.item_id)
            self._set_playback_source(
                state,
                tuple(
                    candidate
                    for candidate in state.model.rows
                    if candidate.kind == "recording"
                    and not candidate.activation_message
                ),
            )
            state.radioarchiwista_queue_active = (
                state.library_view is LibraryView.RADIOARCHIWISTA_QUEUE
            )
            state.view = (
                View.PLAYER if open_player_on_activation else View.LIST
            )
            return [
                PlayMedia(
                    row.url,
                    row.item_id,
                    row.title,
                    position_seconds=row.position_seconds,
                ),
                Announce(row.title),
            ]
        if row.service_kind == "rarchLatest":
            self._push_radioarchiwista_history(state)
            return [OpenRadioarchiwistaLatest()]
        if row.service_kind == "rarchStation" and row.service_id:
            self._push_radioarchiwista_history(state)
            state.library_return_id = row.item_id
            return [OpenRadioarchiwistaLevel(
                LibraryView.RADIOARCHIWISTA_YEARS,
                row.service_id,
            )]
        level = _radioarchiwista_period(row)
        if level is not None:
            slug, year, month, day, target_view = level
            self._push_radioarchiwista_history(state)
            state.library_return_id = row.item_id
            return [OpenRadioarchiwistaLevel(
                target_view,
                slug,
                year=year,
                month=month,
                day=day,
            )]
        if row.kind == "loadMore":
            view = state.library_view
            if view is LibraryView.RADIOARCHIWISTA_HOURS:
                return [OpenRadioarchiwistaLevel(
                    view,
                    state.radioarchiwista_slug,
                    year=state.radioarchiwista_year,
                    month=state.radioarchiwista_month,
                    day=state.radioarchiwista_day,
                    load_more=True,
                )]
            return [Announce(i18n.tr("rarch.loadMore.unknown"))]
        return [Announce(i18n.tr("rarch.open.failed"))]

    def _push_radioarchiwista_history(self, state: SessionState) -> None:
        state.radioarchiwista_history.append(
            (
                state.library_view,
                state.radioarchiwista_slug,
                state.radioarchiwista_year,
                state.radioarchiwista_month,
                state.radioarchiwista_day,
                state.model.selected_id,
            )
        )

    def _leave_radioarchiwista_view(self) -> list[object]:
        """Backspace/Escape: poziom wyżej, na opuszczonym wierszu."""

        state = self.session
        if state.library_view is LibraryView.RADIOARCHIWISTA_FAVORITES:
            return [OpenRadioarchiwistaStations()]
        if state.library_view is LibraryView.RADIOARCHIWISTA_QUEUE:
            return [OpenRadioarchiwistaStations()]
        if not state.radioarchiwista_history:
            return []
        view, slug, year, month, day, return_id = state.radioarchiwista_history.pop()
        if view is LibraryView.RADIOARCHIWISTA_STATIONS or view is None:
            return [OpenRadioarchiwistaStations(preferred_id=return_id)]
        return [OpenRadioarchiwistaLevel(
            view,
            slug,
            year=year,
            month=month,
            day=day,
            preferred_id=return_id,
        )]

    def apply_youtube_broadcasts(
        self,
        subscription_id: str,
        subscription_title: str,
        rows: list[Row],
        *,
        preferred_id: str | None = None,
    ) -> list[object]:
        """Show a dedicated live/upcoming list and preserve Backspace target."""

        state = self.sessions[SessionId.PODCASTS]
        if state.library_view is LibraryView.PODCAST_LIBRARY:
            state.library_return_id = state.model.selected_id or subscription_id
        state.library_view = LibraryView.YOUTUBE_BROADCASTS
        state.library_playlist_id = subscription_id
        state.library_return_view = subscription_title
        remembered = state.view_selected_ids.get(f"youtube-live:{subscription_id}")
        state.model.replace(rows, preferred_id=preferred_id or remembered)
        state.view = View.LIST
        live_count = sum(1 for row in rows if row.service_kind == "youtubeLive")
        upcoming_count = sum(
            1 for row in rows if row.service_kind == "youtubeUpcoming"
        )
        if not rows:
            message = f"Transmisje kanału {subscription_title}, brak trwających i zaplanowanych transmisji"
        else:
            message = (
                f"Transmisje kanału {subscription_title}, "
                f"na żywo {live_count}, zaplanowane {upcoming_count}"
            )
        return [Announce(message)]

    def apply_youtube_broadcasts_aggregate(
        self,
        rows: list[Row],
        *,
        checked_channels: int,
        failed_channels: int = 0,
        preferred_id: str | None = None,
    ) -> list[object]:
        """Pokaż globalny podgląd bez ujawniania identyfikatorów kanałów."""

        state = self.sessions[SessionId.PODCASTS]
        state.library_view = LibraryView.YOUTUBE_BROADCASTS_AGGREGATE
        state.library_playlist_id = None
        state.library_return_view = "Transmisje wybranych kanałów YouTube"
        remembered = state.view_selected_ids.get("youtube-live:aggregate")
        state.model.replace(rows, preferred_id=preferred_id or remembered)
        state.view = View.LIST
        live_count = sum(1 for row in rows if row.service_kind == "youtubeLive")
        upcoming_count = sum(
            1 for row in rows if row.service_kind == "youtubeUpcoming"
        )
        if rows:
            message = (
                "Transmisje wybranych kanałów YouTube, "
                f"na żywo {live_count}, zaplanowane {upcoming_count}"
            )
        else:
            message = (
                "Transmisje wybranych kanałów YouTube, "
                "brak trwających i zaplanowanych transmisji"
            )
        if failed_channels:
            message += (
                f". Sprawdzono {checked_channels} kanałów; "
                f"nie udało się sprawdzić {failed_channels}"
            )
        return [Announce(message)]

    def apply_podcast_aggregate(
        self,
        view: LibraryView,
        heading: str,
        rows: list[Row],
        *,
        preferred_id: str | None = None,
        order_matches_amc: bool = True,
    ) -> list[object]:
        """Apply an aggregate episode view with a stable, user-facing name."""
        if view not in (
            LibraryView.PODCAST_FAVORITES,
            LibraryView.PODCAST_HISTORY,
            LibraryView.PODCAST_QUEUE,
            LibraryView.PODCAST_INBOX,
            LibraryView.PODCAST_DOWNLOADS,
        ):
            raise ValueError("To nie jest zbiorczy widok podcastów")
        state = self.sessions[SessionId.PODCASTS]
        previous = state.library_view
        if previous is not None and state.model.selected_id:
            state.view_selected_ids[previous.value] = state.model.selected_id
        remembered = state.view_selected_ids.get(view.value)
        state.library_view = view
        state.library_playlist_id = None
        state.library_return_view = heading
        state.model.replace(rows, preferred_id=preferred_id or remembered)
        state.view = View.LIST
        count = sum(1 for row in rows if row.kind in ("podcast", "episode"))
        if view is LibraryView.PODCAST_FAVORITES and count == 0:
            message = "Ulubione, brak ulubionych podcastów i odcinków"
        elif view is LibraryView.PODCAST_HISTORY and count == 0:
            message = "Historia odtwarzania, brak odtworzonych odcinków"
        elif view is LibraryView.PODCAST_QUEUE and count == 0:
            message = "Kolejka, zero elementów"
        elif view is LibraryView.PODCAST_INBOX and count == 0:
            message = "Nowe odcinki i materiały, brak nowych materiałów"
        elif view is LibraryView.PODCAST_DOWNLOADS and count == 0:
            message = "Pobrane, brak pobranych odcinków"
        else:
            message = f"{heading}, {count} {_items_word(count)}"
        if not order_matches_amc:
            message = f"{message}, kolejność zastępcza"
        return [Announce(message)]

    def apply_podcast_playlists(
        self,
        rows: list[Row],
        *,
        preferred_id: str | None = None,
    ) -> list[object]:
        """Apply the top-level, session-specific podcast playlist catalogue."""

        state = self.sessions[SessionId.PODCASTS]
        state.library_view = LibraryView.PODCAST_PLAYLISTS
        state.library_playlist_id = None
        state.library_return_view = "Playlisty"
        state.model.replace(
            rows,
            preferred_id=preferred_id or state.library_return_id,
        )
        state.view = View.LIST
        state.library_return_id = state.model.selected_id
        return [Announce(
            f"Playlisty, {len(rows)} {_items_word(len(rows))}"
            if rows else "Playlisty, zero elementów"
        )]

    def apply_podcast_playlist_contents(
        self,
        playlist_id: str,
        playlist_title: str,
        rows: list[Row],
        *,
        preferred_id: str | None = None,
    ) -> list[object]:
        """Apply one playlist while preserving its manual order and parent."""

        state = self.sessions[SessionId.PODCASTS]
        if state.library_view is LibraryView.PODCAST_PLAYLISTS:
            state.library_return_id = state.model.selected_id or (
                f"podcast-playlist:{playlist_id}"
            )
        remembered = state.view_selected_ids.get(playlist_id)
        state.library_view = LibraryView.PODCAST_PLAYLIST_CONTENTS
        state.library_playlist_id = playlist_id
        state.library_return_view = playlist_title
        state.model.replace(rows, preferred_id=preferred_id or remembered)
        state.view = View.LIST
        count = sum(1 for row in rows if row.kind == "episode")
        return [Announce(
            f"Playlista — {playlist_title}, {count} {_items_word(count)}"
            if count else f"Playlista — {playlist_title}, zero elementów"
        )]

    # ------------------------------------------------------------- Spotify

    def open_spotify_view(
        self, *, preferred_id: str | None = None
    ) -> list[object]:
        """Request the read-only root of the single Spotify session."""

        return [OpenSpotifyView(preferred_id=preferred_id)]

    def apply_spotify_view(
        self,
        heading: str,
        rows: list[Row],
        *,
        preferred_id: str | None = None,
    ) -> list[object]:
        """Apply the Spotify root without changing another session."""

        state = self.sessions[SessionId.SPOTIFY]
        remembered = state.view_selected_ids.get(LibraryView.SPOTIFY_LIBRARY.value)
        state.library_view = LibraryView.SPOTIFY_LIBRARY
        state.library_playlist_id = None
        state.library_return_view = heading
        state.spotify_heading = heading
        state.spotify_context = None
        state.spotify_history.clear()
        state.model.replace(rows, preferred_id=preferred_id or remembered)
        state.view = View.LIST
        return [Announce(
            f"{heading}, {len(rows)} {_items_word(len(rows))}"
            if rows else f"{heading}, pusto"
        )]

    def apply_spotify_history(self, rows: list[Row]) -> list[object]:
        """Show AMC's confirmed Spotify plays as a transient named view."""

        state = self.sessions[SessionId.SPOTIFY]
        remembered = state.view_selected_ids.get(
            LibraryView.SPOTIFY_HISTORY.value
        )
        state.library_view = LibraryView.SPOTIFY_HISTORY
        state.library_playlist_id = None
        state.library_return_view = "Spotify — Historia odtwarzania"
        state.spotify_heading = "Spotify — Historia odtwarzania"
        state.spotify_context = None
        state.spotify_history.clear()
        state.model.replace(rows, preferred_id=remembered)
        state.view = View.LIST
        count = len(rows)
        return [Announce(
            f"Spotify — Historia odtwarzania, {count} {_items_word(count)}"
            if count
            else "Spotify — Historia odtwarzania, brak odtworzonych elementów"
        )]

    def apply_service_history(
        self, session_id: SessionId, rows: list[Row]
    ) -> list[object]:
        """Pokaż prywatną Historię bez przechodzenia do Plików lokalnych."""

        if session_id not in (
            SessionId.WIIM, SessionId.SONOS, SessionId.FRONTIER_SMART,
            SessionId.TIDAL, SessionId.RADIOARCHIWISTA,
        ):
            raise ValueError("Ta sesja nie przechowuje osobnej historii")
        state = self.sessions[session_id]
        remembered = state.view_selected_ids.get(
            LibraryView.SERVICE_HISTORY.value
        )
        state.library_view = LibraryView.SERVICE_HISTORY
        state.library_playlist_id = None
        heading = f"{self._session_name(session_id)} — Historia odtwarzania"
        state.library_return_view = heading
        state.model.replace(rows, preferred_id=remembered)
        state.view = View.LIST
        count = len(rows)
        return [Announce(
            f"{heading}, {count} {_items_word(count)}"
            if count
            else f"{heading}, brak odtworzonych elementów"
        )]

    def apply_spotify_container(
        self,
        intent: OpenSpotifyContainer,
        heading: str,
        rows: list[Row],
        *,
        preferred_id: str | None = None,
    ) -> list[object]:
        """Apply one verified result and retain the exact parent focus."""

        state = self.sessions[SessionId.SPOTIFY]
        if not intent.load_more:
            state.spotify_history.append(self._spotify_snapshot(state))
        state.library_view = LibraryView.SPOTIFY_CONTAINER
        state.library_playlist_id = None
        state.library_return_view = heading
        state.spotify_heading = heading
        state.spotify_context = replace(intent, load_more=False)
        state.model.replace(rows, preferred_id=preferred_id)
        state.view = View.LIST
        count = sum(row.kind != "loadMore" for row in rows)
        return [Announce(
            f"{heading}, {count} {_items_word(count)}"
            if count else f"{heading}, pusto"
        )]

    def apply_spotify_collection_view(
        self,
        intent: OpenSpotifyCollectionView,
        heading: str,
        rows: list[Row],
        *,
        preferred_id: str | None = None,
    ) -> list[object]:
        """Apply a named collection view and retain the root focus."""

        state = self.sessions[SessionId.SPOTIFY]
        if not intent.load_more and not intent.replace_current:
            state.spotify_history.append(self._spotify_snapshot(state))
        state.library_view = LibraryView.SPOTIFY_CONTAINER
        state.library_playlist_id = None
        state.library_return_view = heading
        state.spotify_heading = heading
        state.spotify_context = OpenSpotifyCollectionView(
            intent.item_id,
            intent.view,
            intent.title,
        )
        state.model.replace(rows, preferred_id=preferred_id)
        state.view = View.LIST
        count = sum(row.kind != "loadMore" for row in rows)
        return [Announce(
            f"{heading}, {count} {_items_word(count)}"
            if count else f"{heading}, pusto"
        )]

    @staticmethod
    def _spotify_collection_target_from_row(
        row: Row,
    ) -> OpenSpotifyCollectionView | None:
        if (
            row.kind != "folder"
            or row.service_kind != "spotifyCollection"
            or row.service_id not in {
                "liked", "albums", "artists", "playlists", "podcasts", "episodes"
            }
        ):
            return None
        return OpenSpotifyCollectionView(row.item_id, row.service_id, row.title)

    @staticmethod
    def _spotify_target_from_row(row: Row) -> OpenSpotifyContainer | None:
        if (
            not row.service_id
            or row.service_kind not in ("artist", "album", "playlist", "podcast")
            or row.service_section not in (None, "albums", "tracks")
        ):
            return None
        title = row.artist_name if row.service_section else row.title
        if not title:
            return None
        return OpenSpotifyContainer(
            item_id=row.item_id,
            service_id=row.service_id,
            title=title,
            kind=row.service_kind,
            artist=row.artist_name,
            public_uri=row.url,
            section=row.service_section,
        )

    def open_spotify_search_result(
        self,
        row: Row,
        *,
        relations: bool = False,
        open_player: bool = True,
    ) -> list[object]:
        """Open an ephemeral search row without saving it to the library."""

        if self.active is not SessionId.SPOTIFY:
            return [Announce("Wynik wyszukiwania należy do Spotify")]
        target = self._spotify_target_from_row(row)
        if relations:
            if target is not None and target.kind == "artist":
                return self._open_spotify_artist_overview(row, target)
            choices = self._spotify_relation_choices(row)
            if len(choices) == 1:
                choice = choices[0]
                if choice.open_artist_overview:
                    artist = Row(
                        item_id=choice.target.item_id,
                        title=choice.target.title,
                        kind="artist",
                        service_id=choice.target.service_id,
                        service_kind="artist",
                    )
                    return self._open_spotify_artist_overview(artist, choice.target)
                return [choice.target]
            if choices:
                return [Announce(
                    "Ten wynik prowadzi do kilku miejsc; "
                    "otwórz go i użyj prawej strzałki"
                )]
            return [Announce(
                "Z tego wyniku nie można przejść do albumu, "
                "wykonawcy ani podcastu"
            )]

        if target is not None:
            if target.kind == "artist" and target.section is None:
                return self._open_spotify_artist_overview(row, target)
            return [target]
        if row.kind in ("track", "episode"):
            state = self.sessions[SessionId.SPOTIFY]
            if row.activation_message:
                return [Announce(row.activation_message)]
            intent = self._spotify_play_intent(row)
            if isinstance(intent, Announce):
                return [intent]
            intent.previous_item_id = state.current_material_id
            intent.previous_title = state.now_playing_title
            state.player_entry_anchor_id = row.item_id
            state.list_anchor_id = row.item_id
            state.now_playing_id = row.item_id
            state.now_playing_title = row.title
            state.pending_material_id = row.item_id
            self._set_playback_source(state, (row,))
            state.view = View.PLAYER if open_player else View.LIST
            return [intent]
        return [Announce("Nie można otworzyć tego wyniku Spotify")]

    def activate_spotify_search_relation(
        self,
        row: Row,
        choice: SpotifyRelationChoice,
    ) -> list[object]:
        """Open a relation chosen inside the still-open search window."""

        if self.active is not SessionId.SPOTIFY:
            return [Announce("Wynik wyszukiwania należy do Spotify")]
        if choice not in self._spotify_relation_choices(row):
            return [Announce("Wybrane miejsce nie jest już dostępne w Spotify")]
        if choice.open_artist_overview:
            target = choice.target
            artist = Row(
                item_id=target.item_id,
                title=target.title,
                kind="artist",
                service_id=target.service_id,
                service_kind="artist",
            )
            return self._open_spotify_artist_overview(artist, target)
        return [choice.target]

    @staticmethod
    def _spotify_play_intent(row: Row) -> PlaySpotifyItem | Announce:
        if row.kind not in ("track", "episode") or not row.service_id:
            return Announce("Ten materiał nie ma danych potrzebnych do odtworzenia")
        return PlaySpotifyItem(
            item_id=row.item_id,
            title=row.title,
            service_id=row.service_id,
            kind=row.kind,
        )

    @staticmethod
    def _spotify_relation_choices(row: Row) -> tuple[SpotifyRelationChoice, ...]:
        choices: list[SpotifyRelationChoice] = []
        if row.kind == "track" and row.related_album_service_id:
            choices.append(SpotifyRelationChoice(
                "Przejdź do albumu",
                OpenSpotifyContainer(
                    item_id=f"{row.item_id}:related-album",
                    service_id=row.related_album_service_id,
                    title=row.related_album_title or "Album",
                    kind="album",
                    artist=row.artist_name,
                ),
            ))
        if row.kind == "artist" and row.service_id:
            for section, label in (
                ("albums", "Pokaż albumy wykonawcy"),
                ("tracks", "Pokaż utwory wykonawcy"),
            ):
                choices.append(SpotifyRelationChoice(
                    label,
                    OpenSpotifyContainer(
                        item_id=row.item_id,
                        service_id=row.service_id,
                        title=row.title,
                        kind="artist",
                        section=section,
                    ),
                ))
        if (
            row.kind in ("track", "album")
            and row.related_artist_service_id
        ):
            choices.append(SpotifyRelationChoice(
                "Przejdź do wykonawcy",
                OpenSpotifyContainer(
                    item_id=f"{row.item_id}:related-artist",
                    service_id=row.related_artist_service_id,
                    title=(
                        row.related_artist_name
                        or row.artist_name
                        or "Wykonawca"
                    ),
                    kind="artist",
                ),
                open_artist_overview=True,
            ))
        if row.kind == "episode" and row.related_podcast_service_id:
            podcast_title = row.related_podcast_title.strip()
            choices.append(SpotifyRelationChoice(
                (
                    f"Przejdź do {podcast_title}"
                    if podcast_title
                    else "Przejdź do podcastu"
                ),
                OpenSpotifyContainer(
                    item_id=f"{row.item_id}:related-podcast",
                    service_id=row.related_podcast_service_id,
                    title=podcast_title or "Podcast",
                    kind="podcast",
                ),
            ))
        return tuple(choices)

    def spotify_information(self) -> list[object]:
        """Left Arrow: speak one safe description and keep focus unchanged."""

        state = self.session
        if state.session_id is not SessionId.SPOTIFY:
            return [Announce("Informacje Spotify są dostępne tylko w Spotify")]
        row = state.model.selected_row
        if row is None:
            return [Announce("Lista Spotify jest pusta")]
        from .spotify_source import information_text

        return [Announce(information_text(row))]

    def spotify_relations(self) -> list[object]:
        """Right Arrow: expose only relationships proved by the row DTO."""

        state = self.session
        if state.session_id is not SessionId.SPOTIFY:
            return [Announce(
                "Przechodzenie do albumu, wykonawcy lub podcastu "
                "jest dostępne tylko w Spotify"
            )]
        row = state.model.selected_row
        if row is None:
            return [Announce("Lista Spotify jest pusta")]
        choices = self._spotify_relation_choices(row)
        if not choices:
            return [Announce(
                "Spotify nie podał miejsca, do którego można przejść z tego elementu"
            )]
        return [ShowSpotifyRelations("Przejdź do", choices)]

    def activate_spotify_relation(
        self, choice: SpotifyRelationChoice
    ) -> list[object]:
        """Run a relation only while it still belongs to the focused row."""

        state = self.session
        row = state.model.selected_row
        if (
            state.session_id is not SessionId.SPOTIFY
            or row is None
            or choice not in self._spotify_relation_choices(row)
        ):
            return [Announce("Wybrane miejsce nie jest już dostępne w Spotify")]
        return self.activate_spotify_search_relation(row, choice)

    @staticmethod
    def _spotify_snapshot(state: SessionState) -> SpotifyNavigationSnapshot:
        return SpotifyNavigationSnapshot(
            view=state.library_view or LibraryView.SPOTIFY_LIBRARY,
            heading=state.spotify_heading,
            rows=tuple(state.model.rows),
            selected_id=state.model.selected_id,
            context=state.spotify_context,
        )

    def _open_spotify_artist_overview(
        self, row: Row, target: OpenSpotifyContainer
    ) -> list[object]:
        from .spotify_source import artist_overview_rows

        state = self.sessions[SessionId.SPOTIFY]
        rows = list(artist_overview_rows(row))
        if not rows:
            return [Announce("Nie można otworzyć tego wykonawcy Spotify")]
        state.spotify_history.append(self._spotify_snapshot(state))
        state.library_view = LibraryView.SPOTIFY_CONTAINER
        state.spotify_heading = f"Wykonawca, {row.title}"
        state.library_return_view = state.spotify_heading
        state.spotify_context = target
        state.model.replace(rows)
        state.view = View.LIST
        return [Announce(
            f"{state.spotify_heading}, {len(rows)} {_items_word(len(rows))}"
        )]

    def close_spotify_preview(self) -> list[object]:
        """Escape: close player/container and restore the exact prior focus."""

        if self.active is not SessionId.SPOTIFY:
            return []
        if self.session.view is View.PLAYER:
            return self.back_to_list()
        return self.go_to_parent()

    # --------------------------------------------------------------- TIDAL

    def open_tidal_view(
        self,
        view: LibraryView,
        *,
        preferred_id: str | None = None,
    ) -> list[object]:
        """Request one persisted TIDAL collection view."""
        if view not in (
            LibraryView.TIDAL_LIBRARY,
            LibraryView.TIDAL_FAVORITES,
            LibraryView.TIDAL_PLAYLISTS,
        ):
            return [Announce("Ten widok nie jest dostępny w TIDAL")]
        return [OpenTidalView(view=view, preferred_id=preferred_id)]

    def apply_tidal_view(
        self,
        view: LibraryView,
        heading: str,
        rows: list[Row],
        *,
        preferred_id: str | None = None,
        order_matches_amc: bool = True,
    ) -> list[object]:
        """Apply TIDAL rows without touching another session or exposing IDs."""
        if view not in (
            LibraryView.TIDAL_LIBRARY,
            LibraryView.TIDAL_FAVORITES,
            LibraryView.TIDAL_PLAYLISTS,
        ):
            raise ValueError("To nie jest widok TIDAL")
        state = self.sessions[SessionId.TIDAL]
        previous = state.library_view
        if previous in (
            LibraryView.TIDAL_LIBRARY,
            LibraryView.TIDAL_FAVORITES,
            LibraryView.TIDAL_PLAYLISTS,
        ) and state.model.selected_id:
            state.view_selected_ids[previous.value] = state.model.selected_id
        remembered = state.view_selected_ids.get(view.value)
        state.library_view = view
        state.library_playlist_id = None
        state.library_return_view = heading
        state.tidal_heading = heading
        state.tidal_context = None
        state.tidal_history.clear()
        state.model.replace(rows, preferred_id=preferred_id or remembered)
        state.view = View.LIST
        message = (
            f"{heading}, {len(rows)} {_items_word(len(rows))}"
            if rows else f"{heading}, pusto"
        )
        if not order_matches_amc:
            message = f"{message}, kolejność zastępcza"
        return [Announce(message)]

    def apply_tidal_container(
        self,
        intent: OpenTidalContainer,
        heading: str,
        rows: list[Row],
        *,
        preferred_id: str | None = None,
    ) -> list[object]:
        """Apply one verified online result and retain an exact Backspace path."""
        state = self.sessions[SessionId.TIDAL]
        state.tidal_history.append(self._tidal_snapshot(state))
        state.library_view = LibraryView.TIDAL_CONTAINER
        state.library_playlist_id = None
        state.library_return_view = heading
        state.tidal_heading = heading
        state.tidal_context = intent
        state.model.replace(rows, preferred_id=preferred_id)
        state.view = View.LIST
        message = (
            f"{heading}, {len(rows)} {_items_word(len(rows))}"
            if rows else f"{heading}, pusto"
        )
        return [Announce(message)]

    def apply_tidal_queue(
        self,
        rows: list[Row],
        *,
        preferred_id: str | None = None,
    ) -> list[object]:
        """Pokaż prywatną kolejkę TIDAL bez mieszania jej z lokalnym hostem."""
        state = self.sessions[SessionId.TIDAL]
        state.library_view = LibraryView.TIDAL_QUEUE
        state.library_playlist_id = None
        state.library_return_view = "Kolejka TIDAL"
        state.tidal_heading = "Kolejka TIDAL"
        state.tidal_context = None
        state.tidal_history.clear()
        state.model.replace(rows, preferred_id=preferred_id)
        state.view = View.LIST
        count = len(rows)
        return [Announce(
            f"Kolejka TIDAL, {count} {_items_word(count)}"
            if count else "Kolejka TIDAL, zero elementów"
        )]

    # ----------------------------------------------------------- odtwarzanie

    def step_playback_source(self, forward: bool) -> list[object] | None:
        """Page Down/Page Up w odtwarzaczu.

        ``None`` jest jawnym poleceniem, by wykonawca poprosil ZYWA kolejke
        hosta. Lista zamiarow oznacza zwykle zrodlo: pliki albo stacje.
        Rozdzielenie jest konieczne, bo stary port wysylal KAZDY gest do
        ``queue.next``/``queue.previous`` i dlatego zwykly plik odpowiadal
        jedynie „poczatek/koniec kolejki”.
        """
        state = self.session
        if state.playback_uses_queue:
            return None

        rows = state.playback_source_rows
        if not rows:
            # Stan odtworzenia mogl zostac odtworzony przez hosta przed
            # zbudowaniem migawki zrodla. Wtedy pozwalamy zapytac jego zywa
            # kolejke. Dla znanego zwyklego pliku/zakladki (ma now_playing_id)
            # nie nazywamy go jednak kolejka.
            if state.now_playing_id is None:
                return None
            return [Announce(
                "Brak następnego elementu listy źródłowej"
                if forward else "Brak poprzedniego elementu listy źródłowej"
            )]

        current_id = state.now_playing_id
        index = next(
            (position for position, candidate in enumerate(rows)
             if candidate.item_id == current_id),
            -1,
        )
        if index < 0:
            # To nie jest powod, by skoczyc na pierwszy/ostatni wiersz i
            # zaskoczyc uzytkownika. Kontekst odtwarzania jest niepelny.
            return [Announce("Nie znajduję bieżącego elementu na liście źródłowej")]

        target_index = index + (1 if forward else -1)
        if target_index < 0 or target_index >= len(rows):
            return [Announce(
                "To ostatni element listy źródłowej"
                if forward else "To pierwszy element listy źródłowej"
            )]

        target = rows[target_index]
        if state.session_id is SessionId.TIDAL and target.kind == "track":
            intent = self._tidal_track_intent(target)
            if isinstance(intent, PlayTidalTrack):
                intent.previous_item_id = state.current_material_id
                intent.previous_title = state.now_playing_title
            return [intent]
        if (
            state.session_id is SessionId.SPOTIFY
            and target.kind in ("track", "episode")
        ):
            intent = self._spotify_play_intent(target)
            if isinstance(intent, Announce):
                return [intent]
            intent.previous_item_id = state.current_material_id
            intent.previous_title = state.now_playing_title
            state.list_anchor_id = target.item_id
            state.model.select_id(target.item_id)
            state.pending_material_id = target.item_id
            return [intent]
        state.list_anchor_id = target.item_id
        state.model.select_id(target.item_id)
        state.now_playing_id = target.item_id
        state.now_playing_title = target.title
        state.pending_material_id = profile_material_id(target.item_id)
        if target.kind == "station":
            return [
                PlayStation(target.url or "", target.item_id, target.title),
                Announce(target.title),
            ]
        if target.kind == "episode":
            return [
                PlayMedia(
                    target.path or "",
                    target.item_id,
                    target.title,
                    position_seconds=target.position_seconds,
                ),
                Announce(target.title),
            ]
        if state.session_id is SessionId.RADIOARCHIWISTA and target.kind == "recording":
            return [
                PlayMedia(
                    target.url or "",
                    target.item_id,
                    target.title,
                    position_seconds=target.position_seconds,
                ),
                Announce(target.title),
            ]
        return [
            PlayTrack(target.path or "", target.item_id, target.title),
            Announce(target.title),
        ]

    def continue_radioarchiwista_queue_after_end(self) -> list[object]:
        """Po końcu MP3 przejdź do następnego nagrania lokalnej kolejki."""
        state = self.sessions[SessionId.RADIOARCHIWISTA]
        if not state.radioarchiwista_queue_active or not state.now_playing_id:
            return []
        rows = state.playback_source_rows
        current = next(
            (index for index, row in enumerate(rows)
             if row.item_id == state.now_playing_id), -1,
        )
        if current < 0 or current + 1 >= len(rows):
            return []
        target = rows[current + 1]
        if target.kind != "recording" or not target.url:
            return []
        state.now_playing_id = target.item_id
        state.now_playing_title = target.title
        state.pending_material_id = profile_material_id(target.item_id)
        return [
            PlayMedia(target.url, target.item_id, target.title,
                      position_seconds=target.position_seconds),
            Announce(target.title),
        ]

    def note_queue_advanced(self, item_id: str, title: str) -> list[object]:
        """Host POLICZYL przejscie i juz gra nastepna pozycje kolejki.

        Aktualizujemy BIEZACY material, ale nie ruszamy ani zaznaczenia na
        liscie, ani widoku: przejscie dzieje sie samo, bez gestu uzytkownika, a
        fokus nie moze uciekac w trakcie sluchania. Zaznaczenie i audio sa tu
        celowo dwiema osobnymi rzeczami.
        """
        if not item_id:
            # Zdarzenie bez tozsamosci nic nie dowodzi -- nie czyscimy stanu.
            return []
        state = self.sessions[SessionId.FILES]
        state.now_playing_id = item_id
        state.now_playing_title = title
        # Zdarzenie kolejki już potwierdza start, także podczas oglądania Radia.
        state.current_material_id = profile_material_id(item_id)
        state.pending_material_id = ""
        return [Announce(title)] if title else []

    def note_playback_failed(
        self, message: str, session_id: SessionId | None = None
    ) -> list[object]:
        """Blad odtwarzania wraca na LISTE: w odtwarzaczu nie ma co robic."""
        state = self.sessions[session_id] if session_id is not None else self.session
        state.now_playing_id = None
        state.now_playing_title = ""
        # Nieudany start NIE moze zostawic materialu jako biezacego: Ctrl+B
        # pokazalby wtedy "biezacy" wpis dla czegos, co sie nie uruchomilo.
        state.pending_material_id = ""
        state.current_material_id = ""
        if state.view is View.PLAYER:
            state.view = View.LIST
            if state.list_anchor_id is not None:
                state.model.select_id(state.list_anchor_id)
        return [Announce(message)]

    # ------------------------------------------------- zapis i odtworzenie stanu

    @staticmethod
    def _selection_for_snapshot(state: SessionState) -> str | None:
        """Zapisz logiczny fokus korzenia, nie przypadkowy wiersz podwidoku."""
        if state.session_id is not SessionId.WIIM:
            return state.model.selected_id
        row = state.model.selected_row
        if (
            row is not None
            and row.kind == "device"
            and row.service_kind == "device"
        ):
            return row.item_id
        return state.wiim_device_return_id or state.list_anchor_id

    def snapshot(self) -> dict:
        """Stan do zapisu na dysk. Tylko to, co da sie bezpiecznie odtworzyc."""
        return {
            "active": self.active.value,
            "sessions": {
                sid.value: {
                    "folderPath": state.folder_path,
                    "selectedId": self._selection_for_snapshot(state),
                    "view": state.view.value,
                    "breadcrumb": [list(pair) for pair in state.breadcrumb],
                }
                for sid, state in self.sessions.items()
            },
        }

    def restore(self, snapshot: dict) -> None:
        """Odtworz stan. Zapis z przyszlej/uszkodzonej wersji NIE moze wywrocic startu:
        czytamy tylko rozpoznane wartosci, reszte pomijamy."""
        active = snapshot.get("active")
        for candidate in SessionId:
            if candidate.value == active:
                self.active = candidate
                break

        for sid, state in self.sessions.items():
            raw = (snapshot.get("sessions") or {}).get(sid.value) or {}
            folder = raw.get("folderPath")
            state.folder_path = folder if isinstance(folder, str) and folder else None
            selected = raw.get("selectedId")
            # Wybor zapamietujemy jako zyczenie; potwierdzi go dopiero wczytanie
            # listy, bo plik moze juz nie istniec.
            state.list_anchor_id = selected if isinstance(selected, str) else None
            # Po restarcie zawsze stajemy na LISCIE: nic jeszcze nie gra,
            # wiec widok odtwarzacza bylby pusty i myliłby uzytkownika.
            state.view = View.LIST
            state.breadcrumb = [
                (str(pair[0]), str(pair[1]))
                for pair in raw.get("breadcrumb") or []
                if isinstance(pair, (list, tuple)) and len(pair) == 2
            ]
