"""Nawigacja po czterech widokach Biblioteki PODLACZONYCH do GUI.

Warstwa danych (``library_views.py``) jest juz scalona i przetestowana. Tutaj
sprawdzamy to, czego ona nie dotyka: ze GUI potrafi w te widoki WEJSC, ze
czytnik dostaje LISTE I WYBOR (nie surowy ``repr`` modelu) i ze ``Row`` o
rodzaju ``playlist`` prowadzi do NAWIGACJI, a nie do proby odtworzenia jak
pliku.

Skroty i nazwy wziete ze wzorca C#, nie wymyslone:

    ``MainWindow.xaml:426``  ``Header="_Ulubione" InputGestureText="Ctrl+U"``
    ``MainWindow.xaml:427``  ``Header="_Playlisty" InputGestureText="Ctrl+P"``
    ``MainWindow.xaml:458``  ``Header="_Wszystkie pliki alfabetycznie"``
                             ``InputGestureText="Alt+2"``
    ``MainWindow.xaml.cs:65`` ``AllLocalFilesViewName = "Wszystkie pliki"``
    ``MainWindow.xaml.cs:67`` ``PlaylistContentsViewPrefix = "Playlista:"``
    ``MainWindow.xaml.cs:20832`` Backspace = wyjscie o poziom wyzej
"""

from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

from amc_wx_lite.list_model import Row  # noqa: E402
from amc_wx_lite.navigation import (  # noqa: E402
    Announce,
    LibraryView,
    Navigator,
    OpenLibraryView,
    SessionId,
    View,
)
from amc_wx_lite.shortcuts import Action, Chord, resolve  # noqa: E402


def track(item_id: str, title: str) -> Row:
    return Row(item_id=item_id, title=title, kind="track", path=f"C:\\m\\{title}.mp3")


def playlist(item_id: str, title: str, detail: str = "3 pozycje") -> Row:
    return Row(item_id=f"playlist:{item_id}", title=title, kind="playlist", detail=detail)


# ----------------------------------------------------------------- skroty


def test_shortcuts_match_the_regular_app() -> None:
    """Bierzemy rodzine skrotow z C#, nie wymyslamy nowej."""
    assert resolve(Chord("U", ctrl=True), player_view=False, radio_session=False) is (
        Action.VIEW_FAVORITES
    )
    assert resolve(Chord("P", ctrl=True), player_view=False, radio_session=False) is (
        Action.VIEW_PLAYLISTS
    )
    assert resolve(Chord("2", alt=True), player_view=False, radio_session=False) is (
        Action.VIEW_ALL_FILES
    )


def test_existing_shortcuts_are_not_taken_over() -> None:
    """Foldery, Radio i Ctrl+O zostaja -- nowe widoki nic nie zabieraja."""
    keep = {
        Chord("1", ctrl=True): Action.SESSION_FILES,
        Chord("2", ctrl=True): Action.SESSION_RADIO,
        Chord("O", ctrl=True): Action.OPEN_FOLDER_DIALOG,
        Chord("O", ctrl=True, shift=True): Action.OPEN_FILE_DIALOG,
        Chord("C", ctrl=True): Action.COPY_NAME,
        Chord("Back"): Action.PARENT_FOLDER,
    }
    for chord, action in keep.items():
        assert resolve(chord, player_view=False, radio_session=False) is action


def test_library_shortcuts_do_not_fire_in_the_player() -> None:
    """Widoki listy nie maja sensu w odtwarzaczu; Ctrl+P nie moze tam gryzc."""
    for chord in (Chord("U", ctrl=True), Chord("P", ctrl=True), Chord("2", alt=True)):
        assert resolve(chord, player_view=True, radio_session=False) is None


# ------------------------------------------------------------ wejscie w widok


def test_opening_a_view_asks_for_data_and_does_not_invent_rows() -> None:
    nav = Navigator()
    tasks = nav.open_library_view(LibraryView.ALL_FILES)
    assert [type(t) for t in tasks] == [OpenLibraryView]
    assert tasks[0].view is LibraryView.ALL_FILES
    # Samo zlecenie nie przestawia jeszcze listy: dane moga nie dojsc.
    assert nav.sessions[SessionId.FILES].model.rows == []


def test_view_rows_reach_the_reader_as_a_list_and_a_selection() -> None:
    """Czytnik ma dostac NAGLOWEK i liczbe pozycji -- nie powtorzony wiersz.

    Ten test wymagal kiedys tytulu wiersza w zapowiedzi ("czytnik musi
    wiedziec, GDZIE stoi kursor"). Zywy NVDA pokazal, ze kursor opisuje juz
    natywna lista, i to pelniej: z kolumnami oraz "1 z N". Nasza wlasna kopia
    tej informacji dawala DRUGI odczyt tego samego elementu -- zmierzone w
    ``parent-acceptance/odbior-widokow.json``. Wymaganie odwrocone swiadomie;
    zakres zapowiedzi pilnuje teraz ``test_quiet_view_change_speech``.
    """
    nav = Navigator()
    rows = [track("1", "Alfa"), track("2", "Beta")]
    tasks = nav.apply_library_view(LibraryView.ALL_FILES, "Wszystkie pliki", rows)
    said = [t.text for t in tasks if isinstance(t, Announce)]
    assert len(said) == 1
    message = said[0]
    assert "Wszystkie pliki" in message
    assert "2" in message
    assert "Alfa" not in message, "tytul wiersza nalezy do natywnej listy"
    assert "Row(" not in message and "item_id" not in message
    # Wybor w modelu zostaje -- zmienil sie tylko tekst zapowiedzi.
    assert nav.sessions[SessionId.FILES].model.selected_id == "1"
    assert nav.sessions[SessionId.FILES].view is View.LIST


def test_empty_view_says_so_plainly() -> None:
    nav = Navigator()
    tasks = nav.apply_library_view(LibraryView.FAVORITES, "Ulubione", [])
    said = [t.text for t in tasks if isinstance(t, Announce)][0]
    assert "Ulubione" in said and "pusto" in said.lower()


def test_substitute_order_is_admitted_not_hidden() -> None:
    """Bez kluczy hosta kolejnosc jest ZASTEPCZA i mowimy to wprost."""
    nav = Navigator()
    tasks = nav.apply_library_view(
        LibraryView.ALL_FILES, "Wszystkie pliki", [track("1", "Alfa")],
        order_matches_amc=False,
    )
    said = " ".join(t.text for t in tasks if isinstance(t, Announce))
    assert "zastępcza" in said or "zastepcza" in said


# --------------------------------------------------------- playlisty: wejscie


def test_enter_on_a_playlist_navigates_instead_of_playing_it() -> None:
    """Row(kind=playlist) NIE ma sciezki -- Enter wchodzi w zawartosc."""
    nav = Navigator()
    nav.apply_library_view(LibraryView.PLAYLISTS, "Playlisty", [playlist("77", "Poranek")])
    tasks = nav.activate_selected()
    opens = [t for t in tasks if isinstance(t, OpenLibraryView)]
    assert len(opens) == 1
    assert opens[0].view is LibraryView.PLAYLIST_CONTENTS
    assert opens[0].playlist_id == "77"
    # Zadnej proby odtworzenia i zadnego "Brak sciezki pliku".
    assert not any(type(t).__name__ in ("PlayTrack", "PlayStation") for t in tasks)
    assert all("Brak sciezki" not in t.text for t in tasks if isinstance(t, Announce))


def test_playlist_row_is_openable_and_has_a_kind_word_for_the_reader() -> None:
    row = playlist("77", "Poranek")
    assert row.is_openable, "Enter ma wchodzic, nie odtwarzac"
    assert row.kind_label == "playlista", "czytnik czyta kolumne Rodzaj"


def test_backspace_returns_to_playlists_and_restores_the_selection() -> None:
    """Powrot staje na TEJ playliscie, z ktorej weszlismy (MainWindow.xaml.cs:20832)."""
    nav = Navigator()
    nav.apply_library_view(
        LibraryView.PLAYLISTS,
        "Playlisty",
        [playlist("77", "Poranek"), playlist("88", "Wieczór")],
    )
    nav.sessions[SessionId.FILES].model.select_id("playlist:88")
    nav.activate_selected()
    nav.apply_library_view(
        LibraryView.PLAYLIST_CONTENTS, "Playlista — Wieczór", [track("5", "Nokturn")],
        playlist_id="88",
    )

    tasks = nav.go_to_parent()
    opens = [t for t in tasks if isinstance(t, OpenLibraryView)]
    assert len(opens) == 1
    assert opens[0].view is LibraryView.PLAYLISTS
    assert opens[0].preferred_id == "playlist:88", "wracamy na WYBRANA playliste"


def test_vanished_playlist_falls_back_to_the_list_of_playlists() -> None:
    """Dane oddaja fallback_view; GUI ma go uszanowac, nie pokazac pustki."""
    nav = Navigator()
    tasks = nav.apply_library_view(
        LibraryView.PLAYLIST_CONTENTS, "Playlisty", [], playlist_id="99",
        fallback_to_playlists=True,
    )
    opens = [t for t in tasks if isinstance(t, OpenLibraryView)]
    assert opens and opens[0].view is LibraryView.PLAYLISTS


def test_backspace_in_a_flat_view_leaves_it_instead_of_saying_top_level() -> None:
    """W "Wszystkie pliki" nie ma rodzica-folderu, ale Backspace ma wyjsc."""
    nav = Navigator()
    nav.apply_library_view(LibraryView.ALL_FILES, "Wszystkie pliki", [track("1", "Alfa")])
    tasks = nav.go_to_parent()
    assert not any(
        "najwyzszego poziomu" in t.text for t in tasks if isinstance(t, Announce)
    )


# ------------------------------------------------------- wspolistnienie z folderami


def test_folder_browsing_still_works_after_visiting_a_view() -> None:
    """Widoki nie zastepuja Folderow: Ctrl+O i drzewo zostaja czynne."""
    nav = Navigator()
    nav.apply_library_view(LibraryView.FAVORITES, "Ulubione", [track("1", "Alfa")])
    nav.apply_folder("C:\\muzyka", [track("9", "Gamma")])
    state = nav.sessions[SessionId.FILES]
    assert state.library_view is None, "wejscie w folder konczy widok Biblioteki"
    assert state.folder_path == "C:\\muzyka"


def test_radio_session_is_untouched_by_library_views() -> None:
    nav = Navigator()
    nav.apply_stations([Row(item_id="r1", title="Trójka", kind="station", url="http://x")])
    nav.apply_library_view(LibraryView.ALL_FILES, "Wszystkie pliki", [track("1", "Alfa")])
    radio = nav.sessions[SessionId.RADIO]
    assert [r.item_id for r in radio.model.rows] == ["r1"]
