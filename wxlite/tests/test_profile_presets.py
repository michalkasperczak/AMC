"""Presety wxPython sa odbiciem profilu AMC, nie osobna lista."""

from __future__ import annotations

import json
import tempfile
from pathlib import Path

from amc_wx_lite.navigation import SessionId
from amc_wx_lite.profile_presets import resolve_preset, shortcut_label
from amc_wx_lite.shortcuts import (
    LIST_VIEW,
    PLAYER_VIEW,
    PRESET_ACTIONS,
    preset_slot,
)


def _profile() -> dict:
    return {
        "sessionPresets": {
            "entriesBySession": {
                "radio": [
                    {
                        "slot": 2,
                        "targetId": "r2",
                        "targetKind": "station",
                        "targetTitle": "Stara nazwa",
                    },
                    {
                        "slot": 1,
                        "targetId": "r1",
                        "targetKind": "station",
                        "targetTitle": "Pierwsza",
                    },
                ],
                "local": [
                    {
                        "slot": 1,
                        "targetId": "folder:d-test",
                        "targetKind": "folder",
                        "targetTitle": "Muzyka",
                        "targetLocation": "D:\\Muzyka",
                    },
                    {
                        "slot": 7,
                        "targetId": "playlist:p1",
                        "targetKind": "amcPlaylist",
                        "targetTitle": "Ulubiona playlista",
                        "targetLocation": "p1",
                    },
                ],
            }
        },
        "radio": {
            "stations": [
                {"id": "r1", "name": "Radio Jeden", "streamUrl": "https://r1.invalid"},
                {"id": "r2", "name": "Nowa nazwa", "streamUrl": "https://r2.invalid"},
            ]
        },
    }


def _write(raw: dict) -> Path:
    directory = Path(tempfile.mkdtemp(prefix="amc-presets-"))
    path = directory / "state.json"
    path.write_text(json.dumps(raw, ensure_ascii=False), encoding="utf-8")
    return path


def test_radio_preset_uses_current_station_name_and_slot_order() -> None:
    result = resolve_preset(_write(_profile()), SessionId.RADIO, 2)

    assert result.entry is not None
    assert result.radio_target is not None
    assert result.radio_target.title == "Nowa nazwa"
    assert [target.slot for target in result.radio_sequence] == [1, 2]
    assert [target.item_id for target in result.radio_sequence] == ["r1", "r2"]


def test_missing_station_makes_existing_radio_preset_unavailable() -> None:
    raw = _profile()
    raw["radio"]["stations"] = [raw["radio"]["stations"][0]]
    result = resolve_preset(_write(raw), SessionId.RADIO, 2)

    assert result.entry is not None
    assert result.radio_target is None


def test_local_folder_and_playlist_keep_their_real_targets() -> None:
    path = _write(_profile())
    folder = resolve_preset(path, SessionId.FILES, 1).entry
    playlist = resolve_preset(path, SessionId.FILES, 7).entry

    assert folder is not None and folder.location == "D:\\Muzyka"
    assert folder.target_kind == "folder"
    assert playlist is not None and playlist.location == "p1"
    assert playlist.target_kind == "amcPlaylist"


def test_all_twelve_shortcuts_work_in_list_and_player() -> None:
    labels = {**{slot: str(slot) for slot in range(1, 10)}, 10: "0", 11: "-", 12: "="}
    for slot, key in labels.items():
        chord = f"Ctrl+Shift+{key}"
        assert LIST_VIEW[chord] is PRESET_ACTIONS[slot]
        assert PLAYER_VIEW[chord] is PRESET_ACTIONS[slot]
        assert preset_slot(PRESET_ACTIONS[slot]) == slot


def test_spoken_shortcut_labels_never_expose_internal_names() -> None:
    assert shortcut_label(10, spoken=True) == "0"
    assert shortcut_label(11, spoken=True) == "minus"
    assert shortcut_label(12, spoken=True) == "znak równości"
