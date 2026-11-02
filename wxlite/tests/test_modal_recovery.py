"""Regressions for modal windows hidden behind the main AMC frame."""

from types import SimpleNamespace

from amc_wx_lite import gui


class Window:
    def __init__(self, parent=None, *, shown=True, modal=False):
        self.parent = parent
        self.shown = shown
        self.modal = modal
        self.raised = 0
        self.focused = 0

    def IsBeingDeleted(self):
        return False

    def IsShown(self):
        return self.shown

    def IsModal(self):
        return self.modal

    def GetParent(self):
        return self.parent

    def Raise(self):
        self.raised += 1

    def SetFocus(self):
        self.focused += 1


def test_reactivation_raises_deepest_visible_modal(monkeypatch):
    main = Window()
    outer = Window(main, modal=True)
    inner = Window(outer, modal=True)
    hidden = Window(inner, shown=False, modal=True)
    windows = [main, inner, hidden, outer]
    app = SimpleNamespace(
        _frontmost_visible_modal=gui.LiteApp._frontmost_visible_modal,
        GetTopWindow=lambda: main,
    )
    monkeypatch.setattr(gui.wx, "GetTopLevelWindows", lambda: windows, raising=False)
    monkeypatch.setattr(gui.wx, "GetActiveWindow", lambda: main, raising=False)
    monkeypatch.setattr(gui.wx.Window, "FindFocus", lambda: main, raising=False)

    gui.LiteApp._raise_open_modal(app)

    assert inner.raised == 1
    assert inner.focused == 1
    assert outer.raised == hidden.raised == 0


def test_reactivation_keeps_focus_in_native_child(monkeypatch):
    main = Window()
    modal = Window(main, modal=True)
    native_child = Window(modal)
    app = SimpleNamespace(
        _frontmost_visible_modal=gui.LiteApp._frontmost_visible_modal,
        GetTopWindow=lambda: main,
    )
    monkeypatch.setattr(gui.wx, "GetTopLevelWindows", lambda: [main, modal], raising=False)
    monkeypatch.setattr(gui.wx, "GetActiveWindow", lambda: native_child, raising=False)

    gui.LiteApp._raise_open_modal(app)

    assert modal.raised == modal.focused == 0
