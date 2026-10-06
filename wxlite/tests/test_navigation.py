"""Testy nawigacji: Enter/Escape/F6, dwie sesje, zapis i odtworzenie stanu.

Te testy pilnuja ZACHOWANIA uzgodnionego z pelnym AMC, nie wygladu okna.
"""

from __future__ import annotations

from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import (
    Announce,
    Navigator,
    OpenFolder,
    PlayStation,
    PlayTrack,
    SessionId,
    View,
)
from amc_wx_lite.shortcuts import Action, Chord, resolve


def folder_rows(parent: str | None = "/m") -> list[Row]:
    rows: list[Row] = []
    if parent:
        rows.append(Row(item_id=f"parent:{parent}", title="..", kind="parent", path=parent))
    rows += [
        Row(item_id="dir:/m/muzyka/album", title="album", kind="folder", path="/m/muzyka/album"),
        Row(item_id="file:/m/muzyka/a.mp3", title="a", kind="track", path="/m/muzyka/a.mp3"),
        Row(item_id="file:/m/muzyka/b.mp3", title="b", kind="track", path="/m/muzyka/b.mp3"),
    ]
    return rows


def ready_files_navigator() -> Navigator:
    nav = Navigator()
    nav.apply_folder("/m/muzyka", folder_rows())
    return nav


def test_enter_on_folder_asks_to_open_it_not_to_play() -> None:
    nav = ready_files_navigator()
    nav.session.model.select_id("dir:/m/muzyka/album")
    tasks = nav.activate_selected()
    assert any(isinstance(t, OpenFolder) and t.path == "/m/muzyka/album" for t in tasks)
    assert not any(isinstance(t, PlayTrack) for t in tasks)
    assert nav.view is View.LIST, "wejscie w folder NIE przechodzi do odtwarzacza"


def test_enter_on_track_plays_and_moves_to_player_view() -> None:
    nav = ready_files_navigator()
    nav.session.model.select_id("file:/m/muzyka/b.mp3")
    tasks = nav.activate_selected()
    play = next(t for t in tasks if isinstance(t, PlayTrack))
    assert play.path == "/m/muzyka/b.mp3"
    assert nav.view is View.PLAYER


def test_escape_returns_to_same_list_and_same_selection() -> None:
    # To jest wymaganie wprost: Escape wraca do TEJ SAMEJ listy i zaznaczenia.
    nav = ready_files_navigator()
    nav.session.model.select_id("file:/m/muzyka/b.mp3")
    nav.activate_selected()
    assert nav.view is View.PLAYER

    nav.back_to_list()

    assert nav.view is View.LIST
    assert nav.session.model.selected_id == "file:/m/muzyka/b.mp3"
    assert nav.session.folder_path == "/m/muzyka"


def test_escape_restores_selection_even_if_user_moved_it_in_player() -> None:
    nav = ready_files_navigator()
    nav.session.model.select_id("file:/m/muzyka/a.mp3")
    nav.activate_selected()
    # Cos przestawilo wybor w modelu (np. odswiezenie w tle).
    nav.session.model.select_id("file:/m/muzyka/b.mp3")

    nav.back_to_list()

    assert nav.session.model.selected_id == "file:/m/muzyka/a.mp3", "Escape wraca do kotwicy"


def test_f6_requires_something_playing() -> None:
    nav = ready_files_navigator()
    tasks = nav.show_player()
    assert nav.view is View.LIST
    assert any(isinstance(t, Announce) and "Nic" in t.text for t in tasks)


def test_f6_switches_to_player_and_back() -> None:
    nav = ready_files_navigator()
    nav.session.model.select_id("file:/m/muzyka/a.mp3")
    nav.activate_selected()
    nav.back_to_list()
    assert nav.view is View.LIST

    nav.show_player()
    assert nav.view is View.PLAYER, "F6 wraca do grajacego utworu"
    nav.show_player()
    assert nav.view is View.LIST, "F6 w odtwarzaczu wraca na liste (MainWindow 20776)"


def test_backspace_returns_to_parent_standing_on_left_folder() -> None:
    nav = ready_files_navigator()
    nav.session.model.select_id("dir:/m/muzyka/album")
    nav.activate_selected()  # wchodzimy do album
    nav.apply_folder("/m/muzyka/album", [Row(item_id="parent:/m/muzyka", title="..", kind="parent", path="/m/muzyka")])

    tasks = nav.go_to_parent()

    task = next(t for t in tasks if isinstance(t, OpenFolder))
    assert task.path == "/m/muzyka"
    assert task.preferred_id == "dir:/m/muzyka/album", "stajemy na folderze, z ktorego wyszlismy"


def test_backspace_at_top_level_says_so_without_crashing() -> None:
    nav = Navigator()
    nav.apply_folder("C:\\", folder_rows(parent=None))
    tasks = nav.go_to_parent()
    assert any(isinstance(t, Announce) for t in tasks)


def test_sessions_keep_separate_lists_and_selections() -> None:
    nav = ready_files_navigator()
    nav.session.model.select_id("file:/m/muzyka/b.mp3")

    nav.switch_session(SessionId.RADIO)
    nav.apply_stations([Row(item_id="st-1", title="Radio 1", kind="station", url="http://a/1")])
    assert nav.active is SessionId.RADIO
    assert nav.session.model.selected_id == "st-1"

    nav.switch_session(SessionId.FILES)
    assert nav.session.model.selected_id == "file:/m/muzyka/b.mp3", "sesja plikow nietknieta"
    assert nav.session.folder_path == "/m/muzyka"

    nav.switch_session(SessionId.RADIO)
    assert nav.session.model.selected_id == "st-1", "lista stacji przetrwala zmiane sesji"


def test_switching_to_same_session_only_announces_name() -> None:
    # Zwyczaj AMC: ponowne wejscie w te sama sesje nie przeladowuje listy.
    nav = ready_files_navigator()
    nav.session.model.select_id("file:/m/muzyka/a.mp3")
    tasks = nav.switch_session(SessionId.FILES)
    assert len(tasks) == 1 and isinstance(tasks[0], Announce)
    assert nav.session.model.selected_id == "file:/m/muzyka/a.mp3"


def test_enter_on_station_plays_it() -> None:
    nav = Navigator()
    nav.switch_session(SessionId.RADIO)
    nav.apply_stations([Row(item_id="st-1", title="Radio 1", kind="station", url="http://a/1")])
    tasks = nav.activate_selected()
    play = next(t for t in tasks if isinstance(t, PlayStation))
    assert play.url == "http://a/1"
    assert nav.view is View.PLAYER


def test_playback_failure_returns_from_player_to_list() -> None:
    nav = ready_files_navigator()
    nav.session.model.select_id("file:/m/muzyka/a.mp3")
    nav.activate_selected()
    tasks = nav.note_playback_failed("Nie moge odtworzyc pliku")
    assert nav.view is View.LIST, "po bledzie nie zostawiamy pustego odtwarzacza"
    assert nav.session.model.selected_id == "file:/m/muzyka/a.mp3"
    assert any(isinstance(t, Announce) for t in tasks)


def test_snapshot_round_trip_restores_folder_and_wish_for_selection() -> None:
    nav = ready_files_navigator()
    nav.session.model.select_id("file:/m/muzyka/b.mp3")
    nav.switch_session(SessionId.RADIO)
    nav.apply_stations([Row(item_id="st-9", title="R", kind="station", url="http://a/9")])

    snapshot = nav.snapshot()
    fresh = Navigator()
    fresh.restore(snapshot)

    assert fresh.active is SessionId.RADIO
    assert fresh.sessions[SessionId.FILES].folder_path == "/m/muzyka"
    assert fresh.sessions[SessionId.FILES].list_anchor_id == "file:/m/muzyka/b.mp3"


def test_restore_always_starts_on_list_not_on_empty_player() -> None:
    nav = ready_files_navigator()
    nav.session.model.select_id("file:/m/muzyka/a.mp3")
    nav.activate_selected()
    assert nav.view is View.PLAYER
    snapshot = nav.snapshot()

    fresh = Navigator()
    fresh.restore(snapshot)
    assert fresh.view is View.LIST, "po restarcie nic nie gra, wiec odtwarzacz bylby pusty"


def test_restore_tolerates_garbage_without_raising() -> None:
    nav = Navigator()
    nav.restore({"active": "nieistniejaca", "sessions": {"files": {"folderPath": 17, "breadcrumb": "zle"}}})
    assert nav.active is SessionId.FILES
    assert nav.sessions[SessionId.FILES].folder_path is None
    nav.restore({})


def test_restored_selection_is_applied_when_folder_reloads() -> None:
    nav = Navigator()
    nav.restore({"active": "files", "sessions": {"files": {"folderPath": "/m/muzyka", "selectedId": "file:/m/muzyka/b.mp3"}}})
    anchor = nav.sessions[SessionId.FILES].list_anchor_id
    nav.apply_folder("/m/muzyka", folder_rows(), preferred_id=anchor)
    assert nav.session.model.selected_id == "file:/m/muzyka/b.mp3"


def test_restored_selection_that_vanished_falls_back_quietly() -> None:
    nav = Navigator()
    nav.restore({"active": "files", "sessions": {"files": {"folderPath": "/m/muzyka", "selectedId": "file:/m/muzyka/usuniety.mp3"}}})
    anchor = nav.sessions[SessionId.FILES].list_anchor_id
    nav.apply_folder("/m/muzyka", folder_rows(), preferred_id=anchor)
    assert nav.session.model.selected_id == "dir:/m/muzyka/album", "pierwszy element nie bedacy rodzicem"


# ------------------------------------------------------------------ skroty


def test_arrows_in_list_view_belong_to_the_native_control() -> None:
    # Kluczowe dla czytnika ekranu: strzalki na liscie NIE sa przejmowane.
    # WYJATEK oryginalu: LEWA bez modyfikatora czyta krotka informacje
    # (MainWindow.xaml.cs:23206-23216). Lista ma jedna kolumne, wiec w lewo
    # nie ma po czym chodzic -- pionowe strzalki zostaja kontrolce.
    for key in ("Up", "Down", "Right", "Home", "End", "Prior", "Next", "Tab"):
        assert resolve(Chord(key), player_view=False, radio_session=False) is None, key
    assert (
        resolve(Chord("Left"), player_view=False, radio_session=False)
        is Action.QUICK_INFORMATION
    )


def test_player_view_arrows_control_volume_and_seeking() -> None:
    assert resolve(Chord("Up"), player_view=True, radio_session=False) is Action.VOLUME_UP_5
    assert resolve(Chord("Right"), player_view=True, radio_session=False) is Action.SEEK_FORWARD_10
    # Shift = 30 s w warstwie OKNA (MainWindow.xaml.cs:21586, 22390). Dawne
    # 60 s pod Shift pochodzilo z profilu po akordzie prefiksu, czyli z innej
    # warstwy; 60 s jest teraz pod Ctrl, zgodnie z oryginalem.
    assert resolve(Chord("Right", shift=True), player_view=True, radio_session=False) is Action.SEEK_FORWARD_30
    assert resolve(Chord("Right", ctrl=True), player_view=True, radio_session=False) is Action.SEEK_FORWARD_60


def test_shortcuts_match_amc_sources() -> None:
    # Ctrl+cyfra = sesja (MainWindow.xaml.cs:21449)
    assert resolve(Chord("1", ctrl=True), player_view=False, radio_session=False) is Action.SESSION_FILES
    # Spacja = pauza (MainWindow.xaml.cs:21687)
    assert resolve(Chord("Space"), player_view=False, radio_session=False) is Action.PLAY_PAUSE
    # Escape z odtwarzacza = lista (MainWindow.xaml.cs:20792)
    assert resolve(Chord("Escape"), player_view=True, radio_session=False) is Action.SHOW_LIST
    # Shift+F6 = powrot na liste (MainWindow.xaml.cs:20775)
    assert resolve(Chord("F6", shift=True), player_view=True, radio_session=False) is Action.SHOW_LIST
    # Backspace = folder nadrzedny (MainWindow.xaml.cs:20832)
    assert resolve(Chord("Back"), player_view=False, radio_session=False) is Action.PARENT_FOLDER
    # Czasy to Ctrl+SHIFT+E/R/T w warstwie OKNA (MainWindow.xaml.cs:21674-21676,
    # 22190-22192). KeyboardProfile.cs:70-72 wiaze z nimi samo Ctrl+E/R/T, ale
    # to profil czytany PO AKORDZIE PREFIKSU -- w oknie Ctrl+E nalezy do
    # eksportu ulubionych stacji (cs:21662), wiec aliasu tam byc nie moze.
    assert resolve(Chord("E", ctrl=True, shift=True), player_view=True, radio_session=False) is Action.TIME_ELAPSED
    assert resolve(Chord("E", ctrl=True), player_view=True, radio_session=False) is None


def test_station_management_keys_only_in_radio_session() -> None:
    assert resolve(Chord("Delete"), player_view=False, radio_session=True) is Action.STATION_DELETE
    assert resolve(Chord("Delete"), player_view=False, radio_session=False) is None


def test_escape_na_liscie_wychodzi_o_poziom_wyzej() -> None:
    """Escape na liscie = ``NavigateToParentLevel`` (cs:20827 -> cs:22562).

    Test nazywal sie wczesniej \"does nothing harmful\" i wymagal ``None``.
    To nie byl brak szkody, tylko brak funkcji: w oryginale ten klawisz
    wychodzi o poziom wyzej.
    """
    assert resolve(Chord("Escape"), player_view=False, radio_session=False) is Action.PARENT_FOLDER
    # ``back_to_list`` nadal nie ma nic do roboty, gdy juz jestesmy na liscie.
    nav = ready_files_navigator()
    assert nav.back_to_list() == []
