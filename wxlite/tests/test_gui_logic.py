"""Testy czystej logiki z gui.py BEZ pulpitu.

wx nie jest w WSL dostepne (i nie wolno tu zaklada GUI), a mimo to dwie rzeczy
z okna MUSZA byc sprawdzone maszynowo: tlumaczenie zdarzenia klawisza na akord
oraz formatowanie czasu czytane przez czytnik ekranu. Podstawiamy minimalny
zastepnik modulu ``wx`` z samymi stalymi, ktorych te funkcje uzywaja.

Testu samego OKNA tu nie ma - okno wymaga pulpitu Windows i odbierze je rodzic.
"""

from __future__ import annotations

import sys
import types


def install_wx_stub() -> None:
    """Minimalny zastepnik wx: tylko kody klawiszy i atrapy klas bazowych."""
    if "wx" in sys.modules and getattr(sys.modules["wx"], "_amc_stub", False):
        return

    wx = types.ModuleType("wx")
    wx._amc_stub = True  # type: ignore[attr-defined]

    codes = {
        "WXK_RETURN": 13, "WXK_NUMPAD_ENTER": 370, "WXK_BACK": 8, "WXK_ESCAPE": 27,
        "WXK_SPACE": 32, "WXK_DELETE": 127, "WXK_F1": 340, "WXK_F2": 341, "WXK_F6": 345,
        "WXK_LEFT": 314, "WXK_RIGHT": 316, "WXK_UP": 315, "WXK_DOWN": 317, "WXK_TAB": 9,
        "WXK_HOME": 313, "WXK_END": 312, "WXK_PAGEUP": 366, "WXK_PAGEDOWN": 367,
    }
    for name, value in codes.items():
        setattr(wx, name, value)

    # Stale uzywane przy budowie okna (nie wykonujemy go, ale import ich dotyka).
    for name in (
        "LC_REPORT", "LC_VIRTUAL", "LC_SINGLE_SEL", "BORDER_SUNKEN", "ID_ANY", "ID_EXIT",
        "ID_HELP", "ID_OK", "ID_CANCEL", "OK", "CANCEL", "YES", "YES_NO", "NO_DEFAULT",
        "ICON_QUESTION", "ICON_INFORMATION", "VERTICAL", "HORIZONTAL", "ALL", "EXPAND",
        "LEFT", "RIGHT", "TOP", "BOTTOM", "ALIGN_RIGHT", "ALIGN_CENTER_VERTICAL",
        "SL_HORIZONTAL", "SL_LABELS", "FONTWEIGHT_BOLD", "DD_DIR_MUST_EXIST",
        "FD_OPEN", "FD_FILE_MUST_EXIST", "EVT_TIMER", "EVT_CLOSE", "EVT_KEY_DOWN",
        "EVT_LIST_ITEM_ACTIVATED", "EVT_LIST_ITEM_SELECTED", "EVT_BUTTON", "EVT_SLIDER",
        "EVT_MENU",
    ):
        setattr(wx, name, object())

    class _Any:
        def __init__(self, *args, **kwargs) -> None:
            pass

        def __getattr__(self, name):
            return _Any()

        def __call__(self, *args, **kwargs):
            return _Any()

    for name in (
        "Frame", "Panel", "App", "Dialog", "ListCtrl", "StaticText", "TextCtrl", "Button",
        "Slider", "BoxSizer", "FlexGridSizer", "Timer", "MenuBar", "Menu", "KeyEvent",
        "ListEvent", "CommandEvent", "TimerEvent", "CloseEvent", "DirDialog", "FileDialog",
        "Window",
    ):
        setattr(wx, name, _Any)

    wx.CallAfter = lambda fn, *a, **k: fn(*a, **k)  # type: ignore[attr-defined]
    wx.MessageBox = lambda *a, **k: None  # type: ignore[attr-defined]
    sys.modules["wx"] = wx


install_wx_stub()

from amc_wx_lite.gui import chord_from_event, format_time  # noqa: E402
from amc_wx_lite.shortcuts import Action, resolve  # noqa: E402


class FakeKeyEvent:
    """Zdarzenie klawisza o tym samym interfejsie, ktorego uzywa okno."""

    def __init__(self, code: int, ctrl: bool = False, shift: bool = False, alt: bool = False) -> None:
        self._code = code
        self._ctrl = ctrl
        self._shift = shift
        self._alt = alt

    def GetKeyCode(self) -> int:  # noqa: N802 - API wx
        return self._code

    def ControlDown(self) -> bool:  # noqa: N802
        return self._ctrl

    def ShiftDown(self) -> bool:  # noqa: N802
        return self._shift

    def AltDown(self) -> bool:  # noqa: N802
        return self._alt


# ------------------------------------------------- klawisz -> akord -> akcja


def test_enter_from_both_keyboards_activates() -> None:
    import wx

    for code in (wx.WXK_RETURN, wx.WXK_NUMPAD_ENTER):
        chord = chord_from_event(FakeKeyEvent(code))
        assert resolve(chord, player_view=False, radio_session=False) is Action.ACTIVATE


def test_letter_keys_are_upper_cased_so_ctrl_o_matches() -> None:
    chord = chord_from_event(FakeKeyEvent(ord("o"), ctrl=True))
    assert chord.canonical == "Ctrl+O"
    assert resolve(chord, player_view=False, radio_session=False) is Action.OPEN_FOLDER_DIALOG


def test_ctrl_shift_o_opens_a_single_file() -> None:
    chord = chord_from_event(FakeKeyEvent(ord("O"), ctrl=True, shift=True))
    assert chord.canonical == "Ctrl+Shift+O"
    assert resolve(chord, player_view=False, radio_session=False) is Action.OPEN_FILE_DIALOG


def test_digit_keys_switch_sessions() -> None:
    chord = chord_from_event(FakeKeyEvent(ord("1"), ctrl=True))
    assert resolve(chord, player_view=False, radio_session=False) is Action.SESSION_FILES


def test_shift_f6_returns_to_the_list() -> None:
    import wx

    chord = chord_from_event(FakeKeyEvent(wx.WXK_F6, shift=True))
    assert chord.canonical == "Shift+F6"
    assert resolve(chord, player_view=True, radio_session=False) is Action.SHOW_LIST


def test_arrow_keys_on_the_list_are_left_to_the_control() -> None:
    import wx

    for code in (wx.WXK_UP, wx.WXK_DOWN, wx.WXK_LEFT, wx.WXK_RIGHT, wx.WXK_HOME, wx.WXK_END):
        chord = chord_from_event(FakeKeyEvent(code))
        assert resolve(chord, player_view=False, radio_session=False) is None


def test_unknown_key_never_resolves_to_an_action() -> None:
    chord = chord_from_event(FakeKeyEvent(9999))
    assert chord.key.startswith("#")
    assert resolve(chord, player_view=False, radio_session=False) is None
    assert resolve(chord, player_view=True, radio_session=False) is None


# ------------------------------------------------------------ czas po polsku


def test_time_is_spoken_short_and_in_polish() -> None:
    assert format_time(0) == "0 s"
    assert format_time(5) == "5 s"
    assert format_time(65) == "1 min 5 s"
    assert format_time(3725) == "1 godz 2 min 5 s"


def test_unknown_duration_is_named_not_shown_as_a_number() -> None:
    # Strumien radiowy nie ma dlugosci - czytnik ma powiedziec "nieznany",
    # nie "-1 s".
    assert format_time(None) == "nieznany"
    assert format_time(-1) == "nieznany"
