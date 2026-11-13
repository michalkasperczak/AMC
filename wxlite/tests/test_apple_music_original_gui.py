"""Enter plays in AMC; the original app remains an explicit menu action."""

from __future__ import annotations

from contextlib import nullcontext
import sys
from pathlib import Path
from types import SimpleNamespace

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

import amc_wx_lite.gui as gui  # noqa: E402
from amc_wx_lite import menu_model
from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.apple_music_source import records_from_resources
from amc_wx_lite.apple_music_library import AppleMusicLibraryResult
from amc_wx_lite.navigation import (
    Navigator, OpenAppleMusicAlbum, OpenAppleMusicOriginal,
    PlayAppleMusicWeb, SessionId,
)
from amc_wx_lite.shortcuts import Action
from amc_wx_lite.shortcuts import (
    APPLE_MUSIC_SUPPORTED_ACTIONS,
    chord_from_canonical,
    describe_action,
    resolve,
)


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


def test_enter_plays_in_amc_and_menu_opens_original(monkeypatch) -> None:
    record, navigator = _selected_song()
    messages: list[str] = []
    opened = []
    played = []
    closed = []
    owner = SimpleNamespace(
        navigator=navigator,
        _apple_music_records={record.row.item_id: record},
        announcer=SimpleNamespace(say=messages.append),
        _apple_music_handoff=False,
        _apple_music_web_active=False,
        _apple_music_web_client=SimpleNamespace(
            executable=SimpleNamespace(is_file=lambda: True),
            play=lambda song_id: (
                played.append(song_id) or {"handled": True}
            ),
            close=lambda: closed.append(True),
        ),
        runner=SimpleNamespace(
            submit=lambda _name, work, done, _failed: done(work())
        ),
        _refresh_apple_music_external_state=lambda **_kwargs: None,
    )
    monkeypatch.setattr(gui, "apple_music_original_available", lambda value: value is record)
    monkeypatch.setattr(gui, "open_apple_music_original", opened.append)

    intent = navigator.activate_selected()[0]
    assert intent == PlayAppleMusicWeb(record.row.item_id, record.row.title)
    gui.LiteFrame._play_apple_music_web(owner, intent)
    assert played == ["123456"]
    assert owner._apple_music_web_active is True
    gui.LiteFrame._open_apple_music_original(
        owner, OpenAppleMusicOriginal(record.row.item_id, record.row.title)
    )

    assert opened == [record]
    assert closed == [True]
    assert owner._apple_music_handoff is True
    assert owner._apple_music_web_active is False
    assert messages[-1].startswith("Otworzono utwór w oryginalnym Apple Music")
    assert "private.song" not in messages[-1]
    assert "123456" not in messages[-1]


def test_album_enter_loads_tracks_into_apple_music_list(monkeypatch) -> None:
    album, song = records_from_resources([
        {"id": "private.album", "type": "library-albums",
         "attributes": {"name": "Przykładowy album"}},
        {"id": "private.song", "type": "library-songs",
         "attributes": {"name": "Przykładowy utwór",
                        "playParams": {"catalogId": "123456"}}},
    ])
    navigator = Navigator()
    navigator.active = SessionId.APPLE_MUSIC
    navigator.apply_apple_music_view(
        gui.OpenAppleMusicView("albums"), "Albumy Apple Music", [album.row],
    )
    intent = navigator.activate_selected()[0]
    assert intent == OpenAppleMusicAlbum(album.row.item_id, album.row.title)

    class Library:
        def album_tracks_page(self, received, *, cursor=None):
            assert received is album
            assert cursor is None
            return AppleMusicLibraryResult("Album: Przykładowy album", (song,))

    monkeypatch.setattr(gui, "open_apple_music_library", lambda _layout: nullcontext(Library()))
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame.navigator = navigator
    frame._apple_music_records = {album.row.item_id: album}
    frame._apple_music_pages = {}
    frame._apple_music_navigation_version = 0
    frame.layout = None
    frame.announcer = SimpleNamespace(say=lambda _message: None)
    frame.runner = SimpleNamespace(
        submit=lambda _name, work, done, _failed: done(work())
    )
    announcements = []
    frame._run = announcements.extend

    frame._open_apple_music_album(intent)

    assert navigator.session.apple_music_context == "album:" + album.row.item_id
    assert navigator.session.model.selected_row == song.row
    assert frame._apple_music_records[song.row.item_id] is song
    assert announcements[0].text.startswith("Album: Przykładowy album")


def test_menu_and_help_availability_follow_selected_track(monkeypatch) -> None:
    record, navigator = _selected_song()
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame.navigator = navigator
    frame._apple_music_records = {record.row.item_id: record}
    frame._apple_music_web_client = SimpleNamespace(
        executable=SimpleNamespace(is_file=lambda: True),
        is_running=True,
    )
    monkeypatch.setattr(gui, "apple_music_original_available", lambda value: value is record)
    action = Action.APPLE_MUSIC_OPEN_ORIGINAL

    assert frame._action_available_in_active_session(action)
    assert frame._action_available_in_active_session(Action.APPLE_MUSIC_PLAY_WEB)
    assert frame._action_available_in_active_session(Action.APPLE_MUSIC_SHOW_WEB)
    navigator.session.model.replace([])
    assert not frame._action_available_in_active_session(action)
    assert not frame._action_available_in_active_session(Action.APPLE_MUSIC_PLAY_WEB)
    assert frame._action_available_in_active_session(Action.APPLE_MUSIC_SHOW_WEB)


def test_context_menu_has_explicit_user_facing_original_action() -> None:
    record, navigator = _selected_song()

    class Native:
        def __init__(self, label: str) -> None:
            self.label = label

        def IsEnabled(self):  # noqa: N802 - wx API
            return True

        def GetItemLabel(self):  # noqa: N802 - wx API
            return self.label

    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame.navigator = navigator
    frame._refresh_menu_state = lambda: None
    frame._menu_items = [
        (Native(label), menu_model.MenuItem(
            label, action, visible_sessions=(SessionId.APPLE_MUSIC,),
        ))
        for label, action in (
            ("Otwórz utwór w oryginalnym Apple &Music",
             Action.APPLE_MUSIC_OPEN_ORIGINAL),
            ("Odtwórz utwór Apple Music &w AMC",
             Action.APPLE_MUSIC_PLAY_WEB),
        )
    ]
    groups = frame._context_menu_command_groups(player=False)
    labels = [command.label for group in groups for command in group]
    assert labels[:2] == [
        "Otwórz utwór w oryginalnym Apple Music",
        "Odtwórz utwór Apple Music w AMC",
    ]
    assert gui._LIST_CONTEXT_ACTION_GROUPS[0][0] is Action.APPLE_MUSIC_OPEN_ORIGINAL


def test_apple_music_transport_uses_only_apple_session() -> None:
    _record, navigator = _selected_song()
    commands: list[str] = []
    messages: list[str] = []
    refreshes: list[bool] = []
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame.navigator = navigator
    frame.client = SimpleNamespace(
        apple_music_external_transport=lambda command: (
            commands.append(command) or {"handled": True, "message": "Wstrzymano"}
        )
    )
    frame.runner = SimpleNamespace(
        submit=lambda _name, work, done, _failed: done(work())
    )
    frame.announcer = SimpleNamespace(say=messages.append)
    frame._refresh_apple_music_external_state = lambda **_kwargs: refreshes.append(True)

    frame._play_pause()
    frame._queue_step(True)
    frame._queue_step(False)

    assert commands == ["toggle", "next", "previous"]
    assert messages == ["Wstrzymano"] * 3
    assert len(refreshes) == 3
    assert {Action.PLAY_PAUSE, Action.QUEUE_NEXT, Action.QUEUE_PREVIOUS} <= (
        APPLE_MUSIC_SUPPORTED_ACTIONS
    )
    assert "bieżący utwór Apple Music" in describe_action(
        Action.PLAY_PAUSE, "Apple Music"
    )
    for key, action in (
        ("Space", Action.PLAY_PAUSE),
        ("Shift+Next", Action.QUEUE_NEXT),
        ("Shift+Prior", Action.QUEUE_PREVIOUS),
    ):
        chord = chord_from_canonical(key)
        assert chord is not None
        assert resolve(
            chord, player_view=False, radio_session=False,
            apple_music_session=True,
        ) is action
    palette = {entry.action: entry for entry in entries_for_session(
        SessionId.APPLE_MUSIC
    )}
    assert palette[Action.QUEUE_NEXT].label == (
        "Następny utwór Apple Music, Shift+Page Down"
    )
    assert palette[Action.QUEUE_PREVIOUS].label == (
        "Poprzedni utwór Apple Music, Shift+Page Up"
    )


def test_web_playback_transport_does_not_control_original_app() -> None:
    _record, navigator = _selected_song()
    web_commands: list[str] = []
    messages: list[str] = []
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame.navigator = navigator
    frame.client = SimpleNamespace(
        apple_music_external_transport=lambda _command: (
            pytest.fail("original app transport called")
        )
    )
    frame._apple_music_web_active = True
    frame._apple_music_web_client = SimpleNamespace(
        is_running=True,
        transport=lambda command: (
            web_commands.append(command) or
            {"handled": True, "message": "Wstrzymano"}
        ),
    )
    frame.runner = SimpleNamespace(
        submit=lambda _name, work, done, _failed: done(work())
    )
    frame.announcer = SimpleNamespace(say=messages.append)
    frame._refresh_apple_music_external_state = lambda **_kwargs: None

    frame._play_pause()
    frame._queue_step(True)
    assert web_commands == ["toggle", "next"]
    assert messages == ["Wstrzymano", "Wstrzymano"]


def test_apple_music_nvda_status_does_not_claim_selected_song_is_playing() -> None:
    _record, navigator = _selected_song()
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame.navigator = navigator
    frame._apple_music_external_snapshot = {
        "hasSession": True,
        "title": "Prawdziwy utwór w aplikacji Apple",
        "isPlaying": False,
    }
    assert "Prawdziwy utwór w aplikacji Apple, Apple Music, pauza" in (
        frame._nvda_status_message()
    )
    assert frame._nvda_now_playing_message() == "Prawdziwy utwór w aplikacji Apple"
