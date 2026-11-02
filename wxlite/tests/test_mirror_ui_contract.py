"""The mirroring command has one name, shortcut and scope across AMC surfaces."""

import unittest

from amc_wx_lite import command_palette, menu_model
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.shortcuts import (
    Action, Chord, command_presentation, describe, describe_action, resolve,
)


class MirrorUiContractTests(unittest.TestCase):
    def test_name_help_menu_and_palette_match(self):
        presentation = command_presentation(Action.MIRROR_SESSION)
        self.assertEqual(presentation.shortcut, "Shift+M")
        self.assertIn("AirPlay", presentation.description)
        self.assertIn("lokalne", presentation.description)
        self.assertEqual(describe_action(Action.MIRROR_SESSION), presentation.description)
        self.assertIn(("Shift+M", presentation.description), describe())
        for session in (SessionId.FILES, SessionId.RADIO, SessionId.PODCASTS):
            entry = next(item for item in command_palette.entries_for_session(session)
                         if item.action is Action.MIRROR_SESSION)
            menu = next(item for group in menu_model.menus_for_session(session)
                        for item in group.items if item.action is Action.MIRROR_SESSION)
            self.assertEqual((entry.name, entry.shortcut),
                             (presentation.name, presentation.shortcut))
            self.assertEqual((menu.label, menu.shortcut),
                             (presentation.menu_label, presentation.shortcut))
        for session in (SessionId.SPOTIFY, SessionId.TIDAL, SessionId.WIIM,
                        SessionId.SONOS, SessionId.FRONTIER_SMART):
            self.assertFalse(any(item.action is Action.MIRROR_SESSION
                                 for item in command_palette.entries_for_session(session)))

    def test_keyboard_scope_keeps_existing_audio_output_shortcut(self):
        for player in (False, True):
            self.assertIs(resolve(Chord("M", shift=True), player_view=player,
                                  radio_session=True), Action.MIRROR_SESSION)
            self.assertIs(resolve(Chord("A", shift=True), player_view=player,
                                  radio_session=True), Action.SELECT_AUDIO_OUTPUT)
            for context in ("wiim_session", "sonos_session", "spotify_session",
                            "tidal_session", "frontier_smart_session"):
                self.assertIsNone(resolve(Chord("M", shift=True), player_view=player,
                                          radio_session=False,
                                          **{context: True}))


if __name__ == "__main__":
    unittest.main()
