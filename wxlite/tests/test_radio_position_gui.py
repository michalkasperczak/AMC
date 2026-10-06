"""Prawdziwy handler GUI na kontrolowanych granicach zapisu; wymaga wx."""
import unittest
from types import SimpleNamespace

try:
    import wx
except ImportError:
    wx = None


@unittest.skipIf(wx is None, "test handlera wx wymaga runtime Windows z wxPython")
class RadioPositionMenuTransaction(unittest.TestCase):
    def test_odmowa_zapisu_przywraca_takze_stan_menu(self):
        from amc_wx_lite.gui import LiteFrame
        from amc_wx_lite.state_store import Options, LiteState

        messages = []
        refreshed = []
        options = Options(radio_announce_position=True)

        def fail_save(_state):
            raise OSError("testowa odmowa zapisu")

        frame = SimpleNamespace(
            options=options, state=LiteState(options=options),
            store=SimpleNamespace(save=fail_save),
            announcer=SimpleNamespace(say=messages.append),
            _refresh_menu_state=lambda: refreshed.append(options.radio_announce_position),
        )
        LiteFrame._toggle_radio_position(frame)
        self.assertTrue(options.radio_announce_position)
        self.assertEqual(refreshed, [True], "natywne menu przestawia ptaszek przed handlerem")
        self.assertEqual(len(messages), 1)
        self.assertIn("Nie udało się zapisać", messages[0])
