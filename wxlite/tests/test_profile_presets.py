"""Presety wxPython odbijaja profil do pierwszej prywatnej zmiany."""

from __future__ import annotations

import json
import tempfile
from pathlib import Path

from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.profile_presets import (
    PresetEntry,
    PresetTarget,
    adjacent_entry,
    choices,
    effective_wiim_entries,
    effective_entries,
    entries_payload,
    first_free_slot,
    read_preset_overrides,
    remove_entry,
    replace_entry,
    resolve_preset,
    shortcut_label,
    spotify_entry_target,
    target_from_row,
    wiim_device_preset_key,
    wiim_native_preset_number,
    wiim_native_target,
)
from amc_wx_lite.wiim_source import WiiMPresetChoice
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


def test_wiim_preset_mapping_is_private_per_device_and_defaults_by_number() -> None:
    native = tuple(
        WiiMPresetChoice(
            number=number,
            title="Radio Poznań" if number == 2 else f"Preset {number}",
            source="radio internetowe" if number == 2 else "",
            label=(
                "Preset 2, Radio Poznań, radio internetowe"
                if number == 2 else f"Preset {number}, pusty"
            ),
            occupied=number == 2,
            current=number == 2,
            activation_error="",
        )
        for number in range(1, 13)
    )
    missing_profile = Path(tempfile.mkdtemp()) / "missing.json"

    defaults = effective_wiim_entries(
        missing_profile, "device-1", native, {}
    )
    assert [(entry.slot, wiim_native_preset_number(entry), entry.title)
            for entry in defaults] == [(2, 2, "Radio Poznań")]

    target = wiim_native_target(native[1])
    overrides = {
        wiim_device_preset_key("device-1"): entries_payload(
            replace_entry(defaults, target, 5)
        ),
        wiim_device_preset_key("device-2"): [],
    }
    mapped = effective_wiim_entries(
        missing_profile, "device-1", native, overrides
    )
    other = effective_wiim_entries(
        missing_profile, "device-2", native, overrides
    )

    assert [(entry.slot, wiim_native_preset_number(entry)) for entry in mapped] == [
        (5, 2)
    ]
    assert other == ()
    assert read_preset_overrides(overrides) == overrides


def test_wiim_mapping_rejects_obce_kinds_and_invalid_numbers() -> None:
    native = (
        WiiMPresetChoice(1, "Radio", "radio", "Preset 1, Radio", True, False, ""),
    )
    key = wiim_device_preset_key("device-1")
    overrides = {key: [
        {"slot": 1, "targetId": "native-preset:99", "targetKind": "wiimNativePreset", "targetTitle": "Zły"},
        {"slot": 2, "targetId": "file:x", "targetKind": "track", "targetTitle": "Obcy"},
    ]}

    assert effective_wiim_entries(
        Path(tempfile.mkdtemp()) / "missing.json",
        "device-1",
        native,
        overrides,
    ) == ()


def test_replace_moves_same_target_and_remove_keeps_slot_order() -> None:
    entries = effective_entries(_write(_profile()), SessionId.FILES, {})
    target = PresetTarget("folder:d-test", "folder", "Muzyka", "D:\\Muzyka")

    moved = replace_entry(entries, target, 5)
    assert [entry.slot for entry in moved] == [5, 7]
    assert first_free_slot(moved) == 1
    assert [item["slot"] for item in entries_payload(remove_entry(moved, 7))] == [5]


def test_adjacent_preset_skips_empty_slots_and_wraps() -> None:
    entries = effective_entries(_write(_profile()), SessionId.FILES, {})

    assert adjacent_entry(entries, direction=1, current_slot=1).slot == 7
    assert adjacent_entry(entries, direction=1, current_slot=7).slot == 1
    assert adjacent_entry(entries, direction=-1, current_slot=1).slot == 7


def test_adjacent_preset_recognizes_current_target_after_fresh_start() -> None:
    entries = effective_entries(_write(_profile()), SessionId.FILES, {})
    current = PresetTarget(
        "folder:d-test", "folder", "Muzyka", "D:\\Muzyka"
    )

    assert adjacent_entry(entries, direction=1, current_target=current).slot == 7
    assert adjacent_entry(entries, direction=-1, current_target=None).slot == 7
    assert adjacent_entry((), direction=1) is None


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


def test_sonos_material_is_a_preset_target_but_group_is_not() -> None:
    favorite = target_from_row(
        SessionId.SONOS,
        Row(
            "sonos-favorite:visible",
            "Radio Poznań",
            "sonosFavorite",
            parent_id="house-private",
            service_id="favorite-private",
            service_kind="favorite",
        ),
    )
    group = target_from_row(
        SessionId.SONOS,
        Row(
            "sonos:group-private",
            "Salon",
            "sonosGroup",
            parent_id="house-private",
            service_id="group-private",
            service_kind="sonosGroup",
        ),
    )

    assert favorite == PresetTarget(
        "favorite-private",
        "sonos-favorite",
        "Radio Poznań",
        None,
        sonos_household_id="house-private",
    )
    assert group is None


def test_sonos_preset_keeps_safety_fields_out_of_its_spoken_label() -> None:
    raw = _profile()
    raw["sessionPresets"]["entriesBySession"]["sonos"] = [{
        "slot": 4,
        "targetId": "favorite-private",
        "targetKind": "sonos-favorite",
        "targetTitle": "Radio domowe",
        "sonosHouseholdId": "house-private",
        "sonosFixedPlayerIds": ["player-b", "player-a", "player-a"],
    }]

    entry = resolve_preset(_write(raw), SessionId.SONOS, 4).entry

    assert entry is not None
    assert entry.sonos_household_id == "house-private"
    assert entry.sonos_fixed_player_ids == ("player-a", "player-b")
    assert entries_payload((entry,))[0]["sonosFixedPlayerIds"] == [
        "player-a", "player-b"
    ]
    label = choices((entry,))[3].label
    assert label.endswith("— Radio domowe")
    assert "house-private" not in label
    assert "player-a" not in label


def test_spotify_preset_keeps_catalogue_identity_out_of_spoken_label() -> None:
    row = Row(
        "private-row",
        "Album użytkownika",
        "album",
        service_id="AbC123",
        service_kind="album",
    )

    target = target_from_row(SessionId.SPOTIFY, row)

    assert target == PresetTarget(
        "private-row",
        "spotify-album",
        "Album użytkownika",
        "AbC123",
    )
    assert "AbC123" not in choices((target.as_entry(4),))[3].label
    assert spotify_entry_target(target.as_entry(4)) == ("album", "AbC123")


def test_legacy_spotify_preset_accepts_public_url_without_speaking_it() -> None:
    entry = PresetEntry(
        2,
        "spotify:album:Old123",
        "album",
        "Starszy album",
        "https://open.spotify.com/album/Old123?si=private",
    )

    assert spotify_entry_target(entry) == ("album", "Old123")
    label = choices((entry,))[1].label
    assert "open.spotify.com" not in label
    assert "Old123" not in label


def test_same_opaque_sonos_id_in_another_house_is_a_different_target() -> None:
    entries = (
        PresetTarget(
            "shared-id",
            "sonos-favorite",
            "Dom pierwszy",
            None,
            sonos_household_id="house-1",
        ).as_entry(1),
        PresetTarget(
            "shared-id",
            "sonos-favorite",
            "Dom drugi",
            None,
            sonos_household_id="house-2",
        ).as_entry(2),
    )
    replacement = PresetTarget(
        "shared-id",
        "sonos-favorite",
        "Dom pierwszy nowy",
        None,
        sonos_household_id="house-1",
    )

    updated = replace_entry(entries, replacement, 3)

    assert [(entry.slot, entry.title) for entry in updated] == [
        (2, "Dom drugi"),
        (3, "Dom pierwszy nowy"),
    ]


def test_preset_management_shortcuts_work_in_list_and_player() -> None:
    for table in (LIST_VIEW, PLAYER_VIEW):
        assert table["Ctrl+Alt+P"] is Action.VIEW_PRESETS
        assert table["Ctrl+Alt+Shift+P"] is Action.ASSIGN_PRESET


def test_radioarchiwista_defaults_and_overrides_without_legacy_profile() -> None:
    missing = Path(tempfile.gettempdir()) / "amc-rarch-missing-state-for-presets.json"
    defaults = effective_entries(missing, SessionId.RADIOARCHIWISTA, {})
    assert [(item.slot, item.target_kind, item.location) for item in defaults] == [
        (1, "rarchLatest", None), (2, "rarchStation", "pr1")
    ]
    assert resolve_preset(missing, SessionId.RADIOARCHIWISTA, 2).entry == defaults[1]
    custom = target_from_row(SessionId.RADIOARCHIWISTA, Row(
        "rarch:station:pr3", "Program 3, 123 nagrania", "folder",
        service_kind="rarchStation", service_id="pr3",
    ))
    assert custom is not None
    assert custom.title == "Program 3"
    assert target_from_row(SessionId.RADIOARCHIWISTA, Row(
        "rarch:rec:pr3:hour", "Nagranie", "track", service_kind="rarchRecording"
    )) is None
    overrides = {SessionId.RADIOARCHIWISTA.value: entries_payload((custom.as_entry(2),))}
    assert effective_entries(missing, SessionId.RADIOARCHIWISTA, overrides) == (custom.as_entry(2),)
    assert effective_entries(missing, SessionId.RADIOARCHIWISTA, {SessionId.RADIOARCHIWISTA.value: []}) == ()
    assert "pr3" not in choices((custom.as_entry(2),))[1].label


def test_radioarchiwista_preset_commands_share_shortcuts_help_and_menu() -> None:
    from amc_wx_lite import menu_model
    from amc_wx_lite.shortcuts import RADIOARCHIWISTA_SUPPORTED_ACTIONS, chord_from_canonical, describe, resolve

    supported = set(RADIOARCHIWISTA_SUPPORTED_ACTIONS)
    menus = menu_model.menus_for_session(SessionId.RADIOARCHIWISTA)
    items = {item.action: item for menu in menus for item in menu.items if item.action}
    help_entries = dict(describe("Radio Archiwista", supported))
    for chord, action in (
        ("Ctrl+Alt+P", Action.VIEW_PRESETS),
        ("Ctrl+Alt+Shift+P", Action.ASSIGN_PRESET),
        ("Ctrl+Shift+1", Action.PRESET_1),
        ("Ctrl+Shift+2", Action.PRESET_2),
    ):
        assert action in supported
        assert resolve(chord_from_canonical(chord), player_view=False, radio_session=False, radioarchiwista_session=True) is action
        assert chord in help_entries
    assert items[Action.VIEW_PRESETS].shortcut == "Ctrl+Alt+P"
    assert items[Action.ASSIGN_PRESET].shortcut == "Ctrl+Alt+Shift+P"


def test_radioarchiwista_direct_assignment_shortcuts_are_session_specific() -> None:
    from amc_wx_lite import menu_model
    from amc_wx_lite.shortcuts import (
        ASSIGN_PRESET_ACTIONS, RADIOARCHIWISTA_SUPPORTED_ACTIONS,
        chord_from_canonical, describe, resolve,
    )

    help_entries = dict(describe("Radio Archiwista", set(RADIOARCHIWISTA_SUPPORTED_ACTIONS)))
    menu_actions = {
        item.action: item
        for menu in menu_model.menus_for_session(SessionId.RADIOARCHIWISTA)
        for item in menu.items if item.action
    }
    other_actions = {
        item.action
        for menu in menu_model.menus_for_session(SessionId.SPOTIFY)
        for item in menu.items if item.action
    }
    for slot, action in ASSIGN_PRESET_ACTIONS.items():
        shortcut = f"Ctrl+Alt+Shift+{slot % 10}"
        assert resolve(chord_from_canonical(shortcut), player_view=False,
                       radio_session=False, radioarchiwista_session=True) is action
        assert resolve(chord_from_canonical(shortcut), player_view=True,
                       radio_session=False, radioarchiwista_session=True) is action
        assert action not in other_actions
        assert action in menu_actions
        assert menu_actions[action].shortcut == shortcut
        assert menu_actions[action].needs_radioarchiwista_preset_folder
        assert "Przypisz" in help_entries[shortcut]
