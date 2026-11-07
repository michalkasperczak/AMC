"""One Delete command across the playlist, history and help surfaces."""

from __future__ import annotations

from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.menu_model import build_menus
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.shortcuts import (
    Action, Chord, command_presentation, describe, describe_action, resolve,
)


def _menu_items(session: SessionId):
    return [
        item for menu in build_menus() for item in menu.items
        if item.action is Action.REMOVE_SELECTED
        and (not item.visible_sessions or session in item.visible_sessions)
    ]


def test_radio_playlist_delete_routes_to_shared_remove_command():
    assert resolve(
        Chord("Delete"), player_view=False, radio_session=True,
        radio_playlist_contents=True,
    ) is Action.REMOVE_SELECTED
    assert resolve(
        Chord("Delete"), player_view=False, radio_session=True,
        radio_playlist_contents=False,
    ) is Action.STATION_DELETE
    assert any(item.needs_radio_playlist_entry and item.shortcut == "Delete"
               for item in _menu_items(SessionId.RADIO))


def test_tidal_playlist_delete_has_matching_menu_palette_and_help():
    menu = next(item for item in _menu_items(SessionId.TIDAL)
                if item.needs_tidal_playlist_entry)
    palette = next(entry for entry in entries_for_session(SessionId.TIDAL)
                   if entry.action is Action.REMOVE_SELECTED)
    shared = command_presentation(Action.REMOVE_SELECTED)
    assert menu.shortcut == palette.shortcut == shared.shortcut == "Delete"
    assert palette.name == shared.name
    assert describe_action(Action.REMOVE_SELECTED, "TIDAL") == shared.description
    assert any("Delete" in shortcut and shared.description == label
               for shortcut, label in describe("TIDAL", {Action.REMOVE_SELECTED}))
    assert resolve(
        Chord("Delete"), player_view=False, radio_session=False,
        tidal_session=True,
    ) is Action.REMOVE_SELECTED


def test_service_history_delete_has_contextual_menu_in_each_session():
    for session in (SessionId.TIDAL, SessionId.WIIM, SessionId.SONOS,
                    SessionId.FRONTIER_SMART, SessionId.RADIOARCHIWISTA):
        assert any(item.needs_service_history_item and item.shortcut == "Delete"
                   for item in _menu_items(session))


def test_spotify_playlist_delete_has_menu_palette_and_help():
    assert any(item.needs_spotify_saved_item and item.shortcut == "Delete"
               for item in _menu_items(SessionId.SPOTIFY))
    palette = next(entry for entry in entries_for_session(SessionId.SPOTIFY)
                   if entry.action is Action.REMOVE_SELECTED)
    shared = command_presentation(Action.REMOVE_SELECTED)
    assert palette.shortcut == shared.shortcut == "Delete"
    assert palette.name == shared.name
    assert describe_action(Action.REMOVE_SELECTED, "SPOTIFY") == shared.description
