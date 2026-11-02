"""Regresje po pierwszym odbiorze Radio Archiwisty w AMC Test."""

from __future__ import annotations

from pathlib import Path
from types import SimpleNamespace

from amc_wx_lite import menu_model
from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.gui import LiteFrame, copied_address_message
from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import LibraryView, Navigator, PlayMedia, SessionId, View
from amc_wx_lite.playback_activity import activity_rows
from amc_wx_lite.radioarchiwista_source import ArchiveRecording, RadioarchiwistaSource
from amc_wx_lite.shortcuts import (
    Action, RADIOARCHIWISTA_SUPPORTED_ACTIONS, chord_from_canonical,
    describe_action, readable_shortcut, resolve,
)


def recording(item_id: str) -> ArchiveRecording:
    return ArchiveRecording(
        station_slug="pr1", station_name="Program 1", item_id=item_id,
        title="08:00", audio_url=f"https://archive.org/download/{item_id}.mp3",
        duration_seconds=3600, local_date_label="30.10.2026",
        local_hour_label="08:00", local_label="30.10.2026 08:00",
        utc_started_at="2026-10-30T07:00:00Z", hour=8,
        completeness_ratio=1.0, status="published",
    )


def test_archive_shortcuts_route_in_list_and_player() -> None:
    expected = {
        "F5": Action.RADIOARCHIWISTA_REFRESH,
        "Ctrl+Shift+U": Action.TOGGLE_RADIOARCHIWISTA_FAVORITE,
        "Ctrl+Shift+C": Action.COPY_ADDRESS,
        "Ctrl+Q": Action.VIEW_SAVED_QUEUE,
        "Ctrl+L": Action.VIEW_LIBRARY,
        "Ctrl+Shift+Q": Action.ADD_TO_QUEUE,
        "Ctrl+Shift+Return": Action.TOGGLE_PLAY_NEXT,
        "Space": Action.PLAY_PAUSE,
    }
    for player in (False, True):
        for key, action in expected.items():
            assert resolve(
                chord_from_canonical(key), player_view=player, radio_session=False,
                radioarchiwista_session=True,
            ) is action
            assert action in RADIOARCHIWISTA_SUPPORTED_ACTIONS
    for digit in range(10):
        action = Action[f"SEEK_PERCENT_{digit * 10}"]
        assert resolve(
            chord_from_canonical(str(digit)), player_view=True, radio_session=False,
            radioarchiwista_session=True,
        ) is action
        assert action in RADIOARCHIWISTA_SUPPORTED_ACTIONS
    assert Action.ADD_BOOKMARK in RADIOARCHIWISTA_SUPPORTED_ACTIONS
    assert Action.NEXT_BOOKMARK in RADIOARCHIWISTA_SUPPORTED_ACTIONS
    for key, action in (
        ("B", Action.ADD_BOOKMARK),
        ("Ctrl+Shift+B", Action.ADD_NAMED_BOOKMARK),
        ("Shift+Prior", Action.PREVIOUS_BOOKMARK),
        ("Shift+Next", Action.NEXT_BOOKMARK),
    ):
        assert resolve(
            chord_from_canonical(key), player_view=True, radio_session=False,
            radioarchiwista_session=True,
        ) is action
        assert action in RADIOARCHIWISTA_SUPPORTED_ACTIONS


def test_archive_queue_menu_and_copy_label() -> None:
    entries = {
        item.action: item
        for menu in menu_model.menus_for_session(SessionId.RADIOARCHIWISTA)
        for item in menu.items if item.action is not None
    }
    assert entries[Action.VIEW_SAVED_QUEUE].shortcut == "Ctrl+Q"
    assert entries[Action.VIEW_LIBRARY].shortcut == "Ctrl+L"
    assert entries[Action.ADD_TO_QUEUE].shortcut == "Ctrl+Shift+Q"
    assert entries[Action.TOGGLE_PLAY_NEXT].shortcut == "Ctrl+Shift+Return"
    assert entries[Action.COPY_ADDRESS].shortcut == "Ctrl+Shift+C"
    assert "link do nagrania" in entries[Action.COPY_ADDRESS].label
    assert "link do nagrania" in describe_action(
        Action.COPY_ADDRESS, "Radio Archiwista"
    )
    assert "lokalną kolejkę" in describe_action(
        Action.VIEW_SAVED_QUEUE, "Radio Archiwista"
    )
    palette = {item.action: item for item in entries_for_session(SessionId.RADIOARCHIWISTA)}
    for action in (Action.VIEW_LIBRARY, Action.VIEW_SAVED_QUEUE,
                   Action.ADD_TO_QUEUE, Action.TOGGLE_PLAY_NEXT, Action.COPY_ADDRESS):
        assert palette[action].shortcut == readable_shortcut(entries[action].shortcut)
        assert palette[action].name == entries[action].label.replace("&", "")
    row = Row("rarch:rec:a", "08:00", "recording", url="https://archive.org/a.mp3")
    assert row.address == "https://archive.org/a.mp3"
    assert copied_address_message(row) == "Skopiowano link do nagrania"


def test_archive_player_context_menu_contains_copy_link() -> None:
    recording_row = Row(
        "rarch:rec:playing", "Grające", "recording",
        url="https://archive.org/playing.mp3",
    )
    entry = next(
        item
        for menu in menu_model.menus_for_session(SessionId.RADIOARCHIWISTA)
        for item in menu.items
        if item.action is Action.COPY_ADDRESS
    )
    native = SimpleNamespace(
        IsEnabled=lambda: True,
        GetItemLabel=lambda: "Skopiuj &link do nagrania\tCtrl+Shift+C",
    )
    frame = LiteFrame.__new__(LiteFrame)
    frame.navigator = SimpleNamespace(
        session=SimpleNamespace(model=SimpleNamespace(selected_row=recording_row)),
    )
    frame._menu_items = [(native, entry)]
    frame._refresh_menu_state = lambda: None

    groups = frame._context_menu_command_groups(player=True)
    labels = [command.label for group in groups for command in group]

    assert "Skopiuj link do nagrania (Ctrl+Shift+C)" in labels


def test_archive_bookmark_reaches_shared_writer() -> None:
    messages: list[str] = []
    requests: list[dict] = []

    class Runner:
        @staticmethod
        def submit(_name, work, done, _failed) -> None:
            done(work())

    state = SimpleNamespace(
        current_material_id="rarch:rec:playing",
        pending_material_id="",
        now_playing_title="Nagranie 08:00",
    )
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.RADIOARCHIWISTA, session=state),
        client=SimpleNamespace(add_bookmark=lambda **args: (
            requests.append(args) or {"added": True, "positionSeconds": 120}
        )),
        runner=Runner(),
        announcer=SimpleNamespace(say=messages.append),
    )

    LiteFrame._add_bookmark(frame)

    assert requests == [{
        "item_id": "rarch:rec:playing",
        "item_title": "Nagranie 08:00",
        "session_id": "radioarchiwista",
        "name": None,
    }]
    assert messages == ["Dodano zakładkę: 2:00"]


def test_archive_queue_persists_and_reorders(tmp_path: Path) -> None:
    source = RadioarchiwistaSource(tmp_path)
    first, second, third = (recording(f"rarch:rec:{number}") for number in range(3))
    assert source.toggle_queue(first)
    assert source.toggle_queue(second)
    assert source.toggle_queue(third, play_next=True, current_id=first.item_id)
    reopened = RadioarchiwistaSource(tmp_path)
    assert [row.item_id for row in reopened.queue_rows()] == [
        first.item_id, third.item_id, second.item_id,
    ]
    assert reopened.recording_by_id(third.item_id) is not None
    assert not reopened.toggle_queue(third)
    assert [row.item_id for row in reopened.queue_rows()] == [first.item_id, second.item_id]


def test_archive_queue_continues_with_remote_url() -> None:
    navigator = Navigator()
    navigator.active = SessionId.RADIOARCHIWISTA
    rows = [
        Row(f"rarch:rec:{number}", str(number), "recording",
            url=f"https://archive.org/{number}.mp3")
        for number in range(2)
    ]
    navigator.apply_radioarchiwista_rows(LibraryView.RADIOARCHIWISTA_QUEUE, rows)
    navigator.sessions[SessionId.RADIOARCHIWISTA].model.select_id(rows[0].item_id)
    assert any(isinstance(intent, PlayMedia) for intent in navigator.activate_selected())
    next_items = navigator.continue_radioarchiwista_queue_after_end()
    play = next(intent for intent in next_items if isinstance(intent, PlayMedia))
    assert play.source == rows[1].url
    assert navigator.continue_radioarchiwista_queue_after_end() == []


def test_archive_page_down_uses_remote_recording_source() -> None:
    navigator = Navigator()
    navigator.active = SessionId.RADIOARCHIWISTA
    rows = [
        Row(f"rarch:rec:{number}", str(number), "recording",
            url=f"https://archive.org/{number}.mp3")
        for number in range(2)
    ]
    navigator.apply_radioarchiwista_rows(LibraryView.RADIOARCHIWISTA_HOURS, rows)
    navigator.sessions[SessionId.RADIOARCHIWISTA].model.select_id(rows[0].item_id)
    navigator.activate_selected()
    intents = navigator.step_playback_source(True)
    assert intents is not None
    play = next(intent for intent in intents if isinstance(intent, PlayMedia))
    assert play.source == rows[1].url


def test_archive_left_reads_duration_without_file_size_or_bitrate() -> None:
    messages: list[str] = []
    row = Row("rarch:rec:a", "08:00", "recording",
              url="https://archive.org/a.mp3", duration_ticks=3600 * 10_000_000)
    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.RADIOARCHIWISTA,
            session=SimpleNamespace(model=SimpleNamespace(selected_row=row)),
        ),
        announcer=SimpleNamespace(say=messages.append),
    )
    LiteFrame._announce_quick_information(frame)
    assert messages == ["Czas: 1:00:00"]


def test_archive_ctrl_l_returns_to_full_station_list() -> None:
    calls: list[str] = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.RADIOARCHIWISTA),
        _open_radioarchiwista_stations=lambda: calls.append("stations"),
    )
    LiteFrame._return_to_library(frame)
    assert calls == ["stations"]


def test_archive_recording_has_spoken_playback_state() -> None:
    row = Row("rarch:rec:a", "08:00", "recording",
              url="https://archive.org/a.mp3")
    playing = activity_rows([row], current_item_id=row.item_id,
                            playing=True, paused=False)
    assert playing[0].playback_state == "Odtwarzane"
    paused = activity_rows(playing, current_item_id=row.item_id,
                           playing=True, paused=True)
    assert paused[0].playback_state == "Wstrzymane"


def test_archive_player_copies_link_of_playing_recording() -> None:
    playing = Row("rarch:rec:playing", "Grające", "recording",
                  url="https://archive.org/playing.mp3")
    stale = Row("rarch:rec:stale", "Zaznaczone dawniej", "recording",
                url="https://archive.org/stale.mp3")
    copied: list[str] = []
    spoken: list[str] = []
    state = SimpleNamespace(
        view=View.PLAYER, now_playing_id=playing.item_id,
        playback_source_rows=(playing, stale),
        model=SimpleNamespace(selected_row=stale),
    )
    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.RADIOARCHIWISTA, view=View.PLAYER,
            sessions={SessionId.RADIOARCHIWISTA: state},
        ),
        _selected_action_rows=lambda: [stale],
        _file_drop_path=lambda _address: None,
        _clear_pending_external_moves=lambda: None,
        _to_clipboard=lambda text, **_kwargs: copied.append(text) or True,
        announcer=SimpleNamespace(say=spoken.append),
    )
    LiteFrame._copy_address(frame)
    assert copied == [playing.url]
    assert spoken == ["Skopiowano link do nagrania"]
