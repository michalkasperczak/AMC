"""One-step Podcast/YouTube Favorites undo: command parity and GUI handoff."""

from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub

install_wx_stub()

import amc_wx_lite.gui as gui
from amc_wx_lite import command_palette, menu_model
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import LibraryView, SessionId, View
from amc_wx_lite.shortcuts import Action, Chord, keyboard_help_announcement, resolve


def _submit(_name, work, done, failed):
    try:
        done(work())
    except Exception as error:
        failed(error)


def test_ctrl_z_is_podcast_command_in_menu_palette_and_help():
    action = Action.UNDO_PODCAST_FAVORITE
    assert resolve(Chord("Z", ctrl=True), player_view=False,
                   radio_session=False, podcast_session=True) is action
    assert resolve(Chord("Z", ctrl=True), player_view=False,
                   radio_session=False, podcast_session=False) is None
    menus = menu_model.menus_for_session(SessionId.PODCASTS)
    items = [item for menu in menus for item in menu.items if item.action is action]
    assert len(items) == 1 and items[0].shortcut == "Ctrl+Z"
    assert not any(item.action is action for menu in
                   menu_model.menus_for_session(SessionId.FILES)
                   for item in menu.items)
    entry = next(entry for entry in command_palette.entries_for_session(
        SessionId.PODCASTS) if entry.action is action)
    assert entry.shortcut == "Ctrl+Z" and "Cofnij" in entry.name
    assert "ostatnio" in keyboard_help_announcement(
        Chord("Z", ctrl=True), action, "Podcasty i YouTube", available=True)


def test_remove_favorite_then_undo_restores_selected_id_and_consumes_token():
    row = Row("episode-2", "Drugi odcinek", "episode")
    state = SimpleNamespace(view=View.LIST,
                            library_view=LibraryView.PODCAST_FAVORITES)
    calls = []
    refresh = []
    spoken = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.PODCASTS,
                                  sessions={SessionId.PODCASTS: state}),
        client=SimpleNamespace(
            toggle_podcast_favorites=lambda subs, eps: (
                calls.append(("toggle", subs, eps)) or
                {"favorite": False, "changed": 1, "undoToken": "token-1"}),
            undo_podcast_favorite=lambda token: (
                calls.append(("undo", token)) or {"changed": 1}),
        ),
        runner=SimpleNamespace(submit=_submit),
        announcer=SimpleNamespace(say=spoken.append),
        _podcast_favorite_pending=False,
        _podcast_favorite_undo_token=None,
        _selected_action_rows=lambda: [row],
        _reload_podcast_list_after_download=lambda item_id, message: (
            refresh.append((item_id, message)) or True),
    )
    gui.LiteFrame._toggle_podcast_favorite(frame)
    assert calls == [("toggle", [], ["episode-2"])]
    assert frame._podcast_favorite_undo_token == "token-1"
    assert gui.LiteFrame._action_available_in_active_session(
        frame, Action.UNDO_PODCAST_FAVORITE)
    gui.LiteFrame._undo_podcast_favorite(frame)
    assert calls[-1] == ("undo", "token-1")
    assert refresh[-1] == ("episode-2", "Przywrócono poprzedni stan Ulubionych")
    assert frame._podcast_favorite_undo_token is None
    assert not gui.LiteFrame._action_available_in_active_session(
        frame, Action.UNDO_PODCAST_FAVORITE)
    gui.LiteFrame._undo_podcast_favorite(frame)
    assert calls.count(("undo", "token-1")) == 1
    assert spoken[-1] == "Nie ma zmiany Ulubionych do cofnięcia"
