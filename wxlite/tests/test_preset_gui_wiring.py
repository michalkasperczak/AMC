"""Preset execution through the real LiteFrame methods, without opening wx UI."""

from __future__ import annotations

import json
from types import SimpleNamespace

from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.navigation import Navigator, PlayTidalContainer, PlayTidalTrack, SessionId
from amc_wx_lite.profile_presets import PresetTarget, ResolvedPreset


def _frame(tmp_path):
    profile = tmp_path / "state.json"
    profile.write_text(json.dumps({"sessionPresets": {"entriesBySession": {"tidal": []}}}), encoding="utf-8")
    navigator = Navigator()
    navigator.switch_session(SessionId.TIDAL)
    spoken = []
    intents = []
    state = SimpleNamespace(preset_overrides={})
    frame = SimpleNamespace(
        navigator=navigator,
        state=state,
        layout=SimpleNamespace(state_json=profile),
        announcer=SimpleNamespace(say=spoken.append),
        store=SimpleNamespace(save=lambda _state: None),
        _preset_inputs=lambda _session: (state.preset_overrides, []),
        _run=lambda result: intents.extend(result),
        _last_preset_slot_by_session={},
    )
    frame.runner = SimpleNamespace(
        submit=lambda _name, work, done, _failed: done(work())
    )
    return frame, spoken, intents


def test_free_tidal_slot_saves_immediately_and_plays_selected_track(tmp_path) -> None:
    frame, spoken, intents = _frame(tmp_path)
    frame._current_preset_target = lambda: PresetTarget(
        "tidal:track:123", "tidal-track", "Wybrany utwór", "123"
    )

    LiteFrame._assign_preset(frame, 2)

    assert frame.state.preset_overrides["tidal"][0]["slot"] == 2
    assert spoken[-1] == "Zapisano preset 2: Wybrany utwór"
    LiteFrame._activate_preset(frame, 2)
    assert any(isinstance(intent, PlayTidalTrack) for intent in intents)


def test_tidal_album_preset_plays_whole_container(tmp_path) -> None:
    frame, spoken, intents = _frame(tmp_path)
    frame._current_preset_target = lambda: PresetTarget(
        "tidal:album:456", "tidal-album", "Cała płyta", "456"
    )

    LiteFrame._assign_preset(frame, 5)
    LiteFrame._activate_preset(frame, 5)

    container = next(intent for intent in intents if isinstance(intent, PlayTidalContainer))
    assert (container.kind, container.title, container.service_id) == (
        "album", "Cała płyta", "456"
    )
    assert spoken[-1] == "Zapisano preset 5: Cała płyta"


def test_english_preset_labels_menu_and_missing_target(tmp_path) -> None:
    from amc_wx_lite import i18n, menu_model
    from amc_wx_lite.profile_presets import choices

    old_language = i18n.active_language()
    try:
        i18n.configure("en")
        labels = choices(())
        assert labels[0].label == "1, empty, Ctrl+Shift+1"
        assert labels[10].label == "-, empty, Ctrl+Shift+minus"
        assert labels[11].label == "=, empty, Ctrl+Shift+equals sign"
        menu = next(
            item for item in menu_model.menus_for_session(SessionId.TIDAL)
            if item.title == "Pres&ets"
        )
        assert len(menu.items) == 10
        assert menu.items[0].label == "Assign selected item to preset 1 (&1)"
        assert menu.items[9].label == "Assign selected item to preset 0 (&0)"
        frame, spoken, _intents = _frame(tmp_path)
        frame._current_preset_target = lambda: None
        LiteFrame._assign_preset(frame, 3)
        assert spoken == [
            "Select a TIDAL track, album, playlist, or artist in the list "
            "and try assigning the preset again"
        ]
    finally:
        i18n.configure(old_language)


def test_selected_tidal_row_without_catalog_id_is_named_in_both_languages(tmp_path) -> None:
    from amc_wx_lite import i18n
    from amc_wx_lite.list_model import Row

    frame, spoken, _intents = _frame(tmp_path)
    frame._current_preset_target = lambda: None
    frame.navigator.session.model.replace([
        Row("tidal:track:missing", "Widoczny utwór", "track", service_kind="track")
    ])
    old_language = i18n.active_language()
    try:
        for language, expected in (
            ("pl", "Element Widoczny utwór nie ma danych katalogowych TIDAL potrzebnych do zapisania presetu"),
            ("en", "Widoczny utwór lacks the TIDAL catalog details needed to save a preset"),
        ):
            i18n.configure(language)
            LiteFrame._assign_preset(frame, 1)
            assert spoken[-1] == expected
    finally:
        i18n.configure(old_language)
