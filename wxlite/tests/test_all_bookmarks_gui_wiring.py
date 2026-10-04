"""ZBIORCZY widok zakladek PODLACZONY do interfejsu: gest, menu, kontekst, skok.

Warstwa danych (``library_activity.all_bookmark_rows``) jest odebrana osobno i
TUTAJ jej nie powtarzamy. Sprawdzamy wylacznie to, czego brakowalo: czy da sie
ten widok otworzyc z klawiatury i z menu, czy kontekst biezacego materialu jest
TYM, co w WPF (``_sessions.Current.CurrentItem``), czy lokalna zakladka skacze
jednym ``files.play`` i czy obca sesja dostaje uczciwa odmowe zamiast ciszy.
"""

from __future__ import annotations

import ast
import sqlite3
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite import library_activity, menu_model, shortcuts
from amc_wx_lite.library_source import LibrarySource
from amc_wx_lite.library_views import BookmarkContext
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import (
    Announce,
    LibraryView,
    Navigator,
    OpenFolder,
    OpenLibraryView,
    PlayTrack,
    SessionId,
    View,
)
from amc_wx_lite.profile_layout import ProfileLayout, ProfileMode
from amc_wx_lite.shortcuts import Action, Chord, resolve

# --------------------------------------------------------------- 1. gest i menu


def test_ctrl_b_opens_the_collective_view_and_is_not_ctrl_shift_b() -> None:
    """Ctrl+B to ``ViewBookmarks``/``GetForDisplay`` -- WSZYSTKIE zakladki.

    Ctrl+Shift+B (``GetForItem``) musi zostac osobnym, wezszym gestem: dwa
    rozne zbiory nie moga dzielic jednego klawisza.
    """
    collective = resolve(Chord("B", ctrl=True), player_view=False, radio_session=False)
    single = resolve(
        Chord("B", ctrl=True, shift=True), player_view=False, radio_session=False
    )
    assert collective is Action.VIEW_ALL_BOOKMARKS
    assert single is Action.VIEW_ITEM_BOOKMARKS
    assert collective is not single


def test_menu_has_the_collective_bookmarks_item_on_the_same_action() -> None:
    """Pozycja menu musi wywolac TEN SAM ``Action``, a nie wlasna logike."""
    items = [
        item
        for menu in menu_model.build_menus()
        for item in menu.items
        if item.action is Action.VIEW_ALL_BOOKMARKS
    ]
    assert len(items) == 1, "dokladnie jedna pozycja menu dla widoku zbiorczego"
    item = items[0]
    assert item.shortcut == "Ctrl+B", "podpis w menu musi byc prawda"
    # Widok zbiorczy NIE zalezy od zaznaczenia: kontekstem jest grajacy
    # material, wiec pozycja nie moze byc wyszarzana bez wyboru wiersza.
    assert not item.needs_selection


def test_collective_bookmarks_action_is_handled_in_the_window() -> None:
    """Bez galezi w ``_dispatch`` skrot i menu byly by martwe."""
    source = (
        Path(__file__).resolve().parents[1] / "amc_wx_lite" / "gui.py"
    ).read_text(encoding="utf-8")
    tree = ast.parse(source)
    used = {
        node.attr
        for node in ast.walk(tree)
        if isinstance(node, ast.Attribute)
        and isinstance(node.value, ast.Name)
        and node.value.id == "Action"
    }
    assert Action.VIEW_ALL_BOOKMARKS.name in used
    keys = next(
        node.value
        for node in ast.walk(tree)
        if isinstance(node, ast.Assign)
        and any(isinstance(t, ast.Name) and t.id == "_VIEW_KEYS" for t in node.targets)
    )
    mapped = {k.attr for k in keys.keys if isinstance(k, ast.Attribute)}
    assert LibraryView.ALL_BOOKMARKS.name in mapped, "widok bez klucza danych"


def test_help_names_both_bookmark_views_separately() -> None:
    """Pomoc nie moze obiecywac jednego zbioru pod dwoma gestami."""
    described = dict(shortcuts.describe())
    assert "Ctrl+B" in described and "Ctrl+Shift+B" in described
    assert described["Ctrl+B"] != described["Ctrl+Shift+B"]


# -------------------------------------------- 2. kontekst biezacego materialu


def _library_rows() -> list[Row]:
    return [
        Row(item_id="42", title="Alfa", kind="track", path="C:\\m\\Alfa.mp3"),
        Row(item_id="43", title="Beta", kind="track", path="C:\\m\\Beta.mp3"),
    ]


def test_context_is_the_playing_item_not_the_selected_row() -> None:
    """``_sessions.Current.CurrentItem``, nie zaznaczenie listy.

    Graj A, zaznacz B, wywolaj widok zbiorczy: kontekstem musi byc A.
    """
    nav = Navigator()
    nav.apply_library_view(LibraryView.ALL_FILES, "Wszystkie pliki", _library_rows())
    nav.session.model.select_id("42")
    nav.activate_selected()
    nav.note_playback_started()
    nav.back_to_list()
    nav.session.model.select_id("43")

    intent = next(
        i for i in nav.open_all_bookmarks() if isinstance(i, OpenLibraryView)
    )
    assert intent.view is LibraryView.ALL_BOOKMARKS
    assert intent.current_item_id == "42", "kontekst wzialby zaznaczony wiersz"
    assert intent.current_session_id == library_activity.LOCAL_SESSION


def test_selection_alone_never_becomes_the_context() -> None:
    """Samo chodzenie po liscie NIE jest odtwarzaniem."""
    nav = Navigator()
    nav.apply_library_view(LibraryView.ALL_FILES, "Wszystkie pliki", _library_rows())
    nav.session.model.select_id("43")
    intent = next(
        i for i in nav.open_all_bookmarks() if isinstance(i, OpenLibraryView)
    )
    assert intent.current_item_id == ""
    assert intent.current_session_id == ""


def test_paused_material_is_still_the_current_item() -> None:
    """PAUZA nie konczy materialu: ``CurrentItem`` zostaje (nie ``IsPlaying``).

    W tej aplikacji pauza idzie wylacznie przez ``transport.playPause`` w
    hoscie i etykiete przycisku -- do nawigatora nie wraca. Kontekst musi wiec
    przetrwac zmiane widoku i kazda droge, ktora pauza przechodzi, bo gdyby
    byl liczony z \"czy teraz gra\", zakladki granego materialu spadlyby na dol
    listy w chwili zatrzymania.
    """
    nav = Navigator()
    nav.apply_library_view(LibraryView.ALL_FILES, "Wszystkie pliki", _library_rows())
    nav.session.model.select_id("42")
    nav.activate_selected()
    nav.note_playback_started()
    nav.show_player()
    nav.back_to_list()
    intent = next(
        i for i in nav.open_all_bookmarks() if isinstance(i, OpenLibraryView)
    )
    assert intent.current_item_id == "42", "pauza zgubila biezacy material"


def test_pause_path_in_the_window_does_not_touch_the_context() -> None:
    """Gałąź pauzy w oknie nie moze zerowac materialu sesji.

    Sprawdzamy ZRODLO, bo to jedyny sposob udowodnienia, ze pauza nie idzie
    przez nawigator, nie uruchamiajac wx. ``_play_pause`` wolno dotykac
    etykiety transportu i statusu, ale nie ``note_playback_*``.
    """
    source = (
        Path(__file__).resolve().parents[1] / "amc_wx_lite" / "gui.py"
    ).read_text(encoding="utf-8")
    tree = ast.parse(source)
    play_pause = next(
        node
        for node in ast.walk(tree)
        if isinstance(node, ast.FunctionDef) and node.name == "_play_pause"
    )
    called = {
        node.func.attr
        for node in ast.walk(play_pause)
        if isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute)
    }
    assert "note_playback_failed" not in called
    assert "note_playback_started" not in called


def test_failed_start_leaves_no_context() -> None:
    """Nieudany start nie jest materialem sesji."""
    nav = Navigator()
    nav.apply_library_view(LibraryView.ALL_FILES, "Wszystkie pliki", _library_rows())
    nav.session.model.select_id("42")
    nav.activate_selected()
    nav.note_playback_failed("Nie udalo sie odtworzyc")
    intent = next(
        i for i in nav.open_all_bookmarks() if isinstance(i, OpenLibraryView)
    )
    assert intent.current_item_id == ""


def test_file_and_bookmark_prefixes_are_never_the_profile_id() -> None:
    """``file:<path>`` i ``bookmark:<id>`` NIE sa Id profilowym.

    Uruchomienie z przegladania folderu daje ``Row.item_id = \"file:...\"``
    (``LiteEngineHandlers.cs:269``), a wiersz zakladki ``bookmark:<id>``.
    Podanie ktoregokolwiek jako ``currentItemId`` dalo by kontekst, ktory do
    NICZEGO nie pasuje, a wygladalby poprawnie.
    """
    nav = Navigator()
    nav.apply_folder(
        "C:\\m",
        [Row(item_id="file:C:\\m\\Alfa.mp3", title="Alfa", kind="track",
             path="C:\\m\\Alfa.mp3")],
    )
    nav.activate_selected()
    nav.note_playback_started()
    intent = next(
        i for i in nav.open_all_bookmarks() if isinstance(i, OpenLibraryView)
    )
    assert intent.current_item_id == "", "file: nie moze udawac Id profilowego"


def test_bookmark_start_sets_the_real_profile_id_of_the_file() -> None:
    """Po skoku z zakladki kontekstem jest Id PLIKU, nie Id zapisu."""
    nav = _nav_with_collective_view()
    nav.session.model.select_id("bookmark:b1")
    nav.activate_selected()
    nav.note_playback_started()
    intent = next(
        i for i in nav.open_all_bookmarks() if isinstance(i, OpenLibraryView)
    )
    assert intent.current_item_id == "42"
    assert not intent.current_item_id.startswith("bookmark:")


def test_radio_session_has_no_bookmark_context() -> None:
    """Sesja radiowa nie ma zakladek w danych -- nie wymyslamy jej klucza."""
    nav = Navigator()
    nav.apply_stations([Row(item_id="s1", title="Stacja", kind="station", url="http://x")])
    nav.switch_session(SessionId.RADIO)
    nav.activate_selected()
    nav.note_playback_started()
    intent = next(
        i for i in nav.open_all_bookmarks() if isinstance(i, OpenLibraryView)
    )
    assert (intent.current_session_id, intent.current_item_id) == ("", "")


# ----------------------------------------- 3. skok lokalny i odmowa zdalnej


def _nav_with_collective_view() -> Navigator:
    """Widok zbiorczy z JEDNA lokalna i JEDNA obca zakladka."""
    nav = Navigator()
    rows = [
        Row(item_id="bookmark:b1", title="Alfa, 1:23, Pliki lokalne, zakładka",
            kind="track"),
        Row(item_id="bookmark:b2", title="Obcy, 0:30, Spotify, zakładka",
            kind="track"),
    ]
    nav.apply_library_view(
        LibraryView.ALL_BOOKMARKS,
        "Biblioteka — Wszystkie zakładki",
        rows,
        bookmark_targets={"bookmark:b1": ("C:\\m\\Alfa.mp3", 83.456, "Alfa")},
        bookmark_contexts={
            "bookmark:b1": BookmarkContext(
                item_id="42",
                session_id="local",
                session_name="Pliki lokalne",
                can_play_locally=True,
                is_current_item=False,
                position_seconds=83.456,
            ),
            "bookmark:b2": BookmarkContext(
                item_id="spotify:track:x",
                session_id="spotify",
                session_name="Spotify",
                can_play_locally=False,
                is_current_item=False,
                position_seconds=30.0,
            ),
        },
    )
    return nav


def test_local_bookmark_jumps_with_one_play_call() -> None:
    nav = _nav_with_collective_view()
    nav.session.model.select_id("bookmark:b1")
    out = nav.activate_selected()
    plays = [i for i in out if isinstance(i, PlayTrack)]
    assert len(plays) == 1, "jedno files.play, bez osobnego seeka"
    assert plays[0].path == "C:\\m\\Alfa.mp3"
    assert abs(plays[0].position_seconds - 83.456) < 1e-9
    assert nav.session.view is View.PLAYER


def test_foreign_session_bookmark_refuses_without_playing() -> None:
    """Obca sesja: zadnego ``files.play`` i zadnej obietnicy skoku."""
    nav = _nav_with_collective_view()
    nav.session.model.select_id("bookmark:b2")
    out = nav.activate_selected()
    assert not any(isinstance(i, PlayTrack) for i in out)
    assert nav.session.view is View.LIST, "nie wchodzimy do odtwarzacza"
    assert nav.session.model.selected_id == "bookmark:b2", "wiersz zostaje widoczny"
    message = next(i for i in out if isinstance(i, Announce)).text
    assert "Spotify" in message, "odmowa musi nazwac WLASCIWA sesje"


def test_foreign_bookmark_is_refused_even_if_an_id_collides() -> None:
    """Obce Id moze przypadkiem rownac sie lokalnemu -- decyduje SESJA.

    Gdyby kolejnosc sprawdzen byla odwrotna (najpierw mapa celow), zakladka
    TIDAL o Id ``42`` odtworzylaby lokalny plik 42.
    """
    nav = Navigator()
    nav.apply_library_view(
        LibraryView.ALL_BOOKMARKS,
        "z",
        [Row(item_id="bookmark:b9", title="Obcy, TIDAL, zakładka", kind="track")],
        bookmark_targets={"bookmark:b9": ("C:\\m\\Alfa.mp3", 5.0, "Alfa")},
        bookmark_contexts={
            "bookmark:b9": BookmarkContext(
                item_id="42",
                session_id="tidal",
                session_name="TIDAL",
                can_play_locally=False,
                is_current_item=False,
                position_seconds=5.0,
            )
        },
    )
    out = nav.activate_selected()
    assert not any(isinstance(i, PlayTrack) for i in out)
    assert "TIDAL" in next(i for i in out if isinstance(i, Announce)).text


def test_local_bookmark_without_a_file_does_not_pretend_to_play() -> None:
    nav = Navigator()
    nav.apply_library_view(
        LibraryView.ALL_BOOKMARKS,
        "z",
        [Row(item_id="bookmark:b3", title="Zniknal, zakładka", kind="track")],
        bookmark_contexts={
            "bookmark:b3": BookmarkContext(
                item_id="999",
                session_id="local",
                session_name="Pliki lokalne",
                can_play_locally=True,
                is_current_item=False,
                position_seconds=1.0,
            )
        },
    )
    out = nav.activate_selected()
    assert not any(isinstance(i, PlayTrack) for i in out)
    assert nav.session.view is View.LIST


# ------------------------------------------------------------------ 4. powrot


def test_escape_from_the_player_returns_to_the_bookmark_row() -> None:
    nav = _nav_with_collective_view()
    nav.session.model.select_id("bookmark:b1")
    nav.activate_selected()
    assert nav.session.view is View.PLAYER
    nav.back_to_list()
    assert nav.session.view is View.LIST
    assert nav.session.model.selected_id == "bookmark:b1"


def test_backspace_leaves_the_collective_view_to_folders() -> None:
    """Widok zbiorczy nie dotyczy jednego pliku, wiec wyjscie to Foldery."""
    nav = _nav_with_collective_view()
    out = nav.go_to_parent()
    assert any(isinstance(i, OpenFolder) for i in out)
    assert nav.session.library_view is None
    assert nav.session.bookmark_contexts == {}
    assert nav.session.bookmark_targets == {}


def test_leaving_the_view_drops_the_context_map() -> None:
    """Mapa kontekstow nie moze przezyc zmiany widoku."""
    nav = _nav_with_collective_view()
    nav.apply_library_view(LibraryView.ALL_FILES, "Wszystkie pliki", _library_rows())
    assert nav.session.bookmark_contexts == {}
    assert nav.session.bookmark_targets == {}


# ------------------------------------------------- 5. droga przez LibrarySource

#: Schemat jak w prawdziwym profilu (``COLLATE AMC_PL`` wlacznie) -- inaczej
#: ``LibraryDatabase`` czytalby inna baze niz ta, ktora dziala u uzytkownika.
SCHEMA = """
CREATE TABLE local_items (
    id TEXT PRIMARY KEY,
    title TEXT NOT NULL COLLATE AMC_PL,
    has_custom_title INTEGER NOT NULL DEFAULT 0,
    path TEXT NOT NULL,
    duration_ticks INTEGER NOT NULL,
    bitrate_estimated INTEGER NOT NULL DEFAULT 0,
    is_favorite INTEGER NOT NULL,
    is_in_library INTEGER NOT NULL,
    is_available INTEGER NOT NULL,
    is_in_queue INTEGER NOT NULL DEFAULT 0,
    is_play_next INTEGER NOT NULL DEFAULT 0,
    resume_mode INTEGER NOT NULL DEFAULT 0,
    resume_position_ticks INTEGER NOT NULL DEFAULT 0,
    is_radio_recording INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE bookmarks (
    id TEXT PRIMARY KEY,
    ordinal INTEGER NOT NULL,
    session_id TEXT NOT NULL,
    session_name TEXT NOT NULL,
    item_id TEXT NOT NULL,
    item_title TEXT NOT NULL COLLATE AMC_PL,
    name TEXT NOT NULL COLLATE AMC_PL,
    position_ticks INTEGER NOT NULL,
    created_utc_ticks INTEGER NOT NULL,
    purpose INTEGER NOT NULL DEFAULT 1,
    chapter_origin INTEGER NOT NULL DEFAULT 0,
    chapter_source_id TEXT NULL
);
CREATE TABLE metadata (key TEXT PRIMARY KEY, value TEXT NOT NULL);
"""


def _source(tmp: Path) -> LibrarySource:
    db = tmp / "library.db"
    con = sqlite3.connect(db)
    con.create_collation("AMC_PL", lambda a, b: (a > b) - (a < b))
    con.executescript(SCHEMA)
    con.execute(
        "INSERT INTO local_items (id, title, path, duration_ticks, is_favorite, "
        "is_in_library, is_available) VALUES "
        "('42','Alfa','C:\\m\\Alfa.mp3',600000000,0,1,1)"
    )
    con.execute(
        "INSERT INTO bookmarks (id, ordinal, session_id, session_name, item_id, "
        "item_title, name, position_ticks, created_utc_ticks) VALUES "
        "('b1',0,'local','Pliki lokalne','42','Alfa','',834560000,1)"
    )
    # Obca sesja o TYM SAMYM Id elementu: celu lokalnego dostac nie moze.
    con.execute(
        "INSERT INTO bookmarks (id, ordinal, session_id, session_name, item_id, "
        "item_title, name, position_ticks, created_utc_ticks) VALUES "
        "('b2',1,'spotify','Spotify','42','Obcy','',300000000,2)"
    )
    con.commit()
    con.close()
    layout = ProfileLayout(
        mode=ProfileMode.READ_ONLY_MIRROR,
        library_db=db,
        podcasts_db=tmp / "podcasts.db",
        state_json=tmp / "state.json",
        lite_settings_dir=tmp / "_lite",
    )
    return LibrarySource(layout)


def test_load_view_knows_the_collective_bookmarks_view() -> None:
    with tempfile.TemporaryDirectory() as raw:
        src = _source(Path(raw))
        result = src.load_view(
            "all_bookmarks", current_session_id="local", current_item_id="42"
        )
        assert len(result.rows) == 2
        # Bez kolatora kolejnosc jest ZASTEPCZA i mowimy to wprost.
        assert result.order_matches_amc is False
        assert "zakładki" in result.heading.lower()


def test_collective_targets_cover_local_rows_only() -> None:
    """Obca zakladka o lokalnym Id NIE MOZE dostac sciezki pliku."""
    with tempfile.TemporaryDirectory() as raw:
        src = _source(Path(raw))
        result = src.load_view(
            "all_bookmarks", current_session_id="", current_item_id=""
        )
        assert "bookmark:b1" in result.bookmark_targets
        assert "bookmark:b2" not in result.bookmark_targets
        local = result.bookmark_contexts["bookmark:b1"]
        foreign = result.bookmark_contexts["bookmark:b2"]
        assert local.can_play_locally and local.item_id == "42"
        assert not foreign.can_play_locally
        assert foreign.session_name == "Spotify"
        assert abs(local.position_seconds - 83.456) < 1e-9


def test_current_context_marks_the_row_from_the_data_layer() -> None:
    """``is_current_item`` ma przyjsc Z DANYCH, nie byc policzone w GUI."""
    with tempfile.TemporaryDirectory() as raw:
        src = _source(Path(raw))
        result = src.load_view(
            "all_bookmarks", current_session_id="local", current_item_id="42"
        )
        assert result.bookmark_contexts["bookmark:b1"].is_current_item
        assert not result.bookmark_contexts["bookmark:b2"].is_current_item
