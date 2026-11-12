"""Kontrakt dostępnej, atomowej zmiany skrótów klawiaturowych."""

from __future__ import annotations

from pathlib import Path

from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.menu_model import menus_for_session
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.shortcuts import (
    Action,
    Chord,
    configurable_shortcuts,
    describe,
    normalize_shortcut_overrides,
    serialize_shortcut_overrides,
    shortcut_conflict,
    validate_custom_shortcut,
    resolve,
)
from amc_wx_lite.state_store import LiteState, StateStore


GUI_SOURCE = Path(__file__).parents[1] / "amc_wx_lite" / "gui.py"


def test_custom_shortcut_replaces_every_old_alias_only_in_valid_context() -> None:
    overrides = {Action.QUICK_INFORMATION: "Ctrl+Alt+J"}
    assert shortcut_conflict(
        Action.QUICK_INFORMATION, "Ctrl+Alt+J", overrides
    ) is None
    assert resolve(
        Chord("J", ctrl=True, alt=True),
        player_view=False,
        radio_session=False,
        shortcut_overrides=overrides,
    ) is Action.QUICK_INFORMATION
    assert resolve(
        Chord("T", alt=True),
        player_view=False,
        radio_session=False,
        shortcut_overrides=overrides,
    ) is None
    assert resolve(
        Chord("Left"),
        player_view=False,
        radio_session=False,
        shortcut_overrides=overrides,
    ) is None


def test_contextual_command_keeps_its_context_after_rebinding() -> None:
    overrides = {Action.STATION_ADD: "Ctrl+Alt+N"}
    custom = Chord("N", ctrl=True, alt=True)
    assert resolve(
        custom,
        player_view=False,
        radio_session=True,
        shortcut_overrides=overrides,
    ) is Action.STATION_ADD
    assert resolve(
        custom,
        player_view=False,
        radio_session=False,
        shortcut_overrides=overrides,
    ) is None
    assert resolve(
        Chord("N", ctrl=True),
        player_view=False,
        radio_session=True,
        shortcut_overrides=overrides,
    ) is None


def test_removing_a_shortcut_disables_its_defaults() -> None:
    overrides = {Action.GENERAL_SETTINGS: ""}
    for player_view in (False, True):
        assert resolve(
            Chord(",", ctrl=True),
            player_view=player_view,
            radio_session=False,
            shortcut_overrides=overrides,
        ) is None


def test_conflicts_and_typing_keys_are_rejected_with_user_facing_text() -> None:
    conflict = shortcut_conflict(
        Action.GENERAL_SETTINGS,
        "Ctrl+F",
        {Action.GENERAL_SETTINGS: "Ctrl+F"},
    )
    assert conflict is not None
    _action, name = conflict
    assert name == "Szukaj w bieżącej usłudze"
    assert "Action." not in name
    dynamic_session = shortcut_conflict(
        Action.GENERAL_SETTINGS,
        "Ctrl+8",
        {Action.GENERAL_SETTINGS: "Ctrl+8"},
    )
    assert dynamic_session is not None
    assert dynamic_session[1] == "Przełączanie sesji"
    assert validate_custom_shortcut("A") is not None
    assert validate_custom_shortcut("Shift+A") is not None
    assert validate_custom_shortcut("Ctrl+TechnicalKey") is not None
    assert validate_custom_shortcut("Ctrl+Ctrl") is not None
    assert validate_custom_shortcut("Ctrl+Alt+A") is None


def test_recovery_navigation_and_native_preset_actions_are_not_editable() -> None:
    actions = {definition.action for definition in configurable_shortcuts()}
    for protected in (
        Action.ACTIVATE,
        Action.CLOSE_LIST_PREVIEW,
        Action.HELP,
        Action.KEYBOARD_HELP,
        Action.RECORD_STOP_ALL,
        Action.SESSION_LIST,
        Action.PRESET_10,
        Action.TIDAL_RELATIONS,
    ):
        assert protected not in actions


def test_state_round_trip_keeps_only_safe_shortcuts(tmp_path: Path) -> None:
    store = StateStore(tmp_path)
    state = LiteState(shortcut_overrides={
        Action.GENERAL_SETTINGS.value: "Ctrl+Alt+G",
        Action.KEYBOARD_HELP.value: "Ctrl+Alt+H",
        "technical.unknown": "Ctrl+Alt+U",
    })
    store.save(state)
    loaded = store.load()
    assert loaded.shortcut_overrides == {
        Action.GENERAL_SETTINGS.value: "Ctrl+Alt+G"
    }
    assert normalize_shortcut_overrides(loaded.shortcut_overrides) == {
        Action.GENERAL_SETTINGS: "Ctrl+Alt+G"
    }


def test_menu_palette_and_full_help_show_the_same_saved_shortcut() -> None:
    overrides = {Action.GENERAL_SETTINGS: "Ctrl+Alt+G"}
    menu_item = next(
        item
        for menu in menus_for_session(
            SessionId.FILES, shortcut_overrides=overrides
        )
        for item in menu.items
        if item.action is Action.GENERAL_SETTINGS
    )
    palette_item = next(
        item
        for item in entries_for_session(
            SessionId.FILES, shortcut_overrides=overrides
        )
        if item.action is Action.GENERAL_SETTINGS
    )
    help_shortcut = next(
        shortcut
        for shortcut, name in describe(shortcut_overrides=overrides)
        if name.startswith("Ustawienia programu")
    )
    assert menu_item.shortcut == "Ctrl+Alt+G"
    assert palette_item.shortcut == "Ctrl+Alt+G"
    assert help_shortcut == "Ctrl+Alt+G"


def test_serialization_never_exposes_enum_or_unknown_values() -> None:
    payload = serialize_shortcut_overrides({
        Action.GENERAL_SETTINGS: "Ctrl+Alt+G"
    })
    assert payload == {"settings.general": "Ctrl+Alt+G"}
    assert "Action." not in repr(payload)
    assert normalize_shortcut_overrides({
        "unknown.command": "Ctrl+Alt+U",
        Action.KEYBOARD_HELP.value: "Ctrl+Alt+H",
    }) == {}


def test_keyboard_page_has_explicit_names_and_save_cancel_boundary() -> None:
    source = GUI_SOURCE.read_text(encoding="utf-8")
    assert 'self.notebook.AddPage(keyboard, i18n.tr("settings.tab.keyboard"))' in source
    assert 'self.shortcut_filter.SetName("Filtruj polecenia klawiatury")' in source
    assert 'self.shortcut_list.SetName("Polecenia i przypisane skróty")' in source
    assert '"Nowy skrót dla wybranego polecenia"' in source
    assert '"Usuń skrót wybranego polecenia"' in source
    assert '"Przywróć domyślny skrót wybranego polecenia"' in source
    assert '"Przywróć wszystkie domyślne skróty"' in source
    assert "self.shortcut_capture.Bind(" in source
    assert '"shortcut_overrides": serialize_shortcut_overrides(' in source
    method = source[source.index("def _show_general_playback_options") :]
    method = method[:method.index("def _with_radio_recording_options")]
    assert 'chosen.pop("shortcut_overrides", {})' in method
    assert method.index('chosen.pop("shortcut_overrides", {})') < method.index(
        "if not self._save_state():"
    )
    assert "self.shortcut_overrides = chosen_shortcuts" in method
