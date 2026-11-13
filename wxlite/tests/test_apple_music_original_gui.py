"""The Apple Music list, menu and spoken handoff share one action."""

from __future__ import annotations

import sys
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

import amc_wx_lite.gui as gui  # noqa: E402
from amc_wx_lite import menu_model
from amc_wx_lite.apple_music_source import records_from_resources
from amc_wx_lite.navigation import Navigator, OpenAppleMusicOriginal, SessionId
from amc_wx_lite.shortcuts import Action


def _selected_song():
    record = records_from_resources([{
        "id": "private.song",
        "type": "library-songs",
        "attributes": {
            "name": "Przykładowy utwór",
            "playParams": {"catalogId": "123456"},
        },
    }])[0]
    navigator = Navigator()
    navigator.active = SessionId.APPLE_MUSIC
    navigator.session.model.replace([record.row])
    return record, navigator


def test_enter_and_menu_use_same_selected_track(monkeypatch) -> None:
    record, navigator = _selected_song()
    messages: list[str] = []
    opened = []
    owner = SimpleNamespace(
        navigator=navigator,
        _apple_music_records={record.row.item_id: record},
        announcer=SimpleNamespace(say=messages.append),
        _apple_music_handoff=False,
    )
    monkeypatch.setattr(gui, "apple_music_original_available", lambda value: value is record)
    monkeypatch.setattr(gui, "open_apple_music_original", opened.append)

    intent = navigator.activate_selected()[0]
    assert intent == OpenAppleMusicOriginal(record.row.item_id, record.row.title)
    gui.LiteFrame._open_apple_music_original(owner, intent)

    assert opened == [record]
    assert owner._apple_music_handoff is True
    assert messages[-1].startswith("Otworzono utwór w oryginalnym Apple Music")
    assert "private.song" not in messages[-1]
    assert "123456" not in messages[-1]


def test_menu_and_help_availability_follow_selected_track(monkeypatch) -> None:
    record, navigator = _selected_song()
    frame = object.__new__(gui.LiteFrame)
    frame.navigator = navigator
    frame._apple_music_records = {record.row.item_id: record}
    monkeypatch.setattr(gui, "apple_music_original_available", lambda value: value is record)
    action = Action.APPLE_MUSIC_OPEN_ORIGINAL

    assert frame._action_available_in_active_session(action)
    navigator.session.model.replace([])
    assert not frame._action_available_in_active_session(action)


def test_context_menu_has_explicit_user_facing_original_action() -> None:
    record, navigator = _selected_song()

    class Native:
        def IsEnabled(self):  # noqa: N802 - wx API
            return True

        def GetItemLabel(self):  # noqa: N802 - wx API
            return "Otwórz utwór w oryginalnym Apple &Music"

    frame = object.__new__(gui.LiteFrame)
    frame.navigator = navigator
    frame._refresh_menu_state = lambda: None
    frame._menu_items = [(Native(), menu_model.MenuItem(
        "Otwórz utwór w oryginalnym Apple &Music",
        Action.APPLE_MUSIC_OPEN_ORIGINAL,
        visible_sessions=(SessionId.APPLE_MUSIC,),
    ))]
    groups = frame._context_menu_command_groups(player=False)
    labels = [command.label for group in groups for command in group]
    assert labels == ["Otwórz utwór w oryginalnym Apple Music"]
