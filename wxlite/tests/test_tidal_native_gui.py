"""AMC Test native TIDAL command and transport without a live GUI or account."""

from __future__ import annotations

from types import SimpleNamespace

from amc_wx_lite import menu_model
from amc_wx_lite.audio_output import effective_output_device_id
from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.navigation import Row, SessionId
from amc_wx_lite.shortcuts import (
    Action, Chord, TIDAL_NATIVE_ONLY_ACTIONS, describe, resolve,
)
from amc_wx_lite.state_store import LiteState, StateStore


def _menu_actions() -> set[Action]:
    return {
        item.action for menu in menu_model.menus_for_session(SessionId.TIDAL)
        for item in menu.items if item.action is not None
    }


def test_native_command_has_one_test_profile_surface_and_no_public_surface(
    monkeypatch,
) -> None:
    monkeypatch.setenv("AMC_RELEASE_PROFILE", "test")
    actions = _menu_actions()
    assert Action.TIDAL_PLAY_NATIVE in actions
    assert Action.TIDAL_STOP_NATIVE in actions
    entries = entries_for_session(SessionId.TIDAL)
    native = next(entry for entry in entries if entry.action is Action.TIDAL_PLAY_NATIVE)
    assert native.name == "Odtwórz utwór TIDAL w AMC Test (eksperymentalnie)"
    assert native.shortcut is None
    assert (
        "Bez skrótu",
        "Odtwórz zaznaczony utwór TIDAL w odtwarzaczu AMC Test; "
        "Enter nadal otwiera oryginalny TIDAL",
    ) in describe("TIDAL", {Action.TIDAL_PLAY_NATIVE})
    monkeypatch.setenv("AMC_RELEASE_PROFILE", "public")
    assert Action.TIDAL_PLAY_NATIVE not in _menu_actions()
    assert Action.TIDAL_PLAY_NATIVE not in {
        entry.action for entry in entries_for_session(SessionId.TIDAL)
    }


def test_native_transport_keys_use_tidal_session_without_stealing_list_arrows() -> None:
    assert resolve(
        Chord("Left", shift=True), player_view=False,
        radio_session=False, tidal_session=True,
    ) is Action.SEEK_BACK_30
    assert resolve(
        Chord("Right", ctrl=True), player_view=False,
        radio_session=False, tidal_session=True,
    ) is Action.SEEK_FORWARD_60
    assert resolve(
        Chord("Left"), player_view=False,
        radio_session=False, tidal_session=True,
    ) is not Action.SEEK_BACK_10
    assert Action.SELECT_AUDIO_OUTPUT in TIDAL_NATIVE_ONLY_ACTIONS
    assert Action.TIDAL_STOP_NATIVE in TIDAL_NATIVE_ONLY_ACTIONS


def test_external_output_choice_is_unchanged_and_native_choice_is_separate() -> None:
    values = {"tidal": "technical-device"}
    assert effective_output_device_id(SessionId.TIDAL, values, values) is None
    assert effective_output_device_id(
        SessionId.TIDAL, values, {}, native_tidal=True,
    ) == "technical-device"


def test_native_output_choice_roundtrips_only_in_test_profile(monkeypatch) -> None:
    store = StateStore("unused-test-state")
    state = LiteState(audio_output_device_ids_by_session={
        "tidal": "chosen-local-endpoint",
    })
    monkeypatch.setenv("AMC_RELEASE_PROFILE", "test")
    payload = store.to_payload(state)
    assert payload["audio_output_device_ids_by_session"]["tidal"] == "chosen-local-endpoint"
    assert store.load_payload(payload).audio_output_device_ids_by_session["tidal"] == "chosen-local-endpoint"
    monkeypatch.setenv("AMC_RELEASE_PROFILE", "public")
    assert "tidal" not in store.to_payload(state)["audio_output_device_ids_by_session"]
    assert "tidal" not in store.load_payload(payload).audio_output_device_ids_by_session


def test_selected_track_starts_adapter_and_captures_its_list_for_skip() -> None:
    started: list[tuple[str, str]] = []
    rows = (
        Row("tidal:tracks:1", "Pierwszy", "track", service_id="1"),
        Row("tidal:tracks:2", "Drugi", "track", service_id="2"),
    )
    state = SimpleNamespace(
        model=SimpleNamespace(rows=rows), playback_source_rows=(),
        playback_uses_queue=True,
    )
    frame = SimpleNamespace(
        _tidal_native_adapter=SimpleNamespace(
            play=lambda track_id, title: started.append((track_id, title)) or True,
        ),
        client=object(),
        announcer=SimpleNamespace(say=lambda _message: None),
        navigator=SimpleNamespace(sessions={SessionId.TIDAL: state}),
    )
    LiteFrame._play_tidal_native_row(frame, rows[0])
    assert started == [("1", "Pierwszy")]
    assert frame._tidal_native_pending_item_id == rows[0].item_id
    assert state.playback_source_rows == rows
    assert state.playback_uses_queue is False


def test_native_skip_stays_in_amc_adapter() -> None:
    calls: list[tuple[str, bool]] = []
    target = Row("tidal:tracks:2", "Drugi", "track", service_id="2")
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.TIDAL),
        _tidal_native_has_playback=True,
        _tidal_native_adjacent_row=lambda forward: target if forward else None,
        _play_tidal_native_row=lambda row, preserve_source=False: calls.append(
            (row.item_id, preserve_source)
        ),
        announcer=SimpleNamespace(say=lambda _message: None),
    )
    LiteFrame._queue_step(frame, True)
    assert calls == [(target.item_id, True)]


def test_space_and_shift_a_route_native_only_when_active() -> None:
    calls: list[tuple[str, str]] = []
    client = SimpleNamespace(
        pause_resume=lambda session: calls.append(("pause", session)) or {"paused": True},
        audio_outputs=lambda _device, session: calls.append(("outputs", session)),
    )
    runner = SimpleNamespace(submit=lambda _name, work, _done, _failed: work())
    frame = SimpleNamespace(
        client=client, runner=runner,
        navigator=SimpleNamespace(active=SessionId.TIDAL),
        _tidal_native_has_playback=True,
        state=SimpleNamespace(audio_output_device_ids_by_session={}),
        _profile_audio_outputs={},
        announcer=SimpleNamespace(say=lambda _message: None),
    )
    LiteFrame._play_pause(frame)
    LiteFrame._choose_audio_output(frame)
    assert calls == [("pause", "tidal"), ("outputs", "tidal")]


def test_native_seek_and_status_use_tidal_not_files_session() -> None:
    calls: list[tuple] = []
    client = SimpleNamespace(
        seek_by=lambda seconds, session: calls.append(("seek", seconds, session)),
        status=lambda session: calls.append(("status", session)),
    )
    frame = SimpleNamespace(
        client=client,
        runner=SimpleNamespace(submit=lambda _name, work, _done, _failed: work()),
        navigator=SimpleNamespace(active=SessionId.TIDAL),
        _playing_sessions={SessionId.TIDAL},
        _tidal_native_has_playback=True,
        _refresh_radio_transport_status=lambda: None,
    )
    LiteFrame._seek(frame, 30.0)
    LiteFrame._refresh_status(frame)
    assert calls == [("seek", 30.0, "tidal"), ("status", "tidal")]


def test_context_help_disables_native_commands_for_external_tidal() -> None:
    row = Row("tidal:tracks:1", "Utwór", "track", service_id="1")
    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.TIDAL,
            session=SimpleNamespace(model=SimpleNamespace(selected_row=row)),
        ),
        _tidal_native_adapter=object(),
        _tidal_native_has_playback=False,
    )
    assert LiteFrame._action_available_in_active_session(
        frame, Action.TIDAL_PLAY_NATIVE,
    )
    assert not LiteFrame._action_available_in_active_session(
        frame, Action.SELECT_AUDIO_OUTPUT,
    )
    frame._tidal_native_has_playback = True
    assert LiteFrame._action_available_in_active_session(
        frame, Action.SELECT_AUDIO_OUTPUT,
    )
