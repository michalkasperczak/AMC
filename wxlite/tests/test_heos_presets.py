"""HEOS presets retain station identity and never follow a shifted list index."""

from types import SimpleNamespace

import pytest

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.heos_client import HeosPage
from amc_wx_lite.heos_presets import reference_for_entry, target_for_station
from amc_wx_lite.heos_service import HeosPresetUnavailable, HeosService
from amc_wx_lite.heos_source import HeosTarget, catalog_items
from amc_wx_lite.menu_model import menus_for_session
from amc_wx_lite.navigation import SessionId, View
from amc_wx_lite.profile_presets import (
    effective_entries, read_preset_overrides, resolve_preset,
)
from amc_wx_lite.shortcuts import Action, Chord, describe, resolve


def station(name: str, media_id: str):
    return {"name": name, "mid": media_id, "type": "station",
            "container": "no", "playable": "yes"}


def test_heos_preset_reference_uses_media_identity_and_selected_device():
    target = HeosTarget("device", "Głośnik", "player-1")
    first = catalog_items("favorites", "", [station("Stacja A", "media-1")])[0]
    reordered = catalog_items("favorites", "", [
        station("Inna", "media-2"), station("Stacja A", "media-1"),
    ])[1]
    original = target_for_station(target, first)
    after_reorder = target_for_station(target, reordered)
    assert original is not None and after_reorder is not None
    assert original.target_id == after_reorder.target_id
    assert original.title == "Stacja A"
    assert reference_for_entry(original.as_entry(2), target).media_id == "media-1"
    assert reference_for_entry(
        original.as_entry(2), HeosTarget("other", "Inny", "player-2")
    ) is None


def test_heos_preset_rechecks_current_catalog_before_playing():
    sent = []
    client = SimpleNamespace(
        browse_page=lambda sid, cid, *, start, limit: HeosPage(
            (station("Inna", "media-2"), station("Nowa nazwa", "media-1")),
            2, False, 2,
        ),
        play_preset=lambda pid, position: sent.append((pid, position)),
    )
    service = HeosService(client)
    target = HeosTarget("device", "Głośnik", "player-1")
    saved = target_for_station(target, catalog_items(
        "1028", "", [station("Stara nazwa", "media-1")]
    )[0])
    reference = reference_for_entry(saved.as_entry(1), target)
    assert service.play_preset_station(reference) == "Nowa nazwa"
    assert sent == [("player-1", 2)]

    client.browse_page = lambda sid, cid, *, start, limit: HeosPage(
        (station("Inna", "media-2"),), 1, False, 1,
    )
    with pytest.raises(HeosPresetUnavailable):
        service.play_preset_station(reference)
    assert len(sent) == 1


def test_heos_presets_start_empty_without_legacy_profile_and_keep_private_entries(tmp_path):
    missing_profile = tmp_path / "no-legacy-profile.json"
    assert effective_entries(missing_profile, SessionId.HEOS, {}) == ()
    assert resolve_preset(missing_profile, SessionId.HEOS, 1, overrides={}).entry is None
    target = HeosTarget("device", "Głośnik", "player-1")
    item = catalog_items("favorites", "", [station("Stacja", "media-1")])[0]
    entry = target_for_station(target, item).as_entry(1)
    overrides = {SessionId.HEOS.value: [entry.to_payload()]}
    assert read_preset_overrides(overrides) == overrides
    assert effective_entries(missing_profile, SessionId.HEOS, overrides) == (entry,)
    assert resolve_preset(
        missing_profile, SessionId.HEOS, 1, overrides=overrides
    ).entry == entry


def test_heos_preset_assignment_uses_selected_station_and_command_surfaces():
    target = HeosTarget("device", "Głośnik", "player-1")
    item = catalog_items("favorites", "", [station("Stacja", "media-1")])[0]
    state = SimpleNamespace(selected_target=target, items=(item,))
    session = SimpleNamespace(
        view=View.LIST,
        model=SimpleNamespace(selected_row=SimpleNamespace(item_id=item.item_id)),
    )
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.HEOS, session=session),
        _heos_state=state,
    )
    assert LiteFrame._current_preset_target(frame) == target_for_station(target, item)
    session.view = View.PLAYER
    assert LiteFrame._current_preset_target(frame) is None

    actions = {
        item.action for menu in menus_for_session(SessionId.HEOS, ["heos"])
        for item in menu.items
    }
    assert {Action.VIEW_PRESETS, Action.ASSIGN_PRESET} <= actions
    palette = {
        entry.action for entry in entries_for_session(SessionId.HEOS, ["heos"])
    }
    assert {Action.VIEW_PRESETS, Action.ASSIGN_PRESET} <= palette
    help_keys = {shortcut for shortcut, _label in describe("HEOS", actions)}
    assert {"Ctrl+Alt+P", "Ctrl+Alt+Shift+P"} <= help_keys
    assert resolve(
        Chord("Ctrl+Shift+1"), player_view=False, radio_session=False,
        heos_session=True,
    ) is Action.PRESET_1
    assert resolve(
        Chord("Ctrl+Alt+Shift+1"), player_view=False, radio_session=False,
        heos_session=True,
    ) is Action.ASSIGN_PRESET_1


def test_heos_preset_activation_uses_saved_identity_and_current_name(tmp_path):
    target = HeosTarget("device", "Głośnik", "player-1")
    item = catalog_items("favorites", "", [station("Stara nazwa", "media-1")])[0]
    entry = target_for_station(target, item).as_entry(1)
    sent, spoken = [], []

    def submit(_name, work, done, failed):
        try:
            done(work())
        except Exception as error:
            failed(error)

    service = SimpleNamespace(
        play_preset_station=lambda reference: (
            sent.append(reference.media_id) or "Aktualna nazwa"
        ),
    )

    def control(operation, message):
        operation(service, target.player_id)
        spoken.append(message(SimpleNamespace(volume=0)))

    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.HEOS),
        _heos_state=SimpleNamespace(selected_target=target),
        _last_preset_slot_by_session={},
        _preset_inputs=lambda _session: ({"heos": [entry.to_payload()]}, []),
        _heos_control=control,
        _heos_play_confirmation=LiteFrame._heos_play_confirmation,
        layout=SimpleNamespace(state_json=tmp_path / "missing.json"),
        runner=SimpleNamespace(submit=submit),
        announcer=SimpleNamespace(say=spoken.append),
    )
    LiteFrame._activate_preset(frame, 1)
    assert sent == ["media-1"]
    assert spoken == [
        "HEOS przyjął polecenie dla Aktualna nazwa. "
        "Głośność 0; odtwarzanie może być niesłyszalne"
    ]
    assert frame._last_preset_slot_by_session[SessionId.HEOS] == 1
