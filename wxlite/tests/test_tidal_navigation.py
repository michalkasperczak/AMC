"""First visible TIDAL session: shortcuts, state and honest activation."""

from __future__ import annotations

from pathlib import Path

from amc_wx_lite import menu_model
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import (
    Announce,
    LibraryView,
    Navigator,
    OpenLibraryView,
    SessionId,
)
from amc_wx_lite.shortcuts import Action, Chord, TIDAL_READ_ONLY_ACTIONS, resolve


def test_ctrl_4_selects_tidal_from_list_and_player() -> None:
    chord = Chord("4", ctrl=True)
    assert resolve(chord, player_view=False, radio_session=False) is Action.SESSION_TIDAL
    assert resolve(chord, player_view=True, radio_session=False) is Action.SESSION_TIDAL

    view = next(menu for menu in menu_model.build_menus() if "Widok" in menu.title)
    item = next(entry for entry in view.items if entry.action is Action.SESSION_TIDAL)
    assert item.label == "Sesja: &TIDAL"
    assert item.shortcut == "Ctrl+4"


def test_tidal_session_keeps_its_own_selection_across_switches() -> None:
    nav = Navigator()
    rows = [
        Row("album-1", "Album pierwszy", "album"),
        Row("album-2", "Album drugi", "album"),
    ]
    nav.apply_tidal_view(LibraryView.TIDAL_LIBRARY, "Biblioteka TIDAL", rows)
    nav.sessions[SessionId.TIDAL].model.select_id("album-2")

    nav.switch_session(SessionId.TIDAL)
    nav.switch_session(SessionId.FILES)
    events = nav.switch_session(SessionId.TIDAL)

    assert nav.session.model.selected_id == "album-2"
    assert isinstance(events[0], Announce)
    assert events[0].text == "TIDAL, lista, Album drugi"


def test_tidal_view_announcement_never_uses_technical_ids() -> None:
    nav = Navigator()
    events = nav.apply_tidal_view(
        LibraryView.TIDAL_FAVORITES,
        "Ulubione TIDAL",
        [Row("opaque-service-id", "Piosenka", "track")],
        order_matches_amc=False,
    )
    assert events == [Announce("Ulubione TIDAL, 1 pozycja, kolejność zastępcza")]
    assert "opaque-service-id" not in events[0].text


def test_tidal_playlist_activation_does_not_open_a_local_playlist() -> None:
    nav = Navigator()
    nav.active = SessionId.TIDAL
    nav.apply_tidal_view(
        LibraryView.TIDAL_PLAYLISTS,
        "Playlisty TIDAL",
        [Row(
            "playlist-service-id",
            "Moja playlista",
            "playlist",
            activation_message="Katalog online nie jest jeszcze podłączony",
        )],
    )

    events = nav.activate_selected()
    assert events == [Announce("Katalog online nie jest jeszcze podłączony")]
    assert not any(isinstance(event, OpenLibraryView) for event in events)


def test_backspace_at_tidal_collection_root_is_silent_and_stays_in_tidal() -> None:
    nav = Navigator()
    nav.active = SessionId.TIDAL
    nav.apply_tidal_view(LibraryView.TIDAL_LIBRARY, "Biblioteka TIDAL", [])
    assert nav.go_to_parent() == []
    assert nav.active is SessionId.TIDAL


def test_read_only_tidal_cannot_trigger_another_sessions_operations() -> None:
    assert {
        Action.ACTIVATE,
        Action.COPY_NAME,
        Action.COPY_ADDRESS,
        Action.FOCUS_FILTER,
        Action.VIEW_LIBRARY,
        Action.VIEW_FAVORITES,
        Action.VIEW_PLAYLISTS,
    } <= TIDAL_READ_ONLY_ACTIONS
    assert {
        Action.SHOW_PLAYER,
        Action.PLAY_PAUSE,
        Action.RECORD_TOGGLE,
        Action.MANAGE_RADIO_SCHEDULES,
        Action.ADD_PODCAST_SOURCE,
        Action.DOWNLOAD_PODCAST_EPISODES,
        Action.REMOVE_SELECTED,
    }.isdisjoint(TIDAL_READ_ONLY_ACTIONS)

    gui = (
        Path(__file__).resolve().parents[1] / "amc_wx_lite" / "gui.py"
    ).read_text(encoding="utf-8")
    assert "action not in TIDAL_READ_ONLY_ACTIONS" in gui
