"""Presety wxPython odbijaja profil do pierwszej prywatnej zmiany."""

from __future__ import annotations

import json
import tempfile
from pathlib import Path

from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.profile_presets import (
    PresetTarget,
    choices,
    effective_entries,
    entries_payload,
    first_free_slot,
    read_preset_overrides,
    remove_entry,
    replace_entry,
    resolve_preset,
    shortcut_label,
    target_from_row,
)
from amc_wx_lite.shortcuts import (
    Action,
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


def test_private_override_wins_and_empty_override_stays_intentionally_empty() -> None:
    path = _write(_profile())
    override = {
        "files": [
            {
                "slot": 3,
                "targetId": "dir:D:\\Nowe",
                "targetKind": "folder",
                "targetTitle": "Nowe",
                "targetLocation": "D:\\Nowe",
            }
        ],
        "radio": [],
    }

    local = effective_entries(path, SessionId.FILES, override)
    radio = effective_entries(path, SessionId.RADIO, override)

    assert [(entry.slot, entry.title) for entry in local] == [(3, "Nowe")]
    assert radio == ()
    assert read_preset_overrides(override)["radio"] == []


def test_private_radio_preset_uses_current_private_station_data() -> None:
    override = {
        "radio": [{
            "slot": 3,
            "targetId": "private-radio",
            "targetKind": "station",
            "targetTitle": "Stara nazwa",
            "targetLocation": "https://stary.invalid",
        }]
    }

    resolved = resolve_preset(
        _write(_profile()),
        SessionId.RADIO,
        3,
        overrides=override,
        current_stations=[{
            "id": "private-radio",
            "name": "Aktualna nazwa",
            "url": "https://aktualny.invalid",
        }],
    )

    assert resolved.radio_target is not None
    assert resolved.radio_target.title == "Aktualna nazwa"
    assert resolved.radio_target.url == "https://aktualny.invalid"


def test_override_is_strictly_sanitized_without_runtime_representations() -> None:
    raw = {
        "files": [
            {"slot": True, "targetId": "bad", "targetKind": "folder"},
            {"slot": 1, "targetId": object(), "targetKind": "folder"},
            {
                "slot": 1,
                "targetId": "dir:D:\\Muzyka",
                "targetKind": "folder",
                "targetTitle": object(),
                "targetLocation": 123,
            },
            {"slot": 1, "targetId": "duplicate", "targetKind": "folder"},
        ],
        "obca-sesja": [],
    }

    assert read_preset_overrides(raw) == {
        "files": [{
            "slot": 1,
            "targetId": "dir:D:\\Muzyka",
            "targetKind": "folder",
            "targetTitle": "",
        }]
    }


def test_replace_moves_same_target_and_remove_keeps_slot_order() -> None:
    entries = effective_entries(_write(_profile()), SessionId.FILES, {})
    target = PresetTarget("folder:d-test", "folder", "Muzyka", "D:\\Muzyka")

    moved = replace_entry(entries, target, 5)
    assert [entry.slot for entry in moved] == [5, 7]
    assert first_free_slot(moved) == 1
    assert [item["slot"] for item in entries_payload(remove_entry(moved, 7))] == [5]


def test_choice_labels_are_explicit_user_text_for_all_twelve_slots() -> None:
    entries = effective_entries(_write(_profile()), SessionId.FILES, {})
    labels = [choice.label for choice in choices(entries)]

    assert len(labels) == 12
    assert labels[0].endswith("— Muzyka")
    assert labels[1].endswith("— pusty")
    assert labels[10].startswith("Preset numer -, klawisz minus")
    assert "PresetEntry" not in " ".join(labels)
    assert "target_id" not in " ".join(labels)


def test_visible_rows_become_only_targets_that_wx_can_activate() -> None:
    station = target_from_row(
        SessionId.RADIO,
        Row("r1", "Radio", "station", url="https://radio.invalid"),
    )
    playlist = target_from_row(
        SessionId.FILES,
        Row("playlist:p1", "Lista", "playlist"),
    )
    track = target_from_row(
        SessionId.FILES,
        Row("m1", "Utwór", "track", path="D:\\Muzyka\\utwor.flac"),
    )

    assert station is not None and station.target_kind == "station"
    assert playlist is not None and playlist.location == "p1"
    assert track is not None and track.target_kind == "track"
    assert target_from_row(SessionId.FILES, Row("parent:", "..", "parent")) is None


def test_preset_management_shortcuts_work_in_list_and_player() -> None:
    for table in (LIST_VIEW, PLAYER_VIEW):
        assert table["Ctrl+Alt+P"] is Action.VIEW_PRESETS
        assert table["Ctrl+Alt+Shift+P"] is Action.ASSIGN_PRESET
