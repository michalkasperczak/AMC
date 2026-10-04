"""Trzy widoki aktywnosci PODLACZONE do GUI: wejscie, wybor, wykonanie, powrot.

Warstwa danych (``library_activity.py``) jest juz scalona i odebrana. Tutaj
sprawdzamy WYLACZNIE to, czego ona nie dotyka:

  * skroty i pozycje menu zgodne z ODCZYTANYM kodem C# (nie zgadniete),
  * wejscie w widok, powrot z zachowanym Id, puste listy i bledy,
  * aktywacja wiersza historii/kolejki ta sama droga co zwykly plik,
  * aktywacja ZAKLADKI: ``Row.item_id`` to ``bookmark:<id>``, a do backendu
    MUSI pojsc sciezka PRAWDZIWEGO pliku razem z pozycja -- jednym ``play``,
    nie ``play`` plus osobny ``seek``.

Skroty wziete WPROST z kodu, z numerami wierszy:

    ``MainWindow.xaml:487``  ``Header="_Kolejka"``             ``Ctrl+Q``
    ``MainWindow.xaml:488``  ``Header="_Historia odtwarzania"`` ``Ctrl+H``
    ``MainWindow.xaml:446``  ``Header="Foldery _Biblioteki"``   ``Alt+1``
    ``KeyboardProfile.cs:84`` ``Bind("Q", CommandIds.ViewQueue)``
    ``KeyboardProfile.cs:90`` ``Bind("H", CommandIds.ViewHistory)``

Dlaczego zakladki NIE dostaja Ctrl+B
------------------------------------
``Ctrl+B`` w oryginale (``MainWindow.xaml:489`` -> ``BookmarksViewMenu_Click``
-> ``CommandIds.ViewBookmarks`` -> ``MainWindow.xaml.cs:12530``) wola
``BookmarkIndex.GetForDisplay`` (``BookmarkIndex.cs:19-28``): WSZYSTKIE
zakladki wszystkich sesji, z biezacym elementem tylko PRZESUNIETYM na przod.
Nasza funkcja to ``bookmark_rows`` = port ``GetForItem``
(``BookmarkIndex.cs:30-36``), czyli zakladki JEDNEGO elementu -- zakres
WEZSZY. Podpiecie wezszej funkcji pod cudzy szerszy skrot byloby klamstwem
wobec uzytkownika, ktory zna AMC. Dostaje wiec wlasny gest i wlasna etykiete
mowiaca, czego dotyczy.
"""

from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

from amc_wx_lite import menu_model  # noqa: E402
from amc_wx_lite.list_model import Row  # noqa: E402
from amc_wx_lite.navigation import (  # noqa: E402
    Announce,
    LibraryView,
    Navigator,
    OpenLibraryView,
    PlayTrack,
    SessionId,
    View,
)
from amc_wx_lite.shortcuts import LIST_VIEW, Action, Chord, resolve  # noqa: E402


def track(item_id: str, title: str) -> Row:
    return Row(item_id=item_id, title=title, kind="track", path=f"C:\\m\\{title}.mp3")


# --------------------------------------------------------------- 1. skroty


def test_history_and_queue_use_the_shortcuts_from_the_csharp_menu() -> None:
    """Ctrl+H i Ctrl+Q -- dokladnie jak MainWindow.xaml:487-488."""
    assert resolve(Chord("H", ctrl=True), player_view=False, radio_session=False) is (
        Action.VIEW_HISTORY
    )
    assert resolve(Chord("Q", ctrl=True), player_view=False, radio_session=False) is (
        Action.VIEW_SAVED_QUEUE
    )


def test_item_bookmarks_do_not_steal_the_broader_ctrl_b() -> None:
    """Ctrl+B nalezy do GetForDisplay (wszystkie zakladki) -- nie bierzemy go.

    Gdyby wezszy widok odpowiadal na Ctrl+B, uzytkownik AMC dostalby pod znanym
    skrotem INNY zbior niz w oryginale, bez ostrzezenia.
    """
    assert resolve(Chord("B", ctrl=True), player_view=False, radio_session=False) is None
    assert Action.VIEW_ITEM_BOOKMARKS not in LIST_VIEW.values() or (
        LIST_VIEW.get("Ctrl+B") is not Action.VIEW_ITEM_BOOKMARKS
    )


def test_item_bookmarks_have_their_own_working_gesture() -> None:
    """Wezszy zakres = wlasny gest, a nie brak dostepu."""
    action = resolve(Chord("B", ctrl=True, shift=True), player_view=False, radio_session=False)
    assert action is Action.VIEW_ITEM_BOOKMARKS


def test_folders_view_keeps_the_alt_1_of_the_original() -> None:
    """MainWindow.xaml:446 ``Foldery _Biblioteki`` ``Alt+1``."""
    assert resolve(Chord("1", alt=True), player_view=False, radio_session=False) is (
        Action.VIEW_FOLDERS
    )


# ----------------------------------------------------------------- 2. menu


def _menu(title: str) -> menu_model.Menu:
    return next(m for m in menu_model.build_menus() if m.title == title)


def test_library_menu_lists_the_three_activity_views() -> None:
    labels = [i.label for i in _menu("&Biblioteka").items if not i.is_separator]
    assert "&Historia odtwarzania" in labels
    assert "&Kolejka" in labels
    assert "&Zakładki zaznaczonego" in labels


def test_bookmark_menu_label_says_whose_bookmarks_these_are() -> None:
    """Etykieta uczciwie wezsza od oryginalnych "Zakładki"."""
    item = next(
        i for i in _menu("&Biblioteka").items if i.action is Action.VIEW_ITEM_BOOKMARKS
    )
    assert "zaznaczonego" in item.label.lower()
    assert item.needs_selection, "bez wiersza nie ma czyich zakladek pokazac"


def test_menu_regains_the_return_to_list_and_the_folders_entry() -> None:
    """Dwie pozycje istniejacych akcji, ktorych w menu brakowalo."""
    view_labels = {i.action for i in _menu("&Widok").items if not i.is_separator}
    assert Action.SHOW_LIST in view_labels, "powrot na liste byl tylko z klawiatury"
    library_actions = {i.action for i in _menu("&Biblioteka").items if not i.is_separator}
    assert Action.VIEW_FOLDERS in library_actions


def test_audio_menu_and_old_entries_survive() -> None:
    titles = [m.title for m in menu_model.build_menus()]
    assert "&Dźwięk" in titles
    library = {i.action for i in _menu("&Biblioteka").items if not i.is_separator}
    for kept in (Action.VIEW_ALL_FILES, Action.VIEW_FAVORITES, Action.VIEW_PLAYLISTS):
        assert kept in library


# ----------------------------------------------------- 3. wejscie i powrot


def test_entering_history_only_orders_the_read() -> None:
    """Jak kazdy widok: listy nie przestawiamy, dopoki dane nie przyjda."""
    nav = Navigator()
    intents = nav.open_library_view(LibraryView.HISTORY)
    assert intents == [OpenLibraryView(view=LibraryView.HISTORY)]
    assert nav.session.library_view is None


def test_history_view_announces_heading_and_size() -> None:
    nav = Navigator()
    rows = [track("7", "Alfa"), track("9", "Beta")]
    out = nav.apply_library_view(
        LibraryView.HISTORY, "Biblioteka — Historia odtwarzania", rows
    )
    assert nav.session.library_view is LibraryView.HISTORY
    assert isinstance(out[0], Announce)
    assert "Historia odtwarzania" in out[0].text
    assert "2 pozycje" in out[0].text


def test_empty_activity_view_says_pusto_instead_of_silence() -> None:
    """Pusta lista nie ma czego wymowic sama -- komunikat jest konieczny."""
    nav = Navigator()
    out = nav.apply_library_view(LibraryView.SAVED_QUEUE, "Biblioteka — Kolejka", [])
    assert "pusto" in out[0].text


def test_saved_queue_heading_does_not_claim_a_running_queue() -> None:
    """Nie wolno sugerowac, ze kolejka silnika zostala uruchomiona."""
    nav = Navigator()
    out = nav.apply_library_view(
        LibraryView.SAVED_QUEUE, "Biblioteka — Zapisana kolejka", [track("1", "A")]
    )
    text = out[0].text.lower()
    assert "zapisana" in text
    assert "odtwarzam" not in text and "uruchomiona" not in text


def test_backspace_leaves_activity_view_back_to_folders() -> None:
    """Widok plaski -> Foldery, jak MainWindow.xaml.cs:20832."""
    nav = Navigator()
    nav.apply_library_view(LibraryView.HISTORY, "h", [track("1", "A")])
    out = nav.go_to_parent()
    assert nav.session.library_view is None
    assert out and out[0].__class__.__name__ == "OpenFolder"


def test_backspace_from_bookmarks_returns_to_the_file_it_came_from() -> None:
    """Powrot z zakladek ma stanac na TYM pliku, nie na pierwszym wierszu."""
    nav = Navigator()
    state = nav.sessions[SessionId.FILES]
    state.model.replace([track("41", "Alfa"), track("42", "Beta")])
    state.model.select_id("42")

    enter = nav.open_item_bookmarks()
    assert enter == [OpenLibraryView(view=LibraryView.ITEM_BOOKMARKS, item_id="42")]
    assert state.library_return_id == "42"

    nav.apply_library_view(
        LibraryView.ITEM_BOOKMARKS, "Biblioteka — Zakładki", [], item_id="42"
    )
    out = nav.go_to_parent()
    assert isinstance(out[0], OpenLibraryView)
    assert out[0].preferred_id == "42", "powrot musi niesc Id pliku"


def test_bookmarks_without_selection_say_so_and_do_not_open() -> None:
    nav = Navigator()
    out = nav.open_item_bookmarks()
    assert isinstance(out[0], Announce)
    assert not any(isinstance(i, OpenLibraryView) for i in out)


# -------------------------------------------------- 4. wykonanie (aktywacja)


def test_history_row_plays_through_the_normal_path() -> None:
    """Enter na wierszu historii = zwykle odtworzenie pliku, bez nowej drogi."""
    nav = Navigator()
    nav.apply_library_view(LibraryView.HISTORY, "h", [track("7", "Alfa")])
    out = nav.activate_selected()
    play = next(i for i in out if isinstance(i, PlayTrack))
    assert play.path == "C:\\m\\Alfa.mp3"
    assert play.position_seconds == 0.0
    assert nav.session.view is View.PLAYER


def test_bookmark_row_sends_the_real_file_not_the_bookmark_id() -> None:
    """``bookmark:<id>`` NIE MOZE pojsc do backendu jako plik.

    ``BookmarkRow.item_id`` jest Id PLIKU, a ``Row.item_id`` to ``bookmark:``.
    Pomylenie ich dalo by backendowi nieistniejaca sciezke.
    """
    nav = Navigator()
    row = Row(
        item_id="bookmark:b1",
        title="Alfa, utworzono 1 maja 2026, 02:03, zakładka",
        kind="track",
    )
    nav.apply_library_view(
        LibraryView.ITEM_BOOKMARKS,
        "Biblioteka — Zakładki",
        [row],
        item_id="42",
        bookmark_targets={"bookmark:b1": ("C:\\m\\Alfa.mp3", 123.5, "Alfa")},
    )
    out = nav.activate_selected()
    play = next(i for i in out if isinstance(i, PlayTrack))
    assert play.path == "C:\\m\\Alfa.mp3"
    assert not play.item_id.startswith("bookmark:") or play.path != "bookmark:b1"
    assert play.position_seconds == 123.5, "zakladka ma przejsc DO POZYCJI"


def test_bookmark_position_keeps_its_fractional_part() -> None:
    """Kontrakt: ``position_ticks / 10_000_000`` z ulamkiem, NIE ``//``."""
    nav = Navigator()
    row = Row(item_id="bookmark:b2", title="Beta, zakładka", kind="track")
    # 83.456 s = 834_560_000 tickow.
    nav.apply_library_view(
        LibraryView.ITEM_BOOKMARKS,
        "z",
        [row],
        item_id="9",
        bookmark_targets={"bookmark:b2": ("C:\\m\\Beta.mp3", 834_560_000 / 10_000_000, "Beta")},
    )
    play = next(i for i in nav.activate_selected() if isinstance(i, PlayTrack))
    assert abs(play.position_seconds - 83.456) < 1e-9


def test_bookmark_of_a_missing_file_does_not_pretend_to_play() -> None:
    """Zakladka moze wskazywac plik, ktorego katalog nie zna (brak filtra)."""
    nav = Navigator()
    row = Row(item_id="bookmark:b3", title="Zniknal, zakładka", kind="track")
    nav.apply_library_view(LibraryView.ITEM_BOOKMARKS, "z", [row], item_id="9")
    out = nav.activate_selected()
    assert not any(isinstance(i, PlayTrack) for i in out)
    assert isinstance(out[0], Announce)
    assert nav.session.view is View.LIST


def test_activating_from_bookmarks_returns_to_the_file_row_on_escape() -> None:
    """Powrot z odtwarzacza trafia tam, skad weszlismy."""
    nav = Navigator()
    row = Row(item_id="bookmark:b1", title="Alfa, zakładka", kind="track")
    nav.apply_library_view(
        LibraryView.ITEM_BOOKMARKS,
        "z",
        [row],
        item_id="42",
        bookmark_targets={"bookmark:b1": ("C:\\m\\Alfa.mp3", 5.0, "Alfa")},
    )
    nav.activate_selected()
    assert nav.session.view is View.PLAYER
    nav.back_to_list()
    assert nav.session.view is View.LIST
    assert nav.session.model.selected_id == "bookmark:b1"
