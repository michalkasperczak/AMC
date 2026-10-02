"""Check actual notification calls, not what a screen reader speaks."""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub
install_wx_stub()
import wx
from amc_wx_lite.gui import Announcer
from types import SimpleNamespace


class Status:
    def __init__(self):
        self.label = ""
        self.updates = 0
        self.name = ""
    def SetLabel(self, text):
        self.label = text
        self.updates += 1
    def GetLabel(self):
        return self.label
    def SetName(self, text):
        self.name = text
    def GetParent(self):
        return SimpleNamespace()


def capture(active=True):
    events = []
    wx.ACC_EVENT_SYSTEM_ALERT = 2
    wx.OBJID_CLIENT = -4
    wx.Accessible = SimpleNamespace(NotifyEvent=lambda *args: events.append(args))
    wx.GetTopLevelParent = lambda _: SimpleNamespace(IsActive=lambda: active)
    status = Status()
    return Announcer(status), status, events


def test_explicit_announcement_emits_standard_accessibility_event():
    announcer, status, events = capture()
    announcer.say("Pauza")
    assert status.label == "Pauza"
    assert events == [(2, status, -4, 0)], "Changing static text alone is not an announcement"


def test_repeated_explicit_query_is_announced_without_rewriting_text():
    announcer, status, events = capture()
    announcer.say("10 sekund")
    announcer.say("10 sekund")
    assert len(events) == 2
    assert status.updates == 1


def test_background_window_updates_text_but_does_not_interrupt_other_app():
    announcer, status, events = capture(active=False)
    announcer.say("Próba")
    assert status.label == "Próba" and not events
