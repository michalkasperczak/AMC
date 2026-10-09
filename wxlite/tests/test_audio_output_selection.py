"""Shift+A: stan per sesja, etykiety NVDA i polaczenie z hostem."""

from __future__ import annotations

import json
from pathlib import Path

from amc_wx_lite import menu_model
from amc_wx_lite.audio_output import (
    choices_from_payload,
    effective_output_device_id,
    read_profile_outputs,
)
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.shortcuts import Action, Chord, resolve
from amc_wx_lite.state_store import LiteState, StateStore


def test_shift_a_works_in_the_native_list_and_player() -> None:
    chord = Chord("A", shift=True)
    assert resolve(chord, player_view=False, radio_session=True) is Action.SELECT_AUDIO_OUTPUT
    assert resolve(chord, player_view=True, radio_session=False) is Action.SELECT_AUDIO_OUTPUT


def test_audio_menu_exposes_shift_a_without_window_accelerator() -> None:
    audio = next(menu for menu in menu_model.build_menus() if "Dźwięk" in menu.title)
    item = next(entry for entry in audio.items if entry.action is Action.SELECT_AUDIO_OUTPUT)
    assert item.shortcut == "Shift+A"
    assert item.accelerator is False


def test_profile_local_device_is_mapped_to_the_files_session(tmp_path: Path) -> None:
    path = tmp_path / "state.json"
    path.write_text(json.dumps({
        "settings": {"audio": {"outputDeviceIdsBySession": {
            "local": "denon-usb",
            "radio": "radio-dac",
            "nieznana-sesja": "nie",
        }}}
    }), encoding="utf-8")

    assert read_profile_outputs(path) == {
        "files": "denon-usb",
        "radio": "radio-dac",
    }


def test_private_default_choice_overrides_a_profile_device() -> None:
    selected = effective_output_device_id(
        SessionId.RADIO,
        {"radio": ""},
        {"radio": "denon-usb"},
    )
    assert selected is None


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
        "obca": "nie zapisuj",
    }))

    assert store.load().audio_output_device_ids_by_session == {
        "files": "denon-usb",
        "radio": "",
        "podcasts": "sluchawki",
    }


def test_host_contract_contains_selection_and_runtime_radio_recovery() -> None:
    root = Path(__file__).resolve().parents[2]
    host = (root / "src" / "AccessibleMediaController.LiteHost" / "LiteEngineHandlers.cs").read_text(
        encoding="utf-8"
    )
    radio = (root / "src" / "AccessibleMediaController.Windows" / "Services" / "RadioMediaOutput.cs").read_text(
        encoding="utf-8"
    )

    assert '["audio.selectOutput"]' in host
    assert 'ConfigureOutputForSession("radio")' in host
    assert "OutputDevicePlaybackStopped" in radio
    assert "RestartPlaybackOutput" in radio
    assert "TryReplaceOutput" in radio
