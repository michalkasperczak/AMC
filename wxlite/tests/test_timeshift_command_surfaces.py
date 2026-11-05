"""The new Time Shift setting remains discoverable through the shared command."""

from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.menu_model import menus_for_session
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.shortcuts import (
    Action,
    Chord,
    describe,
    describe_action,
    keyboard_help_announcement,
)


def test_timeshift_folder_is_described_in_settings_command_surfaces():
    description = (
        "Ustawienia programu; na karcie Komunikaty dostępności ustawisz "
        "odczyt listy strzałkami, a na karcie Radio i nagrywanie długość oraz folder bufora Time Shift"
    )
    for session in (SessionId.RADIO, SessionId.FILES):
        menu = next(
            item
            for group in menus_for_session(session)
            for item in group.items
            if item.action is Action.GENERAL_SETTINGS
        )
        palette = next(
            item
            for item in entries_for_session(session)
            if item.action is Action.GENERAL_SETTINGS
        )
        assert menu.shortcut == palette.shortcut == "Ctrl+,"
        assert palette.name == "Ustawienia programu…"
        assert describe_action(Action.GENERAL_SETTINGS) == description
        assert dict(describe("Radio internetowe", {Action.GENERAL_SETTINGS}))["Ctrl+,"] == description
        assert "folder bufora Time Shift" in keyboard_help_announcement(
            Chord(",", ctrl=True), Action.GENERAL_SETTINGS, "Radio internetowe"
        )
