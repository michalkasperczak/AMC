"""Selection events should not repeat native menu work for one arrow."""

from types import SimpleNamespace

from amc_wx_lite.gui import LiteFrame, MediaListCtrl
from amc_wx_lite.navigation import SessionId


def test_focus_then_select_refreshes_menu_once() -> None:
    for session_id in (
        SessionId.TIDAL,
        SessionId.SPOTIFY,
        SessionId.RADIOARCHIWISTA,
    ):
        calls = []

        class Model:
            selected_id = None
            selected_row = None

            def select_id(self, item_id):
                self.selected_id = item_id
                calls.append(("select", item_id))
                return True

        class Control(MediaListCtrl):
            updating = False

            def __init__(self):
                pass

            def shown_item_id(self, index):
                return "row-2"

        control = Control()
        frame = LiteFrame.__new__(LiteFrame)
        frame.navigator = SimpleNamespace(
            active=session_id, session=SimpleNamespace(model=Model()),
        )
        frame._refresh_menu_state = lambda: calls.append(("menu", None))
        frame._play_radio_activity_cue = lambda row: None

        class Event:
            def GetEventObject(self):
                return control

            def GetIndex(self):
                return 1

            def Skip(self):
                pass

        frame._on_item_focused(Event())
        frame._on_item_selected(Event())
        assert calls.count(("menu", None)) == 1, session_id
        assert frame.navigator.session.model.selected_id == "row-2"
