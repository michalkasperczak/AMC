"""Nawigacja AMC-wx-Lite: dwie sesje, widok listy i widok odtwarzacza.

Cala logika przejsc jest TUTAJ, bez wx. Okno tylko odwzorowuje ten stan.
Dzieki temu zachowanie Enter/Escape/F6/Ctrl+cyfra testujemy w WSL.

Zgodnosc skrotow z pelnym AMC zostala odczytana ze ZRODEL (KeyboardProfile.cs,
CommandIds.cs), nie wymyslona - patrz mapa w shortcuts.py.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum

from .list_model import ListModel, Row


class View(Enum):
    """Ktory widok jest na wierzchu. Odpowiada _playerViewActive w pelnym AMC."""

    LIST = "list"
    PLAYER = "player"


class SessionId(Enum):
    FILES = "files"
    RADIO = "radio"
    PODCASTS = "podcasts"


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
    #: Plany nagrywania z profilu AMC. Python tylko je pokazuje; nie uruchamia
    #: drugiego harmonogramu obok wlasciciela C#.
    RADIO_RECORDING_SCHEDULES = "radioRecordingSchedules"
    #: Glowna lista podcastow/kanalow oraz zawartosc jednego zrodla.
    #: Osobne wartosci pozwalaja Backspace i filtrom rozpoznac poziom bez
    #: ujawniania technicznego Id podcastu w nazwie dostepnej.
    PODCAST_LIBRARY = "podcastLibrary"
    PODCAST_EPISODES = "podcastEpisodes"
    #: Zbiorcze widoki z glownego AMC. ``PODCAST_INBOX`` jest globalnym,
    #: chwilowym podgladem pod Ctrl+I; ``PODCAST_IN_PROGRESS`` jest pelnym
    #: widokiem sesji Podcasty i YouTube pod Ctrl+Shift+I.
    PODCAST_INBOX = "podcastInbox"
    PODCAST_IN_PROGRESS = "podcastInProgress"
    PODCAST_DOWNLOADS = "podcastDownloads"


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
    #: ``True`` tylko wtedy, gdy odtwarzanie rzeczywiscie prowadzi kolejka
    #: hosta (Zapisana kolejka albo Zywa kolejka). Zwykly plik, zakladka i
    #: stacja nie moga pytac ``queue.previous``/``queue.next``.
    playback_uses_queue: bool = False


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
class OpenPodcastAggregateView:
    """Load one top-level aggregate without exposing a technical source ID."""

    view: LibraryView
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
    position_seconds: float = 0.0


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
        }
        self.active = SessionId.FILES

    @property
    def session(self) -> SessionState:
        return self.sessions[self.active]

    @property
    def view(self) -> View:
        return self.session.view

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

    @staticmethod
    def _session_name(session_id: SessionId) -> str:
        return {
            SessionId.FILES: "Pliki lokalne",
            SessionId.RADIO: "Radio internetowe",
            SessionId.PODCASTS: "Podcasty i YouTube",
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
        """Escape z odtwarzacza: TA SAMA lista i TO SAMO zaznaczenie."""
        state = self.session
        if state.view is View.LIST:
            return []
        state.view = View.LIST
        target = state.list_anchor_id if follow_playback else state.player_entry_anchor_id
        if target is not None:
            state.model.select_id(target)
        row = state.model.selected_row
        suffix = f", {row.title}" if row is not None else ""
        return [Announce(f"Lista{suffix}")]

    # ------------------------------------------------------------- aktywacja

    def activate_selected(
        self, *, stay_on_list_after_radio_enter: bool = False
    ) -> list[object]:
        """Enter. Folder otwiera, utwor/stacje odtwarza i przechodzi do odtwarzacza."""
        state = self.session
        row = state.model.selected_row
        if row is None:
            return [Announce("Lista jest pusta")]

        if row.kind == "parent":
            return self.go_to_parent()

        if row.kind == "playlist":
            return self._enter_playlist(row)

        if row.kind == "podcast":
            state.library_return_id = row.item_id
            return [OpenPodcastView(row.item_id, row.title)]

        if row.kind == "loadMore":
            if state.library_view in (
                LibraryView.PODCAST_INBOX,
                LibraryView.PODCAST_IN_PROGRESS,
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

        if row.activation_message:
            # Wiersz zostaje zaznaczony i widoczny. To zamierzona odmowa
            # (np. nieudane nagranie), nie powod do wejscia w odtwarzacz.
            return [Announce(row.activation_message)]

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
            if not stay_on_list_after_radio_enter:
                state.view = View.PLAYER
            return [PlayStation(row.url or "", row.item_id, row.title), Announce(row.title)]

        if row.kind == "episode":
            if not row.path:
                return [Announce("Ten odcinek nie ma adresu do odtworzenia")]
            state.player_entry_anchor_id = row.item_id
            state.list_anchor_id = row.item_id
            state.now_playing_id = row.item_id
            state.now_playing_title = row.title
            state.pending_material_id = row.item_id
            state.playback_source_rows = tuple(
                candidate for candidate in state.model.rows
                if candidate.kind == "episode" and candidate.path
            )
            state.playback_uses_queue = False
            state.view = View.PLAYER
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
            return self._activate_live_queue_row(row)

        if not row.path:
            # Wiersz ZAKLADKI nie ma sciezki i miec jej nie moze: jego
            # ``item_id`` to ``bookmark:<id>``, czyli identyfikator ZAPISU, nie
            # pliku. Prawdziwy plik i pozycja przyszly obok, w mapie celow.
            if row.item_id.startswith("bookmark:"):
                return self._activate_bookmark(row)
            return [Announce("Brak sciezki pliku")]

        # Widok Zapisanej kolejki prowadzi ZYWA kolejke hosta, a nie pojedyncze
        # odtworzenie. Zwykle ``files.play`` zerwaloby nastepstwo: po koncu
        # utworu nie byloby czym przejsc dalej.
        if state.library_view is LibraryView.SAVED_QUEUE:
            return self._activate_queue_row(row)

        state.player_entry_anchor_id = row.item_id
        state.list_anchor_id = row.item_id
        state.now_playing_id = row.item_id
        state.now_playing_title = row.title
        state.pending_material_id = profile_material_id(row.item_id)
        state.playback_source_rows = tuple(
            candidate for candidate in state.model.rows
            if candidate.kind == "track" and candidate.path
        )
        state.playback_uses_queue = False
        state.view = View.PLAYER
        return [PlayTrack(row.path, row.item_id, row.title), Announce(row.title)]

    def _activate_queue_row(self, row: Row) -> list[object]:
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
        state.playback_source_rows = ()
        state.playback_uses_queue = True
        state.view = View.PLAYER
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

    def _activate_live_queue_row(self, row: Row) -> list[object]:
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
        state.playback_source_rows = ()
        state.playback_uses_queue = True
        state.view = View.PLAYER
        return [PlayQueueAt(row.item_id, row.title), Announce(row.title)]

    def _activate_bookmark(self, row: Row) -> list[object]:
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
        state.playback_source_rows = ()
        state.playback_uses_queue = False
        state.view = View.PLAYER
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
        state.playback_source_rows = (target,)
        state.playback_uses_queue = False
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
            if state.library_view is LibraryView.PODCAST_EPISODES:
                return [OpenPodcastView(preferred_id=state.library_return_id)]
            return []
        # W nazwanym widoku Biblioteki nie ma wiersza rodzica, ale Backspace
        # nadal ma WYJSC: z zawartosci playlisty na liste playlist, a z
        # widoku plaskiego z powrotem do Folderow (MainWindow.xaml.cs:20832).
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
        state.model.replace(rows, preferred_id=preferred_id or state.library_return_id)
        state.view = View.LIST

        row = state.model.selected_row
        # Czytnik dostaje to, czego natywna lista NIE powie: nazwe widoku i
        # rozmiar. Tytulu wiersza tu NIE MA -- kontrolka wymawia go sama, i to
        # pelniej (kolumny + "1 z N"). Powtarzanie go wlasnymi slowami bylo tym
        # drugim odczytem tego samego elementu, ktory zglosil uzytkownik.
        # Puste widoki to wyjatek: tam kontrolka nie ma czego wymowic.
        if row is None:
            parts = [f"{heading}, pusto"]
        else:
            parts = [f"{heading}, {len(rows)} {_items_word(len(rows))}"]
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
        return [
            OpenLibraryView(
                view=LibraryView.PLAYLIST_CONTENTS, playlist_id=playlist_id
            )
        ]

    def _leave_library_view(self) -> list[object]:
        """Backspace w nazwanym widoku. Wyjscie, nie "najwyzszy poziom".

        Z zawartosci playlisty wracamy na liste playlist i stajemy na TEJ
        playliscie, z ktorej weszlismy (po Id wiersza, nie po numerze).
        Z widoku plaskiego wracamy do Folderow -- tam Backspace znow znaczy
        "folder nadrzedny", jak dotad.
        """
        state = self.session
        if state.session_id is SessionId.PODCASTS:
            if state.library_view is LibraryView.PODCAST_EPISODES:
                return [OpenPodcastView(preferred_id=state.library_return_id)]
            return []
        if state.session_id is SessionId.RADIO:
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
    ) -> list[object]:
        """Odpowiedz loadera Radia nie rusza listy ani wyboru Plikow."""
        if view not in (
            None,
            LibraryView.FAVORITES,
            LibraryView.HISTORY,
            LibraryView.ACTIVE_RADIO_RECORDINGS,
            LibraryView.RADIO_RECORDING_SCHEDULES,
        ):
            raise ValueError("Nieobslugiwany widok Radia")
        state = self.sessions[SessionId.RADIO]
        old_key = state.library_view.value if state.library_view else "library"
        if state.model.selected_id is not None:
            state.view_selected_ids[old_key] = state.model.selected_id
        key = view.value if view else "library"
        state.library_view = view
        state.model.replace(rows, preferred_id=preferred_id or state.view_selected_ids.get(key))
        state.view = View.LIST
        # Nazwe wiersza czyta natywna lista, nie powtarzamy jej w komunikacie.
        if rows:
            message = heading
        elif view is LibraryView.ACTIVE_RADIO_RECORDINGS:
            # Konkretna liczba jest szybsza i jednoznaczna. To rowniez
            # komunikat koncowy po aktualizacji pustej natywnej listy, dzieki
            # czemu NVDA nie zostawia uzytkownika ze slowem "nieznane".
            message = f"{heading}, zero elementów"
        else:
            message = f"{heading}, pusto"
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
        self, rows: list[Row], preferred_id: str | None = None
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
        return [Announce(
            f"Podcasty i YouTube, {len(rows)} {_items_word(len(rows))}"
            if rows else "Podcasty i YouTube, pusto"
        )]

    def apply_podcast_episodes(
        self,
        subscription_id: str,
        subscription_title: str,
        rows: list[Row],
        *,
        preferred_id: str | None = None,
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
        return [Announce(
            f"{subscription_title}, {count} {_items_word(count)}"
            if count else f"{subscription_title}, pusto"
        )]

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
            LibraryView.PODCAST_INBOX,
            LibraryView.PODCAST_IN_PROGRESS,
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
        count = sum(1 for row in rows if row.kind == "episode")
        if view is LibraryView.PODCAST_INBOX and count == 0:
            message = "Nowe odcinki i materiały, brak nowych materiałów"
        elif view is LibraryView.PODCAST_IN_PROGRESS and count == 0:
            message = "W trakcie słuchania, brak rozpoczętych odcinków"
        elif view is LibraryView.PODCAST_DOWNLOADS and count == 0:
            message = "Pobrane, brak pobranych odcinków"
        else:
            message = f"{heading}, {count} {_items_word(count)}"
        if not order_matches_amc:
            message = f"{message}, kolejność zastępcza"
        return [Announce(message)]

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
        return [
            PlayTrack(target.path or "", target.item_id, target.title),
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

    def snapshot(self) -> dict:
        """Stan do zapisu na dysk. Tylko to, co da sie bezpiecznie odtworzyc."""
        return {
            "active": self.active.value,
            "sessions": {
                sid.value: {
                    "folderPath": state.folder_path,
                    "selectedId": state.model.selected_id,
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
