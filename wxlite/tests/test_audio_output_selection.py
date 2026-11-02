"""Shift+A: stan per sesja, etykiety NVDA i polaczenie z hostem."""

from __future__ import annotations

import json
from pathlib import Path
from types import SimpleNamespace

from amc_wx_lite import menu_model
from amc_wx_lite.audio_output import (
    AudioOutputChoice,
    adjacent_available_choice,
    audio_output_selection_message,
    choices_from_payload,
    effective_output_device_id,
    read_profile_outputs,
)
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.shortcuts import Action, Chord, resolve
from amc_wx_lite.state_store import LiteState, Options, StateStore


def test_shift_a_works_in_the_native_list_and_player() -> None:
    chord = Chord("A", shift=True)
    assert resolve(chord, player_view=False, radio_session=True) is Action.SELECT_AUDIO_OUTPUT
    assert resolve(chord, player_view=True, radio_session=False) is Action.SELECT_AUDIO_OUTPUT


def test_shift_a_uses_the_network_device_picker_for_speaker_sessions() -> None:
    for session, method in (
        (SessionId.WIIM, "_load_wiim_devices"),
        (SessionId.SONOS, "_select_sonos_target"),
        (SessionId.FRONTIER_SMART, "_show_frontier_smart_devices"),
    ):
        calls: list[str] = []
        frame = SimpleNamespace(navigator=SimpleNamespace(active=session))
        setattr(frame, method, lambda method=method: calls.append(method))
        LiteFrame._choose_audio_output(frame)
        assert calls == [method]


def test_shift_a_is_offered_only_with_a_real_picker_in_each_session() -> None:
    for session in (
        SessionId.RADIOARCHIWISTA,
        SessionId.WIIM,
        SessionId.SONOS,
        SessionId.FRONTIER_SMART,
    ):
        menus = menu_model.menus_for_session(session)
        assert any(
            entry.action is Action.SELECT_AUDIO_OUTPUT and entry.shortcut == "Shift+A"
            for menu in menus for entry in menu.items
        ), session
    for key in ("WIIM", "SONOS", "FRONTIER_SMART"):
        assert resolve(
            Chord("A", shift=True), player_view=True, radio_session=False,
            **{f"{key.lower()}_session": True},
        ) is Action.SELECT_AUDIO_OUTPUT


def test_radioarchiwista_shift_a_requests_its_own_local_output_list() -> None:
    calls: list[tuple[str | None, str]] = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.RADIOARCHIWISTA),
        state=SimpleNamespace(audio_output_device_ids_by_session={}),
        _profile_audio_outputs={},
        client=SimpleNamespace(audio_outputs=lambda device, session: calls.append(
            (device, session)
        )),
        runner=SimpleNamespace(submit=lambda _name, work, _done, _failed: work()),
    )
    LiteFrame._choose_audio_output(frame)
    assert calls == [(None, "radioarchiwista")]
    assert effective_output_device_id(
        SessionId.RADIOARCHIWISTA,
        {"radioarchiwista": "salon"},
        {},
    ) == "salon"


def test_resumed_archive_start_is_attributed_to_archive_not_files() -> None:
    started: list[SessionId] = []
    frame = SimpleNamespace(
        _window_alive=lambda: True,
        navigator=SimpleNamespace(
            sessions={SessionId.RADIOARCHIWISTA: SimpleNamespace(
                playback_source_rows=(),
                now_playing_id="recording-1",
                model=SimpleNamespace(selected_row=None),
            )},
            note_playback_started=started.append,
        ),
        _pending_playback_session=None,
        _playing_session=SessionId.FILES,
        _playing_sessions={SessionId.FILES},
        announcer=SimpleNamespace(say=lambda _message: None),
        _refresh_live_queue=lambda: None,
    )
    LiteFrame._handle_engine_event(frame, "playback.started", {
        "engine": "files",
        "sessionId": "radioarchiwista",
        "id": "recording-1",
        "title": "Nagranie",
    })
    assert started == [SessionId.RADIOARCHIWISTA]
    assert frame._playing_sessions == {SessionId.RADIOARCHIWISTA}


def test_space_restarts_only_the_remembered_radio_station_when_host_is_empty() -> None:
    row = SimpleNamespace(
        item_id="station-katedra", kind="station", url="https://example.invalid/radio",
        title="Katedra",
    )
    calls: list[tuple] = []
    tasks: list = []
    client = SimpleNamespace(
        status=lambda session: {"playing": False, "paused": False},
        play_station=lambda url, **options: calls.append((url, options)),
        pause_resume=lambda session: calls.append(("pause", session)),
    )
    frame = SimpleNamespace(
        client=client,
        navigator=SimpleNamespace(
            active=SessionId.RADIO,
            sessions={SessionId.RADIO: SimpleNamespace(
                now_playing_id=row.item_id,
                playback_source_rows=(row,),
                model=SimpleNamespace(rows=()),
            )},
        ),
        options=SimpleNamespace(volume=42),
        runner=SimpleNamespace(submit=lambda _name, work, _done, _failed: tasks.append(work)),
    )
    LiteFrame._play_pause(frame)
    assert tasks[0]() == {"paused": False, "playing": True, "sessionId": "radio"}
    assert calls == [(row.url, {
        "volume": 42, "item_id": row.item_id, "title": row.title,
    })]

    calls.clear()
    client.status = lambda session: {"playing": True, "paused": False}
    client.pause_resume = lambda session: calls.append(("pause", session)) or {"paused": True}
    LiteFrame._play_pause(frame)
    assert tasks[1]() == {"paused": True}
    assert calls == [("pause", "radio")]


def test_brackets_cycle_outputs_in_the_native_list_and_player() -> None:
    assert resolve(
        Chord("[", ctrl=True), player_view=False, radio_session=False
    ) is Action.PREVIOUS_AUDIO_OUTPUT
    assert resolve(
        Chord("]", ctrl=True), player_view=True, radio_session=True
    ) is Action.NEXT_AUDIO_OUTPUT


def test_audio_menu_exposes_shift_a_without_window_accelerator() -> None:
    audio = next(
        menu for menu in menu_model.build_menus()
        if menu.title.replace("&", "") == "Dźwięk"
    )
    item = next(entry for entry in audio.items if entry.action is Action.SELECT_AUDIO_OUTPUT)
    assert item.shortcut == "Shift+A"
    assert item.accelerator is False
    previous = next(
        entry for entry in audio.items if entry.action is Action.PREVIOUS_AUDIO_OUTPUT
    )
    following = next(
        entry for entry in audio.items if entry.action is Action.NEXT_AUDIO_OUTPUT
    )
    assert (previous.shortcut, previous.accelerator) == ("Ctrl+[", False)
    assert (following.shortcut, following.accelerator) == ("Ctrl+]", False)


def test_output_cycle_wraps_and_skips_disconnected_devices() -> None:
    choices = (
        AudioOutputChoice(None, "Domyślne urządzenie systemowe"),
        AudioOutputChoice("denon", "Denon USB", available=False),
        AudioOutputChoice("headphones", "Słuchawki"),
    )
    assert adjacent_available_choice(choices, None, 1).label == "Słuchawki"
    assert adjacent_available_choice(choices, "headphones", 1).device_id is None
    assert adjacent_available_choice(choices, None, -1).label == "Słuchawki"


def test_output_confirmation_is_short_and_does_not_repeat_the_session() -> None:
    assert audio_output_selection_message("Denon USB") == (
        "Urządzenie audio: Denon USB"
    )
    assert audio_output_selection_message(
        "Denon USB", using_default=True
    ) == "Urządzenie audio: Denon USB. Niedostępne, używane domyślne"


def test_profile_local_device_is_mapped_to_the_files_session(tmp_path: Path) -> None:
    path = tmp_path / "state.json"
    path.write_text(json.dumps({
        "settings": {"audio": {"outputDeviceIdsBySession": {
            "local": "denon-usb",
            "radio": "radio-dac",
            "nieznana-sesja": "nie",
        }}, "spotifyLibrespotDeviceName": "spotify-dac"}
    }), encoding="utf-8")

    assert read_profile_outputs(path) == {
        "files": "denon-usb",
        "radio": "radio-dac",
        "spotify": "spotify-dac",
    }


def test_private_default_choice_overrides_a_profile_device() -> None:
    selected = effective_output_device_id(
        SessionId.RADIO,
        {"radio": ""},
        {"radio": "denon-usb"},
    )
    assert selected is None


def test_tidal_output_is_not_saved_before_its_player_supports_selection() -> None:
    assert effective_output_device_id(
        SessionId.TIDAL,
        {"tidal": "technical-device"},
        {"tidal": "profile-device"},
    ) is None


def test_output_choice_uses_only_the_explicit_user_label() -> None:
    choices = choices_from_payload({"devices": [{
        "id": "{technical-endpoint-id}",
        "name": "Denon USB",
        "available": True,
        "technicalObject": "MMDevice { State = Active }",
    }]})

    assert len(choices) == 1
    assert choices[0].label == "Denon USB"
    assert "technical" not in choices[0].label


def test_private_device_choices_survive_restart(tmp_path: Path) -> None:
    store = StateStore(tmp_path)
    store.save(LiteState(audio_output_device_ids_by_session={
        "files": "denon-usb",
        "radio": "",
        "podcasts": "sluchawki",
        "spotify": "spotify-dac",
        "obca": "nie zapisuj",
    }))

    assert store.load().audio_output_device_ids_by_session == {
        "files": "denon-usb",
        "radio": "",
        "podcasts": "sluchawki",
        "spotify": "spotify-dac",
    }


def test_pause_after_output_loss_is_private_and_reaches_the_host_payload(
    tmp_path: Path,
) -> None:
    options = Options(pause_on_audio_output_fallback=True)
    assert options.audio_payload()["pauseOnOutputFallback"] is True
    store = StateStore(tmp_path)
    store.save(LiteState(options=options))
    assert store.load().options.pause_on_audio_output_fallback is True


def test_host_contract_contains_selection_and_runtime_radio_recovery() -> None:
    root = Path(__file__).resolve().parents[2]
    host = (root / "src" / "AccessibleMediaController.LiteHost" / "LiteEngineHandlers.cs").read_text(
        encoding="utf-8"
    )
    radio = (root / "src" / "AccessibleMediaController.Windows" / "Services" / "RadioMediaOutput.cs").read_text(
        encoding="utf-8"
    )

    assert '["audio.selectOutput"]' in host
    assert '"pauseOnOutputFallback"' in host
    assert "ConfigureOutputFallbackBehavior" in radio
    assert "failedDevice && _pauseAfterOutputDeviceFallback" in radio
    assert 'ConfigureOutputForSession("radio")' in host
    assert "OutputDevicePlaybackStopped" in radio
    assert "RestartPlaybackOutput" in radio
    assert "TryReplaceOutput" in radio
    # Ustalona polityka: odłączone wyjście jest zastępowane domyślnym,
    # odtwarzanie trwa dalej, a świadoma pauza pozostaje pauzą.
    assert "outputDeviceId: null" in radio
    assert "remainPaused = _pauseRequested" in radio
    assert "if (!remainPaused)" in radio
    assert "AudioOutputPauseGuard.Play" in radio
