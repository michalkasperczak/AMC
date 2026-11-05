"""AMC Test native TIDAL command and transport without a live GUI or account."""

from __future__ import annotations

from types import SimpleNamespace

from amc_wx_lite import menu_model
from amc_wx_lite.audio_output import effective_output_device_id
from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.gui import LiteFrame, _LIST_CONTEXT_ACTION_GROUPS
from amc_wx_lite.navigation import Navigator, Row, SessionId, View
from amc_wx_lite.shortcuts import (
    Action, Chord, TIDAL_NATIVE_ONLY_ACTIONS, TIDAL_SUPPORTED_ACTIONS,
    describe, resolve,
)
from amc_wx_lite.state_store import LiteState, StateStore
from amc_wx_lite.tidal_source import native_track_id


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
        "Odtwórz zaznaczony utwór TIDAL w AMC Test z najwyższą dostępną "
        "jakością, do Max; Enter otwiera oryginalny TIDAL",
    ) in describe("TIDAL", {Action.TIDAL_PLAY_NATIVE})
    monkeypatch.setenv("AMC_RELEASE_PROFILE", "public")
    assert Action.TIDAL_PLAY_NATIVE not in _menu_actions()
    assert Action.TIDAL_PLAY_NATIVE not in {
        entry.action for entry in entries_for_session(SessionId.TIDAL)
    }


def test_native_track_id_accepts_real_catalog_ids_and_rejects_other_kinds() -> None:
    assert native_track_id("tracks:123") == "123"
    assert native_track_id("123") == "123"
    for value in ("albums:123", "tracks:", "tracks:abc", "tracks:1/2", None):
        assert native_track_id(value) is None


def test_native_command_is_in_track_context_menu() -> None:
    assert Action.TIDAL_PLAY_NATIVE in _LIST_CONTEXT_ACTION_GROUPS[0]
    assert Action.TIDAL_PLAY_NATIVE in _menu_actions()

    row = Row("tidal:tracks:1", "Utwór", "track", service_id="tracks:1")
    entry = SimpleNamespace(action=Action.TIDAL_PLAY_NATIVE, checkable=False)
    native = SimpleNamespace(IsEnabled=lambda: True, GetItemLabel=lambda: "Odtwórz utwór TIDAL w AMC Test")
    frame = SimpleNamespace(
        navigator=SimpleNamespace(session=SimpleNamespace(
            model=SimpleNamespace(selected_row=row),
        )),
        _menu_items=[(native, entry)],
        _refresh_menu_state=lambda: None,
        _relation_context_menu_commands=lambda: (),
        _context_menu_entry_label=lambda _entry, label: label,
    )
    groups = LiteFrame._context_menu_command_groups(frame, player=False)
    assert groups[0][0].label == "Odtwórz utwór TIDAL w AMC Test"
    assert groups[0][0].action is Action.TIDAL_PLAY_NATIVE


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


def test_native_player_percent_edges_and_volume_match_spotify_surfaces(monkeypatch) -> None:
    player_actions = {
        *(Action[f"SEEK_PERCENT_{percent}"] for percent in range(0, 100, 10)),
        Action.TRACK_START, Action.TRACK_END,
        Action.VOLUME_UP_5, Action.VOLUME_DOWN_5,
        Action.VOLUME_UP_1, Action.VOLUME_DOWN_1,
    }
    assert player_actions <= TIDAL_SUPPORTED_ACTIONS
    assert player_actions <= TIDAL_NATIVE_ONLY_ACTIONS
    assert resolve(Chord("5"), player_view=True, radio_session=False) is Action.SEEK_PERCENT_50
    assert resolve(Chord("Home"), player_view=True, radio_session=False) is Action.TRACK_START
    assert resolve(Chord("End"), player_view=True, radio_session=False) is Action.TRACK_END

    monkeypatch.setenv("AMC_RELEASE_PROFILE", "test")
    assert {Action.TRACK_START, Action.TRACK_END} <= _menu_actions()
    assert player_actions <= {
        entry.action for entry in entries_for_session(SessionId.TIDAL)
    }
    described = describe("TIDAL", player_actions)
    assert len(described) == len(player_actions)
    assert ("5", "Skok do 50% utworu TIDAL w AMC Test") in described
    assert ("End", "Przejdź dziesięć sekund przed końcem utworu TIDAL w AMC Test") in described
    monkeypatch.setenv("AMC_RELEASE_PROFILE", "public")
    assert not player_actions.intersection(_menu_actions())
    assert not player_actions.intersection({
        entry.action for entry in entries_for_session(SessionId.TIDAL)
    })


def test_native_percent_uses_current_tidal_track_and_duration() -> None:
    calls: list[tuple] = []
    announcements: list[tuple] = []
    client = SimpleNamespace(
        status=lambda session: calls.append(("status", session)) or {
            "id": "tidal:123", "durationSeconds": 200,
        },
        seek_to_position=lambda target, session: calls.append(
            ("seek", target, session)
        ) or {"positionSeconds": target},
    )
    frame = SimpleNamespace(
        client=client,
        _tidal_native_active_track_id="123",
        runner=SimpleNamespace(submit=lambda _name, work, done, _failed: done(work())),
        _announce_seek_percent=lambda percent, payload, duration: announcements.append(
            (percent, payload["positionSeconds"], duration)
        ),
    )
    LiteFrame._seek_tidal_native_absolute(frame, percent=50)
    assert calls == [("status", "tidal"), ("seek", 100.0, "tidal")]
    assert announcements == [(50, 100.0, 200.0)]


def test_percent_home_end_and_volume_route_to_native_tidal() -> None:
    seeks: list[dict] = []
    volume_calls: list[tuple[int, str]] = []
    slider = SimpleNamespace(SetValue=lambda value: None)
    frame = SimpleNamespace(
        client=SimpleNamespace(
            set_volume=lambda value, session: volume_calls.append((value, session)),
        ),
        navigator=SimpleNamespace(active=SessionId.TIDAL),
        _tidal_native_has_playback=True,
        _seek_tidal_native_absolute=lambda **kwargs: seeks.append(kwargs),
        volume_slider=slider,
        options=SimpleNamespace(volume=50),
        messages=SimpleNamespace(volume_text=lambda value: f"{value}%", speaks_routine=True),
        announcer=SimpleNamespace(say=lambda _message: None),
        runner=SimpleNamespace(submit=lambda _name, work, _done, _failed: work()),
    )
    frame._set_volume = lambda value: LiteFrame._set_volume(frame, value)
    LiteFrame._seek_percent(frame, 70)
    LiteFrame._seek_to_track_edge(frame, False)
    LiteFrame._seek_to_track_edge(frame, True)
    LiteFrame._adjust_volume(frame, 5)
    assert seeks == [{"percent": 70}, {"to_end": False}, {"to_end": True}]
    assert volume_calls == [(55, "tidal")]


def test_native_absolute_seek_refuses_old_or_unknown_timeline() -> None:
    spoken: list[str] = []
    seeks: list[tuple] = []
    status = {"id": "tidal:other", "durationSeconds": 200}
    frame = SimpleNamespace(
        client=SimpleNamespace(
            status=lambda _session: status,
            seek_to_position=lambda *args: seeks.append(args),
        ),
        _tidal_native_active_track_id="123",
        runner=SimpleNamespace(submit=lambda _name, work, done, _failed: done(work())),
        announcer=SimpleNamespace(say=spoken.append),
        messages=SimpleNamespace(percent_seek_text=lambda *_args, **_kwargs:
            "Skok procentowy niedostępny: czas trwania jest nieznany"),
    )
    LiteFrame._seek_tidal_native_absolute(frame, percent=50)
    assert seeks == []
    assert spoken == ["Utwór TIDAL zmienił się, ponów skok"]
    status.update({"id": "tidal:123", "durationSeconds": 0})
    LiteFrame._seek_tidal_native_absolute(frame, percent=50)
    assert seeks == []
    assert spoken[-1] == "Skok procentowy niedostępny: czas trwania jest nieznany"


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
        Row("tidal:tracks:1", "Pierwszy", "track", service_id="tracks:1"),
        Row("tidal:tracks:2", "Drugi", "track", service_id="tracks:2"),
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
    assert frame._tidal_native_pending_row is rows[0]
    assert state.playback_source_rows == rows
    assert state.playback_uses_queue is False


def test_native_skip_stays_in_amc_adapter() -> None:
    calls: list[tuple[str, bool]] = []
    target = Row("tidal:tracks:2", "Drugi", "track", service_id="tracks:2")
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
    row = Row("tidal:tracks:1", "Utwór", "track", service_id="tracks:1")
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
    assert not LiteFrame._action_available_in_active_session(
        frame, Action.SEEK_PERCENT_50,
    )
    frame._tidal_native_has_playback = True
    assert LiteFrame._action_available_in_active_session(
        frame, Action.SELECT_AUDIO_OUTPUT,
    )
    assert LiteFrame._action_available_in_active_session(
        frame, Action.SEEK_PERCENT_50,
    )


def test_native_start_records_confirmed_tidal_history_for_ctrl_h() -> None:
    nav = Navigator()
    nav.active = SessionId.TIDAL
    row = Row(
        "tidal:tracks:1", "Utwór", "track", service_id="tracks:1",
        related_album_service_id="albums:2", artist_name="Artystka",
    )
    nav.sessions[SessionId.TIDAL].model.replace([row])
    records = []
    spoken = []
    frame = SimpleNamespace(
        navigator=nav,
        _tidal_native_pending_track_id="1",
        _tidal_native_pending_item_id=row.item_id,
        _tidal_native_pending_title=row.title,
        _tidal_native_pending_row=row,
        _playing_session=None,
        _playing_sessions=set(),
        _window_alive=lambda: True,
        _close_tidal_native_challenge=lambda: None,
        _record_streamer_history_row=lambda *args, **kwargs: records.append((args, kwargs)),
        announcer=SimpleNamespace(say=spoken.append),
        _refresh_menu_state=lambda: None,
        _refresh_status=lambda: None,
        _sync_views=lambda: None,
    )
    LiteFrame._on_tidal_native_status(
        frame, "started", "1", "Odtwarzanie TIDAL rozpoczęte. Jakość Max, FLAC 24 bit, 192 kHz."
    )
    assert records == [((SessionId.TIDAL, row), {"replay_supported": True})]
    assert "Jakość Max" in spoken[0]
    assert nav.sessions[SessionId.TIDAL].view is View.PLAYER


def test_native_stop_returns_from_player_to_selected_track_list() -> None:
    nav = Navigator()
    nav.active = SessionId.TIDAL
    state = nav.sessions[SessionId.TIDAL]
    row = Row("tidal:tracks:1", "Utwór", "track", service_id="tracks:1")
    state.model.replace([row])
    state.view = View.PLAYER
    state.now_playing_id = row.item_id
    state.now_playing_title = row.title
    state.current_material_id = row.item_id
    state.playback_source_rows = (row,)
    spoken: list[str] = []
    frame = SimpleNamespace(
        navigator=nav,
        _tidal_native_active_track_id="1",
        _tidal_native_has_playback=True,
        _playing_session=SessionId.TIDAL,
        _playing_sessions={SessionId.TIDAL},
        _window_alive=lambda: True,
        _close_tidal_native_challenge=lambda: None,
        announcer=SimpleNamespace(say=spoken.append),
        _refresh_menu_state=lambda: None,
        _refresh_status=lambda: None,
        _sync_views=lambda: None,
    )
    LiteFrame._on_tidal_native_status(frame, "stopped", "1", "Zatrzymano")
    assert state.view is View.LIST
    assert state.model.selected_id == row.item_id
    assert state.now_playing_id is None
    assert state.playback_source_rows == ()
    assert frame._playing_session is None
    assert not frame._tidal_native_has_playback
    assert spoken == ["Zatrzymano"]


def test_native_time_input_uses_current_track_and_rejects_out_of_range() -> None:
    calls: list[tuple] = []
    spoken: list[str] = []
    status = {"id": "tidal:123", "durationSeconds": 2721}
    client = SimpleNamespace(
        status=lambda session: calls.append(("status", session)) or status,
        seek_to_position=lambda seconds, session: calls.append(("seek", seconds, session)) or {"positionSeconds": seconds},
    )
    frame = SimpleNamespace(
        client=client,
        _tidal_native_active_track_id="123",
        runner=SimpleNamespace(submit=lambda _name, work, done, _failed: done(work())),
        announcer=SimpleNamespace(say=spoken.append),
    )
    LiteFrame._seek_tidal_native_absolute(frame, position_seconds=120)
    assert calls == [("status", "tidal"), ("seek", 120.0, "tidal")]
    assert spoken == ["2:00"]
    LiteFrame._seek_tidal_native_absolute(frame, position_seconds=2722)
    assert calls[-1] == ("status", "tidal")
    assert spoken[-1] == "Podany czas przekracza długość utworu TIDAL"
    status["id"] = "tidal:456"
    LiteFrame._seek_tidal_native_absolute(frame, position_seconds=100)
    assert calls[-1] == ("status", "tidal")
    assert spoken[-1] == "Utwór TIDAL zmienił się, ponów skok"


def test_native_tidal_bookmark_uses_playing_track_id() -> None:
    sent: list[dict] = []
    spoken: list[str] = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(
            active=SessionId.TIDAL,
            session=SimpleNamespace(
                current_material_id=None, pending_material_id=None,
                now_playing_id="tidal:other", now_playing_title="Utwór"),
        ),
        _tidal_native_active_track_id="123",
        _tidal_native_has_playback=True,
        client=SimpleNamespace(add_bookmark=lambda **kwargs: sent.append(kwargs) or {
            "added": True, "positionSeconds": 120,
        }),
        runner=SimpleNamespace(submit=lambda _name, work, done, _failed: done(work())),
        announcer=SimpleNamespace(say=spoken.append),
    )
    LiteFrame._add_bookmark(frame)
    assert sent == [{
        "item_id": "tidal:123", "item_title": "Utwór", "session_id": "tidal", "name": None,
    }]
    assert spoken == ["Dodano zakładkę: 2:00"]
