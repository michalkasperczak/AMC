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
    #: Zakladki JEDNEGO zaznaczonego elementu (``BookmarkIndex.GetForItem``).
    #: NIE jest to zbiorczy widok ``ViewBookmarks``/``GetForDisplay``.
    ITEM_BOOKMARKS = "itemBookmarks"


@dataclass(slots=True)
class OpenLibraryView:
    """Zlecenie: wczytaj dane nazwanego widoku Biblioteki.

    Okno samo nic nie wymysla -- idzie po dane do ``library_views`` i wraca
    przez ``apply_library_view``. ``playlist_id`` wypelniamy wylacznie dla
    zawartosci playlisty.
    """

    view: LibraryView
    playlist_id: str | None = None
    preferred_id: str | None = None
    #: RZECZYWISTE Id elementu dla ``ITEM_BOOKMARKS``. ``bookmark_rows``
    #: inaczej zawolac sie nie da -- w C# tez przyjmuje Id, nie indeks listy.
    item_id: str | None = None


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
class Announce:
    """Krotki komunikat dla czytnika. JEDNA brama komunikatow w calej aplikacji."""

    text: str


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
        return "Pliki lokalne" if session_id is SessionId.FILES else "Radio internetowe"

    # ---------------------------------------------------------------- widoki

    def show_player(self) -> list[object]:
        """F6 z listy. Zgodnie z MainWindow.xaml.cs:20778 (ShowPlayerView)."""
        state = self.session
        if state.view is View.PLAYER:
            # F6 w odtwarzaczu wraca na liste (MainWindow.xaml.cs:20776).
            return self.back_to_list()
        if state.now_playing_id is None:
            return [Announce("Nic nie jest odtwarzane")]
        state.list_anchor_id = state.model.selected_id
        state.view = View.PLAYER
        return [Announce(f"Odtwarzacz, {state.now_playing_title}")]

    def toggle_view(self) -> list[object]:
        """Zachowane dla wygody: rownowazne show_player/back_to_list."""
        state = self.session
        if state.view is View.LIST:
            if state.now_playing_id is None:
                return [Announce("Nic nie jest odtwarzane")]
            state.list_anchor_id = state.model.selected_id
            state.view = View.PLAYER
            return [Announce(f"Odtwarzacz, {state.now_playing_title}")]
        return self.back_to_list()

    def back_to_list(self) -> list[object]:
        """Escape z odtwarzacza: TA SAMA lista i TO SAMO zaznaczenie."""
        state = self.session
        if state.view is View.LIST:
            return []
        state.view = View.LIST
        if state.list_anchor_id is not None:
            state.model.select_id(state.list_anchor_id)
        row = state.model.selected_row
        suffix = f", {row.title}" if row is not None else ""
        return [Announce(f"Lista{suffix}")]

    # ------------------------------------------------------------- aktywacja

    def activate_selected(self) -> list[object]:
        """Enter. Folder otwiera, utwor/stacje odtwarza i przechodzi do odtwarzacza."""
        state = self.session
        row = state.model.selected_row
        if row is None:
            return [Announce("Lista jest pusta")]

        if row.kind == "parent":
            return self.go_to_parent()

        if row.kind == "playlist":
            return self._enter_playlist(row)

        if row.kind == "folder":
            if not row.path:
                return [Announce("Brak sciezki folderu")]
            if state.folder_path:
                state.breadcrumb.append((state.folder_path, row.item_id))
            return [OpenFolder(row.path)]

        if row.kind == "station":
            state.list_anchor_id = row.item_id
            state.now_playing_id = row.item_id
            state.now_playing_title = row.title
            state.view = View.PLAYER
            return [PlayStation(row.url or "", row.item_id, row.title), Announce(row.title)]

        if not row.path:
            # Wiersz ZAKLADKI nie ma sciezki i miec jej nie moze: jego
            # ``item_id`` to ``bookmark:<id>``, czyli identyfikator ZAPISU, nie
            # pliku. Prawdziwy plik i pozycja przyszly obok, w mapie celow.
            target = state.bookmark_targets.get(row.item_id)
            if target is not None:
                path, position_seconds, title = target
                state.list_anchor_id = row.item_id
                state.now_playing_id = f"file:{path}"
                state.now_playing_title = title
                state.view = View.PLAYER
                return [
                    PlayTrack(
                        path,
                        f"file:{path}",
                        title,
                        position_seconds=position_seconds,
                    ),
                    # Komunikat mowi, ze to SKOK do zapisanej pozycji, a nie
                    # zwykly start od zera.
                    Announce(f"{title}, od zakładki"),
                ]
            if row.item_id.startswith("bookmark:"):
                # Zakladki NIE maja filtra ActiveLocalItems, wiec moga wskazywac
                # material, ktorego biezacy katalog nie zna. Nie udajemy, ze
                # gramy -- i nie wchodzimy do odtwarzacza.
                return [Announce("Nie znajduję pliku tej zakładki")]
            return [Announce("Brak sciezki pliku")]
        state.list_anchor_id = row.item_id
        state.now_playing_id = row.item_id
        state.now_playing_title = row.title
        state.view = View.PLAYER
        return [PlayTrack(row.path, row.item_id, row.title), Announce(row.title)]

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
        # W nazwanym widoku Biblioteki nie ma wiersza rodzica, ale Backspace
        # nadal ma WYJSC: z zawartosci playlisty na liste playlist, a z
        # widoku plaskiego z powrotem do Folderow (MainWindow.xaml.cs:20832).
        if state.library_view is not None:
            return self._leave_library_view()

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
        LibraryView.ITEM_BOOKMARKS: "Zakładki",
    }

    #: Widoki aktywnosci sa PLASKIE jak ALL_FILES/FAVORITES: Backspace z nich
    #: wychodzi do Folderow, nie szuka "folderu nadrzednego".
    _ACTIVITY_VIEWS = (
        LibraryView.HISTORY,
        LibraryView.SAVED_QUEUE,
        LibraryView.ITEM_BOOKMARKS,
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
            OpenLibraryView(view=view, playlist_id=playlist_id, preferred_id=preferred_id)
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

        state.library_view = view
        state.library_playlist_id = playlist_id
        state.library_item_id = item_id
        # Mapa celow zakladek obowiazuje TYLKO w swoim widoku. Zostawienie jej
        # przy wejsciu w inny widok groziloby odtworzeniem pozycji ze starej
        # zakladki na niepowiazanym wierszu.
        state.bookmark_targets = dict(bookmark_targets or {})
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
        return [OpenFolder(state.folder_path)]

    # -------------------------------------------------------- wynik operacji

    def apply_folder(self, path: str, rows: list[Row], preferred_id: str | None = None) -> list[object]:
        """Skutek udanego ``files.listFolder``. Wywolywane w watku GUI."""
        state = self.sessions[SessionId.FILES]
        state.folder_path = path
        # Wejscie w folder konczy nazwany widok Biblioteki: od tej chwili
        # Backspace znow znaczy "folder nadrzedny".
        state.library_view = None
        state.library_playlist_id = None
        state.library_return_id = None
        state.library_item_id = None
        state.bookmark_targets = {}
        state.model.replace(rows, preferred_id=preferred_id)
        state.view = View.LIST
        row = state.model.selected_row
        name = path.rstrip("/\\").rsplit("/", 1)[-1].rsplit("\\", 1)[-1] or path
        count = sum(1 for r in state.model.rows if r.kind != "parent")
        # Tytul wiersza wymawia natywna lista (gest C00d pokazal go az 3 razy
        # po jednym Ctrl+O). Mowimy tylko to, czego kontrolka nie powie.
        suffix = "" if row is not None else ", pusty"
        return [Announce(f"{name}, {count} elementow{suffix}")]

    def apply_stations(self, rows: list[Row], preferred_id: str | None = None) -> list[object]:
        state = self.sessions[SessionId.RADIO]
        state.model.replace(rows, preferred_id=preferred_id)
        row = state.model.selected_row
        suffix = f", {row.title}" if row is not None else ", lista pusta"
        return [Announce(f"Stacje: {len(rows)}{suffix}")]

    # ----------------------------------------------------------- odtwarzanie

    def note_playback_failed(self, message: str) -> list[object]:
        """Blad odtwarzania wraca na LISTE: w odtwarzaczu nie ma co robic."""
        state = self.session
        state.now_playing_id = None
        state.now_playing_title = ""
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
