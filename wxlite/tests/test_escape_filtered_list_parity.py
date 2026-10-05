"""Escape na wynikach czyści filtr przed wyjściem z folderu (WPF 22551+)."""
from typing import Any

from test_gui_logic import FakeKeyEvent, install_wx_stub
install_wx_stub()
from amc_wx_lite import gui
from amc_wx_lite.navigation import SessionId
from test_gui_filter_wiring import FakeFrame, ROWS


class RoutedKeyEvent(FakeKeyEvent):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.skipped = False

    def Skip(self):
        self.skipped = True


def make_frame():
    frame: Any = FakeFrame(ROWS)
    frame.active = SessionId.FILES
    parent_calls = []
    frame.go_to_parent = lambda: parent_calls.append(True) or []
    frame._run = lambda intents: None
    frame._dispatch = gui.LiteFrame._dispatch.__get__(frame, FakeFrame)
    return frame, parent_calls


def test_escape_on_filtered_list_clears_filter_before_parent():
    frame, parent_calls = make_frame()
    frame.type_into_filter("beta")
    frame._focus_filter_results()
    assert frame.shown_names() == ["Beta"]
    gui.LiteFrame._on_key(frame, FakeKeyEvent(27))
    assert parent_calls == [], "Pierwszy Escape ma wyczyścić filtr, nie wyjść z folderu"
    assert frame.filter_box.GetValue() == ""
    assert len(frame.shown_names()) == len(ROWS)
    assert frame.files_list.focused

    gui.LiteFrame._on_key(frame, FakeKeyEvent(27))
    assert parent_calls == [True], "Dopiero Escape bez filtra wraca poziom wyżej"


def test_modified_escape_does_not_clear_filter_or_navigate():
    frame, parent_calls = make_frame()
    frame.type_into_filter("beta")
    event = RoutedKeyEvent(27, ctrl=True)
    gui.LiteFrame._on_key(frame, event)
    assert parent_calls == []
    assert frame.filter_box.GetValue() == "beta"
    assert event.skipped
