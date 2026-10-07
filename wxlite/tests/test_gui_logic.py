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
        # OK/CANCEL/YES NIE sa tu atrapami -- maja liczby, patrz nizej.
        "ID_HELP", "ID_OK", "ID_CANCEL", "YES_NO", "NO_DEFAULT",
        "ICON_QUESTION", "ICON_INFORMATION",
        # Flagi ukladu i przyciskow maja LICZBY (patrz nizej), nie atrapy.
        "SL_HORIZONTAL", "SL_LABELS", "FONTWEIGHT_BOLD", "DD_DIR_MUST_EXIST",
        "FD_OPEN", "FD_FILE_MUST_EXIST", "EVT_TIMER", "EVT_CLOSE", "EVT_KEY_DOWN",
        "EVT_LIST_ITEM_ACTIVATED", "EVT_LIST_ITEM_SELECTED", "EVT_BUTTON", "EVT_SLIDER",
        "EVT_MENU", "ITEM_NORMAL", "ITEM_CHECK", "ITEM_RADIO", "ID_OPEN", "ID_COPY",
        "ID_PREFERENCES", "ID_ABOUT", "ACCEL_CTRL", "ACCEL_SHIFT", "ACCEL_ALT",
        "ACCEL_NORMAL",
    ):
        setattr(wx, name, object())

    # Bity stanu ListCtrl: nasz kod sklada z nich maske dla ``SetItemState``,
    # wiec musza byc LICZBAMI, nie atrapami. Wartosci ODCZYTANE z naglowka
    # wxWidgets ``include/wx/listbase.h`` (nie z pamieci):
    #   #define wxLIST_STATE_FOCUSED   0x0002
    #   #define wxLIST_STATE_SELECTED  0x0004
    wx.LIST_STATE_FOCUSED = 0x0002  # type: ignore[attr-defined]
    wx.LIST_STATE_SELECTED = 0x0004  # type: ignore[attr-defined]

    # Stale ``wx.Accessible``. Nazwa i rola SAMEJ listy ida przez nakladke
    # ``MediaListAccessible`` -- bez tego pusta lista byla dla czytnika
    # obiektem bez nazwy i z rola 0 ("nieznane").
    #
    # Wartosci ODCZYTANE z zainstalowanego wxPython 4.2.3 w runtime produkcji
    # (``python.exe -c "import wx; print(wx.ACC_OK)"``), NIE z pamieci. Pierwsza
    # wersja tej atrapy miala tu 1/2/33 "z glowy" i byla po prostu zla -- atrapa
    # ze zlymi stalymi zielenilaby test, ktory na prawdziwym wx nie dziala.
    wx.ACC_FAIL = 0  # type: ignore[attr-defined]
    wx.ACC_OK = 2  # type: ignore[attr-defined]
    wx.ACC_NOT_IMPLEMENTED = 3  # type: ignore[attr-defined]
    wx.ROLE_SYSTEM_LIST = 32  # type: ignore[attr-defined]
    wx.ROLE_NONE = 0  # type: ignore[attr-defined]

    # Flagi przyciskow dialogu. Nasz kod sklada z nich maske (``wx.OK |
    # wx.CANCEL``) dla ``CreateStdDialogButtonSizer``, wiec MUSZA byc
    # liczbami -- atrapa ``object()`` wywala sie na operatorze ``|``.
    #
    # Wartosci ODCZYTANE z naglowka wxWidgets ``include/wx/defs.h``
    # (nie z pamieci):
    #   #define wxYES     0x00000002
    #   #define wxOK      0x00000004
    #   #define wxNO      0x00000008
    #   #define wxCANCEL  0x00000010
    wx.YES = 0x0002  # type: ignore[attr-defined]
    wx.OK = 0x0004  # type: ignore[attr-defined]
    wx.NO = 0x0008  # type: ignore[attr-defined]
    wx.CANCEL = 0x0010  # type: ignore[attr-defined]

    # Flagi ukladu sizerow. Tez skladane operatorem ``|`` (``wx.ALL |
    # wx.EXPAND``), wiec musza byc liczbami. Wartosci ODCZYTANE z
    # ``include/wx/defs.h`` (enum wxOrientation/wxDirection/wxAlignment/
    # wxStretch), nie z pamieci:
    #   wxHORIZONTAL 0x0004, wxVERTICAL 0x0008
    #   wxLEFT 0x0010, wxRIGHT 0x0020, wxUP 0x0040, wxDOWN 0x0080
    #   wxALL = wxUP|wxDOWN|wxRIGHT|wxLEFT = 0x00F0
    #   wxALIGN_RIGHT 0x0200, wxALIGN_CENTER_VERTICAL 0x0800
    #   wxGROW 0x2000, wxEXPAND = wxGROW
    wx.HORIZONTAL = 0x0004  # type: ignore[attr-defined]
    wx.VERTICAL = 0x0008  # type: ignore[attr-defined]
    wx.LEFT = 0x0010  # type: ignore[attr-defined]
    wx.RIGHT = 0x0020  # type: ignore[attr-defined]
    wx.TOP = 0x0040  # type: ignore[attr-defined]
    wx.BOTTOM = 0x0080  # type: ignore[attr-defined]
    wx.ALL = 0x00F0  # type: ignore[attr-defined]
    wx.ALIGN_RIGHT = 0x0200  # type: ignore[attr-defined]
    wx.ALIGN_CENTER_VERTICAL = 0x0800  # type: ignore[attr-defined]
    wx.EXPAND = 0x2000  # type: ignore[attr-defined]

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
        "Window", "Accessible",
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
    """Ctrl+O to PLIKI, tak jak w oryginale (MainWindow.xaml:42-45).

    Ten test dlugo wymagal odwrotnie (folder) i dlatego utrwalal blad, ktory
    Michal zglosil: ``OpenLocalFilesMenuItem`` ma ``InputGestureText="Ctrl+O"``.
    """
    chord = chord_from_event(FakeKeyEvent(ord("o"), ctrl=True))
    assert chord.canonical == "Ctrl+O"
    assert resolve(chord, player_view=False, radio_session=False) is Action.OPEN_FILE_DIALOG


def test_ctrl_shift_o_opens_a_folder() -> None:
    """Ctrl+Shift+O to FOLDER (MainWindow.xaml:46-49, OpenLocalFolderMenuItem)."""
    chord = chord_from_event(FakeKeyEvent(ord("O"), ctrl=True, shift=True))
    assert chord.canonical == "Ctrl+Shift+O"
    assert resolve(chord, player_view=False, radio_session=False) is Action.OPEN_FOLDER_DIALOG


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

    # LEWA ma wyjatek z oryginalu (krotka informacja, cs:23206-23216); pozostale
    # strzalki i Home/End nadal chodza po wierszach w samej kontrolce.
    for code in (wx.WXK_UP, wx.WXK_DOWN, wx.WXK_RIGHT, wx.WXK_HOME, wx.WXK_END):
        chord = chord_from_event(FakeKeyEvent(code))
        assert resolve(chord, player_view=False, radio_session=False) is None
    left = chord_from_event(FakeKeyEvent(wx.WXK_LEFT))
    assert resolve(left, player_view=False, radio_session=False) is Action.QUICK_INFORMATION


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


def test_zaznaczenie_wiersza_odswieza_bramki_menu() -> None:
    """Regresja ZMIERZONA na zywym GUI (statusclip-2).

    Ctrl+Shift+C fizycznie dotarlo do okna (hook.key: keyCode 67, mods 6) i
    NIE wywolalo zadnej akcji, mimo ze na liscie byl zaznaczony plik.

    Przyczyna: ``needs_selection=True`` wylacza pozycje menu, dopoki nic nie
    jest zaznaczone, a ``_refresh_menu_state`` lecial TYLKO z ``_sync_views``
    (zmiana widoku/sesji). Zaznaczenie wiersza strzalka zmienia model, ale nie
    bylo zdarzeniem odswiezajacym menu -- wiec WYLACZONY akcelerator okna
    polykal klawisz, zamiast przepuscic go do listy.

    Ten test pilnuje SPRZEZENIA: po ``_on_item_selected`` stan menu musi byc
    przeliczony. Nie sprawdza samego ``Enable`` (to robi
    ``_refresh_menu_state``), tylko ze ktos go w ogole wola.
    """
    from amc_wx_lite import gui

    calls = []

    class Model:
        rows = [1]
        selected_index = 0
        selected_row = object()

        def select_id(self, item_id):
            calls.append(("select_id", item_id))

        def select_index(self, index):
            calls.append(("select_index", index))

    class Session:
        model = Model()

    class Nav:
        session = Session()

    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame.navigator = Nav()
    frame._refresh_menu_state = lambda: calls.append(("refresh", None))

    class Ctrl(gui.MediaListCtrl):
        updating = False

        def __init__(self):
            pass

        def shown_item_id(self, index):
            return "i1"

    class Event:
        def GetEventObject(self):
            return ctrl

        def GetIndex(self):
            return 0

        def Skip(self):
            pass

    ctrl = Ctrl()
    frame._on_item_selected(Event())

    assert ("refresh", None) in calls, (
        "zaznaczenie wiersza nie przeliczylo stanu menu; wylaczony akcelerator "
        f"nadal polykalby Ctrl+Shift+C (wywolania: {calls})")
