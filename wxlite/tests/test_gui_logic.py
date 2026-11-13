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
        "WXK_SPACE": 32, "WXK_DELETE": 127,
        "WXK_LEFT": 314, "WXK_RIGHT": 316, "WXK_UP": 315, "WXK_DOWN": 317, "WXK_TAB": 9,
        "WXK_HOME": 313, "WXK_END": 312, "WXK_PAGEUP": 366, "WXK_PAGEDOWN": 367,
        "WXK_NUMPAD_LEFT": 372, "WXK_NUMPAD_RIGHT": 374,
        "WXK_NUMPAD_UP": 373, "WXK_NUMPAD_DOWN": 375,
        "WXK_NUMPAD_HOME": 371, "WXK_NUMPAD_END": 376,
        "WXK_NUMPAD_PAGEUP": 377, "WXK_NUMPAD_PAGEDOWN": 378,
        "WXK_NUMPAD_DELETE": 380,
    }
    for number in range(1, 25):
        codes[f"WXK_F{number}"] = 339 + number
    for name, value in codes.items():
        setattr(wx, name, value)

    # Stale uzywane przy budowie okna (nie wykonujemy go, ale import ich dotyka).
    for name in (
        "LC_REPORT", "LC_VIRTUAL", "LC_SINGLE_SEL", "BORDER_SUNKEN", "ID_ANY", "ID_EXIT",
        # OK/CANCEL/YES NIE sa tu atrapami -- maja liczby, patrz nizej.
        "ID_HELP", "ID_OK", "ID_CANCEL", "YES_NO", "NO_DEFAULT",
        "ICON_QUESTION", "ICON_INFORMATION", "ICON_WARNING",
        # Flagi ukladu i przyciskow maja LICZBY (patrz nizej), nie atrapy.
        "SL_HORIZONTAL", "SL_LABELS", "FONTWEIGHT_BOLD", "DD_DIR_MUST_EXIST",
        "FD_OPEN", "FD_FILE_MUST_EXIST", "EVT_TIMER", "EVT_CLOSE", "EVT_KEY_DOWN",
        "EVT_CHAR_HOOK", "EVT_CONTEXT_MENU", "EVT_ACTIVATE_APP",
        "EVT_HOTKEY",
        "EVT_LIST_ITEM_ACTIVATED", "EVT_LIST_ITEM_FOCUSED", "EVT_LIST_ITEM_SELECTED",
        "EVT_LIST_ITEM_DESELECTED",
        "EVT_BUTTON", "EVT_SLIDER", "EVT_CHOICE",
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
    wx.YES_NO = wx.YES | wx.NO  # type: ignore[attr-defined]
    wx.NO_DEFAULT = 0x0080  # type: ignore[attr-defined]
    wx.ICON_WARNING = 0x0100  # type: ignore[attr-defined]
    wx.MOD_CONTROL = 0x0002  # type: ignore[attr-defined]
    wx.MOD_SHIFT = 0x0004  # type: ignore[attr-defined]
    wx.LB_SINGLE = 0x0000  # type: ignore[attr-defined]

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
        "Slider", "Choice", "BoxSizer", "FlexGridSizer", "Timer", "MenuBar", "Menu", "KeyEvent",
        "ListEvent", "CommandEvent", "TimerEvent", "CloseEvent", "DirDialog", "FileDialog",
        "Window", "CheckListBox",
    ):
        setattr(wx, name, _Any)

    wx.CallAfter = lambda fn, *a, **k: fn(*a, **k)  # type: ignore[attr-defined]
    wx.MessageBox = lambda *a, **k: None  # type: ignore[attr-defined]
    sys.modules["wx"] = wx


install_wx_stub()

from amc_wx_lite.gui import (  # noqa: E402
    LiteFrame,
    YOUTUBE_EXPORT_WILDCARD,
    _WindowsPreset10HotKey,
    chord_from_event,
    format_time,
    relation_destination_labels,
    session_selection_labels,
    youtube_export_path_for_filter,
)
from amc_wx_lite.shortcuts import Action, Chord, resolve, shortcut_enabled_in_view  # noqa: E402
from amc_wx_lite.navigation import LibraryView, Navigator, SessionId, View  # noqa: E402
from amc_wx_lite.list_model import Row  # noqa: E402


class FakeKeyEvent:
    """Zdarzenie klawisza o tym samym interfejsie, ktorego uzywa okno."""

    def __init__(
        self,
        code: int,
        ctrl: bool = False,
        shift: bool = False,
        alt: bool = False,
        *,
        raw_code: int | None = None,
    ) -> None:
        self._code = code
        self._raw_code = code if raw_code is None else raw_code
        self._ctrl = ctrl
        self._shift = shift
        self._alt = alt

    def GetKeyCode(self) -> int:  # noqa: N802 - API wx
        return self._code

    def GetRawKeyCode(self) -> int:  # noqa: N802 - API wx
        return self._raw_code

    def ControlDown(self) -> bool:  # noqa: N802
        return self._ctrl

    def ShiftDown(self) -> bool:  # noqa: N802
        return self._shift

    def AltDown(self) -> bool:  # noqa: N802
        return self._alt


def test_youtube_export_format_is_explicit_and_follows_selected_file_type() -> None:
    assert "NewPipe" in YOUTUBE_EXPORT_WILDCARD
    assert "FreeTube" in YOUTUBE_EXPORT_WILDCARD
    assert "CSV kanałów" in YOUTUBE_EXPORT_WILDCARD
    assert "czytników RSS" in YOUTUBE_EXPORT_WILDCARD
    assert youtube_export_path_for_filter("Kanaly YouTube AMC.csv", 1).endswith(".opml")
    assert youtube_export_path_for_filter("Kanaly YouTube AMC.opml", 0).endswith(".csv")


# ------------------------------------------------- klawisz -> akord -> akcja


def test_enter_from_both_keyboards_activates() -> None:
    import wx

    for code in (wx.WXK_RETURN, wx.WXK_NUMPAD_ENTER):
        chord = chord_from_event(FakeKeyEvent(code))
        assert resolve(chord, player_view=False, radio_session=False) is Action.ACTIVATE


def test_session_list_labels_are_explicit_and_never_expose_model_values() -> None:
    labels = session_selection_labels()
    assert labels == (
        "Ctrl+1, Pliki lokalne",
        "Ctrl+2, Radio",
        "Ctrl+3, Podcasty i YouTube",
    )
    assert all("SessionId" not in label and "{" not in label for label in labels)


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


def test_physical_f5_and_ctrl_f5_reach_the_files_session_commands() -> None:
    """Use raw wx key codes, not a prebuilt Chord, to cover the GUI boundary."""

    import wx

    refresh = chord_from_event(FakeKeyEvent(wx.WXK_F5))
    configure = chord_from_event(FakeKeyEvent(wx.WXK_F5, ctrl=True))

    assert refresh.canonical == "F5"
    assert configure.canonical == "Ctrl+F5"
    assert resolve(
        refresh,
        player_view=False,
        radio_session=False,
        files_session=True,
    ) is Action.REFRESH_LOCAL_LIBRARY
    assert resolve(
        configure,
        player_view=False,
        radio_session=False,
        files_session=True,
    ) is Action.MANAGE_LIBRARY_FOLDERS


def test_all_physical_function_keys_have_stable_canonical_names() -> None:
    """Future commands must not leak raw wx codes such as ``#343``."""

    import wx

    for number in range(1, 25):
        code = getattr(wx, f"WXK_F{number}")
        assert chord_from_event(FakeKeyEvent(code)).canonical == f"F{number}"
        assert chord_from_event(
            FakeKeyEvent(code, ctrl=True, shift=True, alt=True)
        ).canonical == f"Ctrl+Alt+Shift+F{number}"

    # F4 nie ma obecnie polecenia. Ma jednak poprawna nazwe i pozostaje
    # bezczynny, zamiast przypadkiem trafic do innej akcji przez surowy kod wx.
    f4 = chord_from_event(FakeKeyEvent(wx.WXK_F4))
    assert f4.canonical == "F4"
    assert resolve(f4, player_view=False, radio_session=False) is None


def test_numpad_navigation_uses_the_same_names_as_navigation_cluster() -> None:
    import wx

    expected = {
        wx.WXK_NUMPAD_LEFT: "Left",
        wx.WXK_NUMPAD_RIGHT: "Right",
        wx.WXK_NUMPAD_UP: "Up",
        wx.WXK_NUMPAD_DOWN: "Down",
        wx.WXK_NUMPAD_HOME: "Home",
        wx.WXK_NUMPAD_END: "End",
        wx.WXK_NUMPAD_PAGEUP: "Prior",
        wx.WXK_NUMPAD_PAGEDOWN: "Next",
        wx.WXK_NUMPAD_DELETE: "Delete",
    }
    for code, name in expected.items():
        assert chord_from_event(FakeKeyEvent(code)).canonical == name


def test_ctrl_shift_zero_uses_physical_windows_key_for_preset_10(monkeypatch) -> None:
    """MSW CHAR_HOOK may expose Shift+0 as ``)`` instead of physical zero."""

    monkeypatch.setattr(
        "amc_wx_lite.gui._windows_virtual_key_down",
        lambda _virtual_key: False,
    )
    chord = chord_from_event(FakeKeyEvent(
        ord(")"),
        ctrl=True,
        shift=True,
        raw_code=0x30,
    ))
    assert chord.canonical == "Ctrl+Shift+0"
    assert resolve(chord, player_view=False, radio_session=False) is Action.PRESET_10


def test_ctrl_i_control_character_uses_physical_i_for_both_inbox_shortcuts() -> None:
    for shifted, expected in (
        (False, Action.VIEW_PODCAST_INBOX),
        (True, Action.VIEW_YOUTUBE_LIVE_AGGREGATE),
    ):
        chord = chord_from_event(FakeKeyEvent(
            9, ctrl=True, shift=shifted, raw_code=0x49,
        ))
        assert chord.canonical == ("Ctrl+Shift+I" if shifted else "Ctrl+I")
        assert resolve(
            chord, player_view=False, radio_session=False,
            podcast_session=True, youtube_account_available=True,
        ) is expected


def test_native_list_hook_forwards_both_inbox_shortcuts_when_wx_reports_tab() -> None:
    calls: list[str] = []
    frame = object.__new__(LiteFrame)
    frame._on_key = lambda event: calls.append(chord_from_event(event).canonical)
    for shifted in (False, True):
        frame._on_list_shortcut_hook(FakeKeyEvent(
            9, ctrl=True, shift=shifted, raw_code=0x49,
        ))
    assert calls == ["Ctrl+I", "Ctrl+Shift+I"]


def test_page_keys_scroll_all_native_lists_and_skip_tracks_only_in_player() -> None:
    import wx

    class PageEvent(FakeKeyEvent):
        skipped = False

        def Skip(self) -> None:  # noqa: N802 - wx API
            self.skipped = True

    frame = object.__new__(LiteFrame)
    frame._handle_keyboard_help_event = lambda _event: False
    for session in SessionId:
        frame.navigator = types.SimpleNamespace(view=View.LIST, active=session)
        for code in (wx.WXK_PAGEUP, wx.WXK_PAGEDOWN):
            event = PageEvent(code)
            frame._on_key(event)
            assert event.skipped, session
    for key, action in (("Prior", Action.QUEUE_PREVIOUS),
                        ("Next", Action.QUEUE_NEXT)):
        assert resolve(Chord(key), player_view=False, radio_session=False) is None
        assert resolve(Chord(key), player_view=True, radio_session=False) is action
        assert not shortcut_enabled_in_view(action, player_view=False)
        assert shortcut_enabled_in_view(action, player_view=True)
    assert resolve(
        Chord("Prior", ctrl=True), player_view=False, radio_session=False,
    ) is Action.PREVIOUS_SESSION


def test_ctrl_shift_zero_recovers_modifier_omitted_by_wx(monkeypatch) -> None:
    """Stare AMC też musiało dopełniać modyfikatory stanem klawiatury Win32."""

    monkeypatch.setattr(
        "amc_wx_lite.gui._windows_virtual_key_down",
        lambda virtual_key: virtual_key in (0x11, 0x10),
    )
    chord = chord_from_event(FakeKeyEvent(
        0,
        raw_code=0x30,
    ))
    assert chord.canonical == "Ctrl+Shift+0"
    assert resolve(chord, player_view=True, radio_session=False) is Action.PRESET_10


def test_ctrl_shift_zero_is_global_in_every_session_and_view() -> None:
    chord = Chord(key="0", ctrl=True, shift=True)
    session_flags = (
        {},
        {"radio_session": True},
        {"podcast_session": True},
        {"tidal_session": True},
        {"spotify_session": True},
        {"wiim_session": True},
        {"sonos_session": True},
    )

    for player_view in (False, True):
        for flags in session_flags:
            assert resolve(
                chord,
                player_view=player_view,
                radio_session=flags.get("radio_session", False),
                podcast_session=flags.get("podcast_session", False),
                tidal_session=flags.get("tidal_session", False),
                spotify_session=flags.get("spotify_session", False),
                wiim_session=flags.get("wiim_session", False),
                sonos_session=flags.get("sonos_session", False),
            ) is Action.PRESET_10


def test_raw_zero_does_not_rewrite_ordinary_shift_zero(monkeypatch) -> None:
    monkeypatch.setattr(
        "amc_wx_lite.gui._windows_virtual_key_down",
        lambda _virtual_key: False,
    )
    chord = chord_from_event(FakeKeyEvent(
        ord(")"),
        shift=True,
        raw_code=0x30,
    ))
    assert chord.canonical == "Shift+)"


def test_native_keyboard_boundary_recognizes_only_first_ctrl_shift_zero() -> None:
    from amc_wx_lite.gui import _native_preset_10_keydown

    assert _native_preset_10_keydown(
        0, 0x30, 0, ctrl_down=True, shift_down=True, alt_down=False
    )
    for virtual_key, flags, ctrl, shift, alt in (
        (0x39, 0, True, True, False),
        (0x30, 1 << 30, True, True, False),
        (0x30, 1 << 31, True, True, False),
        (0x30, 0, False, True, False),
        (0x30, 0, True, False, False),
        (0x30, 0, True, True, True),
    ):
        assert not _native_preset_10_keydown(
            0,
            virtual_key,
            flags,
            ctrl_down=ctrl,
            shift_down=shift,
            alt_down=alt,
        )


def test_foreground_hotkey_registers_physical_ctrl_shift_zero(monkeypatch) -> None:
    calls = []

    class Owner:
        filter_box = object()
        _keyboard_help_active = False

        def Bind(self, event_type, handler, *, id):
            calls.append(("bind", event_type, id))
            self.handler = handler

        def RegisterHotKey(self, id, modifiers, virtual_key):
            calls.append(("register", id, modifiers, virtual_key))
            return True

        def UnregisterHotKey(self, id):
            calls.append(("unregister", id))

        def _trigger_preset_10_from_native_boundary(self):
            calls.append(("native",))
            self._activate_preset(10)

        def _activate_preset(self, slot):
            calls.append(("preset", slot))

    owner = Owner()
    monkeypatch.setattr("amc_wx_lite.gui.os.name", "nt")
    monkeypatch.setattr(
        "amc_wx_lite.gui.wx.Window.FindFocus",
        lambda: object(),
        raising=False,
    )
    hotkey = _WindowsPreset10HotKey(owner)

    assert hotkey.register()
    assert ("register", hotkey._ID, 0x0002 | 0x0004 | 0x4000, 0x30) in calls
    owner.handler(None)
    assert ("native",) in calls
    assert ("preset", 10) in calls
    hotkey.unregister()
    assert ("unregister", hotkey._ID) in calls


def test_native_preset_10_routes_are_deduplicated() -> None:
    calls = []

    class Owner:
        filter_box = object()
        _keyboard_help_active = False
        _native_preset_10_guard_until = 0.0

        def _activate_preset(self, slot):
            calls.append(slot)

    owner = Owner()

    LiteFrame._trigger_preset_10_from_native_boundary(owner, now=10.0)
    LiteFrame._trigger_preset_10_from_native_boundary(owner, now=10.1)
    LiteFrame._trigger_preset_10_from_native_boundary(owner, now=10.3)

    assert calls == [10, 10]


def test_active_window_keeps_both_native_preset_10_routes() -> None:
    calls = []

    class Boundary:
        def __init__(self, name):
            self.name = name

        def register(self):
            calls.append((self.name, "register"))
            return True

        def install(self):
            calls.append((self.name, "install"))
            return True

        def unregister(self):
            calls.append((self.name, "unregister"))

        def uninstall(self):
            calls.append((self.name, "uninstall"))

    class Event:
        def Skip(self):
            calls.append(("event", "skip"))

        def GetActive(self):
            return True

    class Owner:
        _windows_preset_10_hotkey = Boundary("hotkey")
        _windows_preset_10_hook = Boundary("hook")

        def _reconcile_completed_external_moves(self):
            calls.append(("owner", "reconcile"))

    LiteFrame._on_window_activate(Owner(), Event())

    assert ("hotkey", "register") in calls
    assert ("hook", "install") in calls
    assert ("hook", "uninstall") not in calls


def test_relation_destinations_use_natural_short_labels_unless_details_enabled() -> None:
    labels = (
        "Przejdź do albumu",
        "Przejdź do wykonawcy",
        "Przejdź do podcastu",
        "Pokaż albumy wykonawcy",
        "Pokaż utwory wykonawcy",
    )

    assert relation_destination_labels(labels, detailed_hints=False) == (
        "Album",
        "Wykonawca",
        "Podcasty",
        "Albumy",
        "Utwory",
    )
    assert relation_destination_labels(labels, detailed_hints=True) == labels


def test_shift_f10_opens_the_list_context_menu_without_running_a_list_action() -> None:
    import wx

    calls: list[tuple[object, bool]] = []
    control = types.SimpleNamespace()
    frame = object.__new__(LiteFrame)
    frame._active_list = lambda: control
    frame._show_context_menu = lambda target, *, player: calls.append(
        (target, player)
    )

    frame._on_list_shortcut_hook(FakeKeyEvent(wx.WXK_F10, shift=True))

    assert calls == [(control, False)]


def test_native_context_event_is_bound_to_every_media_list() -> None:
    import wx

    bindings: list[tuple[object, object]] = []
    control = types.SimpleNamespace(
        Bind=lambda event_type, handler: bindings.append((event_type, handler))
    )
    frame = object.__new__(LiteFrame)

    frame._bind_list(control)

    assert (wx.EVT_CONTEXT_MENU, frame._on_list_context_menu) in bindings


def test_context_menu_reuses_only_enabled_user_facing_menu_entries() -> None:
    from amc_wx_lite import menu_model

    class Native:
        def __init__(self, label: str, *, enabled: bool) -> None:
            self.label = label
            self.enabled = enabled

        def IsEnabled(self):  # noqa: N802 - wx API
            return self.enabled

        def GetItemLabel(self):  # noqa: N802 - wx API
            return self.label

    frame = object.__new__(LiteFrame)
    frame.navigator = types.SimpleNamespace(
        active=SessionId.FILES,
        session=types.SimpleNamespace(
            model=types.SimpleNamespace(selected_row=object()),
        ),
    )
    frame._refresh_menu_state = lambda: None
    frame._menu_items = [
        (
            Native("&Otwórz zaznaczone\tEnter", enabled=True),
            menu_model.MenuItem(
                "&Otwórz zaznaczone",
                Action.ACTIVATE,
                shortcut="Return",
            ),
        ),
        (
            Native("&Skopiuj nazwę (Ctrl+C)", enabled=True),
            menu_model.MenuItem("&Skopiuj nazwę", Action.COPY_NAME),
        ),
        (
            Native("Zmień nazwę w Bibliote&ce (F2)", enabled=False),
            menu_model.MenuItem(
                "Zmień nazwę w Bibliote&ce",
                Action.RENAME_LIBRARY_ITEM,
            ),
        ),
    ]

    groups = frame._context_menu_command_groups(player=False)
    commands = [command for group in groups for command in group]

    assert [command.action for command in commands] == [
        Action.ACTIVATE,
        Action.COPY_NAME,
    ]
    assert [command.label for command in commands] == [
        "Otwórz zaznaczone (Enter)",
        "Skopiuj nazwę (Ctrl+C)",
    ]
    assert all("Action." not in command.label for command in commands)


def test_context_menu_shows_action_then_real_shortcut_without_access_letter() -> None:
    from amc_wx_lite import menu_model

    frame = object.__new__(LiteFrame)
    frame.navigator = types.SimpleNamespace(
        session=types.SimpleNamespace(
            model=types.SimpleNamespace(
                selected_row=types.SimpleNamespace(kind="playlist")
            )
        )
    )

    quick = menu_model.MenuItem(
        "Szybkie informacje o elemencie (&0)",
        Action.QUICK_INFORMATION,
        shortcut="Alt+T",
        accelerator=False,
    )
    rename = menu_model.MenuItem(
        "Zmień nazwę w Bibliote&ce",
        Action.RENAME_LIBRARY_ITEM,
        shortcut="F2",
        accelerator=False,
    )

    assert frame._context_menu_entry_label(
        quick, "Szybkie informacje o elemencie (&0) (Alt+T)"
    ) == "Szybkie informacje o elemencie (Alt+T)"
    assert frame._context_menu_entry_label(rename) == (
        "Zmień nazwę playlisty (F2)"
    )

    frame.navigator.session.model.selected_row = Row(
        "recording-1",
        "Nagranie.mp3",
        "track",
        path=r"D:\Nagrania\Nagranie.mp3",
    )
    copy_path = menu_model.MenuItem(
        "Skopiuj &adres lub ścieżkę",
        Action.COPY_ADDRESS,
        shortcut="Ctrl+Shift+C",
    )
    assert frame._context_menu_entry_label(copy_path) == (
        "Skopiuj ścieżkę do pliku (Ctrl+Shift+C)"
    )


def test_f2_renames_playlist_through_the_atomic_profile_snapshot() -> None:
    from amc_wx_lite import gui

    replacements: list[tuple[str, list[dict[str, object]]]] = []
    spoken: list[str] = []
    refreshed: list[str] = []
    focused: list[bool] = []

    class _RenameDialog:
        value = "Nowa nazwa"

        def __init__(self, _parent, **_kwargs) -> None:
            pass

        def __enter__(self):
            return self

        def __exit__(self, *_args) -> None:
            return None

        def ShowModal(self) -> int:
            return gui.wx.ID_OK

    client = types.SimpleNamespace(
        profile_playlists=lambda _session_id: {
            "playlists": [{
                "playlistId": "p1",
                "name": "Stara nazwa",
                "createdUtcTicks": 123,
                "itemIds": ["track-1"],
            }]
        },
        replace_profile_playlists=lambda session_id, playlists: (
            replacements.append((session_id, playlists)) or {}
        ),
    )

    def submit(_name, work, done, failed):
        try:
            done(work())
        except Exception as error:
            failed(error)

    frame = types.SimpleNamespace(
        navigator=types.SimpleNamespace(active=SessionId.FILES),
        _playlist_membership_pending=False,
        _profile_edit_client=lambda: client,
        runner=types.SimpleNamespace(submit=submit),
        announcer=types.SimpleNamespace(say=spoken.append),
        _restore_focus_after_dialog=lambda: focused.append(True),
        _refresh_profile_view=lambda preferred_id=None: refreshed.append(preferred_id),
    )
    previous = gui.RenameItemDialog
    gui.RenameItemDialog = _RenameDialog
    try:
        LiteFrame._rename_profile_playlist(
            frame,
            Row("playlist:p1", "Stara nazwa", "playlist"),
        )
    finally:
        gui.RenameItemDialog = previous

    assert replacements[0][0] == "local"
    assert replacements[0][1] == [{
        "playlistId": "p1",
        "name": "Nowa nazwa",
        "createdUtcTicks": 123,
        "itemIds": ["track-1"],
    }]
    assert refreshed == ["playlist:p1"]
    assert focused == [True]
    assert spoken[-1] == "Zmieniono nazwę playlisty: Nowa nazwa"
    assert frame._playlist_membership_pending is False


def test_spotify_context_relation_exposes_only_the_intentional_podcast_label() -> None:
    row = types.SimpleNamespace(
        kind="episode",
        item_id="episode-row",
        related_podcast_service_id="spotify:show:private-id",
        related_podcast_title="Podcast użytkowy",
    )
    frame = object.__new__(LiteFrame)
    frame.navigator = types.SimpleNamespace(
        active=SessionId.SPOTIFY,
        session=types.SimpleNamespace(
            model=types.SimpleNamespace(selected_row=row),
        ),
        _spotify_relation_choices=Navigator._spotify_relation_choices,
    )

    commands = frame._relation_context_menu_commands()

    assert [command.label for command in commands] == [
        "Przejdź do Podcast użytkowy"
    ]
    assert all("private-id" not in command.label for command in commands)


def test_native_context_popup_dispatches_once_and_restores_list_focus() -> None:
    from amc_wx_lite import gui

    handlers: dict[int, object] = {}
    labels: list[str] = []
    invoked: list[object] = []

    class Item:
        def __init__(self, identifier: int) -> None:
            self.identifier = identifier

        def GetId(self):  # noqa: N802 - wx API
            return self.identifier

        def Check(self, _checked):  # noqa: N802 - wx API
            pass

    class Menu:
        def __init__(self) -> None:
            self.identifiers: list[int] = []
            self.destroyed = False

        def Append(self, _identifier, label):  # noqa: N802 - wx API
            labels.append(label)
            item = Item(7000 + len(self.identifiers))
            self.identifiers.append(item.identifier)
            return item

        def AppendCheckItem(self, identifier, label):  # noqa: N802 - wx API
            return self.Append(identifier, label)

        def AppendSeparator(self):  # noqa: N802 - wx API
            labels.append("---")

        def Destroy(self):  # noqa: N802 - wx API
            self.destroyed = True

    class Control:
        focused = 0

        def PopupMenu(self, menu, *_position):  # noqa: N802 - wx API
            handlers[menu.identifiers[0]](None)

        def SetFocus(self):  # noqa: N802 - wx API
            self.focused += 1

    command = gui._ContextMenuCommand(
        label="&Skopiuj nazwę (Ctrl+C)",
        action=Action.COPY_NAME,
    )
    control = Control()
    frame = object.__new__(LiteFrame)
    frame.navigator = types.SimpleNamespace(view=View.LIST)
    frame.announcer = types.SimpleNamespace(say=lambda _text: None)
    frame._context_menu_command_groups = lambda *, player: ((command,),)
    frame._context_menu_position = lambda _control, _event: None
    frame._invoke_context_menu_command = invoked.append
    frame._active_list = lambda: control
    frame.Bind = lambda _event, handler, *, id: handlers.__setitem__(id, handler)
    frame.Unbind = lambda _event, *, id: handlers.pop(id, None) is not None

    original_menu = gui.wx.Menu
    gui.wx.Menu = Menu
    try:
        frame._show_context_menu(control, player=False)
    finally:
        gui.wx.Menu = original_menu

    assert labels == ["&Skopiuj nazwę (Ctrl+C)"]
    assert invoked == [command]
    assert control.focused == 1
    assert handlers == {}


def test_player_context_exposes_queue_navigation_with_readable_shortcuts() -> None:
    from amc_wx_lite import menu_model

    play_pause = next(
        item
        for menu in menu_model.menus_for_session(SessionId.SPOTIFY)
        for item in menu.items
        if item.action is Action.PLAY_PAUSE
    )
    shuffle = next(
        item
        for menu in menu_model.menus_for_session(SessionId.SPOTIFY)
        for item in menu.items
        if item.action is Action.TOGGLE_SHUFFLE
    )

    class Native:
        def __init__(self, label: str, checked: bool = False) -> None:
            self.label = label
            self.checked = checked

        def IsEnabled(self):  # noqa: N802 - wx API
            return True

        def GetItemLabel(self):  # noqa: N802 - wx API
            return self.label

        def IsChecked(self):  # noqa: N802 - wx API
            return self.checked

    frame = object.__new__(LiteFrame)
    frame.navigator = types.SimpleNamespace(
        active=SessionId.SPOTIFY,
        session=types.SimpleNamespace(
            model=types.SimpleNamespace(selected_row=None),
        ),
    )
    frame._menu_items = [
        (Native("&Pauza albo wznowienie (Spacja)"), play_pause),
        (Native("Odtwa&rzaj losowo (R)", checked=True), shuffle),
    ]
    frame._refresh_menu_state = lambda: None
    frame._action_available_in_active_session = lambda _action: True

    groups = frame._context_menu_command_groups(player=True)
    labels = {
        command.action: command.label
        for group in groups
        for command in group
    }

    assert labels[Action.QUEUE_PREVIOUS] == "Poprzedni utwór kolejki (Page Up)"
    assert labels[Action.QUEUE_NEXT] == "Następny utwór kolejki (Page Down)"
    assert labels[Action.TOGGLE_SHUFFLE] == "Odtwarzaj losowo (R)"
    shuffle_command = next(
        command
        for group in groups
        for command in group
        if command.action is Action.TOGGLE_SHUFFLE
    )
    assert shuffle_command.checkable is True
    assert shuffle_command.checked is True


def test_shift_f6_returns_to_the_list() -> None:
    import wx

    chord = chord_from_event(FakeKeyEvent(wx.WXK_F6, shift=True))
    assert chord.canonical == "Shift+F6"
    assert resolve(chord, player_view=True, radio_session=False) is Action.SHOW_LIST


def test_ctrl_f1_describes_keys_without_executing_them() -> None:
    import wx

    calls: list[str] = []
    frame = object.__new__(LiteFrame)
    frame._keyboard_help_active = False
    frame._refresh_menu_state = lambda: calls.append("menu")
    frame.announcer = types.SimpleNamespace(say=lambda text: calls.append(text))
    session = types.SimpleNamespace(library_view=LibraryView.ALL_FILES)
    frame.navigator = types.SimpleNamespace(
        view=View.LIST,
        active=SessionId.FILES,
        session=session,
        sessions={SessionId.FILES: session},
    )

    assert frame._handle_keyboard_help_event(
        FakeKeyEvent(wx.WXK_F1, ctrl=True)
    ) is True
    assert frame._keyboard_help_active is True
    assert "Pomoc klawiatury włączona" in calls[-1]

    # Ctrl+F zostaje tylko opisane: nie wywołujemy dispatchera ani okna
    # wyszukiwania. Jedynym skutkiem jest użytkowy komunikat w Announcerze.
    assert frame._handle_keyboard_help_event(
        FakeKeyEvent(ord("F"), ctrl=True)
    ) is True
    assert calls[-1] == (
        "Ctrl+F. Szukaj w bieżącej usłudze. Pliki lokalne"
    )
    assert frame._keyboard_help_active is True

    assert frame._handle_keyboard_help_event(
        FakeKeyEvent(wx.WXK_ESCAPE)
    ) is True
    assert frame._keyboard_help_active is False
    assert calls[-1] == "Pomoc klawiatury wyłączona"


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


def test_shift_space_menu_names_and_marks_the_actual_recording_state() -> None:
    """Shift+Spacja ma byc jednym stanowym poleceniem, nie trzema wpisami.

    NVDA musi uslyszec aktualna czynnosc: przy trwajacym nagraniu
    ``Wstrzymaj``, przy wstrzymanym ``Wznow``. Zaznaczenie pozycji oznacza
    stan wstrzymany. Bez nagrania polecenie pozostaje niedostepne i nie
    sugeruje, ze cos aktualnie nagrywamy.
    """
    from amc_wx_lite import menu_model

    entry = next(
        item
        for menu in menu_model.build_menus()
        for item in menu.items
        if item.action is Action.RECORD_PAUSE
    )

    class FakeMenuItem:
        def __init__(self) -> None:
            self.enabled = True
            self.checked = False
            self.label = ""

        def IsEnabled(self):  # noqa: N802 - API wx
            return self.enabled

        def Enable(self, value):  # noqa: N802 - API wx
            self.enabled = value

        def Check(self, value):  # noqa: N802 - API wx
            self.checked = value

        def GetItemLabel(self):  # noqa: N802 - API wx
            return self.label

        def SetItemLabel(self, value):  # noqa: N802 - API wx
            self.label = value

    selected = types.SimpleNamespace(
        kind="station",
        item_id="radio-poznan",
        url="https://example.test/radio",
    )
    session = types.SimpleNamespace(
        view=View.LIST,
        library_view=LibraryView.ALL_FILES,
        library_playlist_id=None,
        model=types.SimpleNamespace(selected_row=selected),
    )
    frame = object.__new__(LiteFrame)
    frame.navigator = types.SimpleNamespace(
        active=SessionId.RADIO,
        session=session,
        sessions={SessionId.RADIO: session},
    )
    frame._last_status = {}
    frame._podcast_inbox_sort_mode = ""
    frame._tidal_effective_sort_modes = {}
    frame.options = types.SimpleNamespace(
        radio_recognition_monitoring=False,
        radio_recognition_announcements=False,
    )
    frame._keyboard_help_active = False

    menu_item = FakeMenuItem()
    frame._menu_items = [(menu_item, entry)]

    frame._last_recording_status = {
        "recordings": [{"stationId": "radio-poznan", "paused": False}]
    }
    frame._refresh_menu_state()
    assert menu_item.enabled is True
    assert menu_item.checked is False
    assert menu_item.label.startswith("&Wstrzymaj nagrywanie")

    frame._last_recording_status = {
        "recordings": [{"stationId": "radio-poznan", "state": "paused"}]
    }
    frame._refresh_menu_state()
    assert menu_item.enabled is True
    assert menu_item.checked is True
    assert menu_item.label.startswith("&Wznów nagrywanie")

    frame._last_recording_status = {"recordings": []}
    frame._refresh_menu_state()
    assert menu_item.enabled is False
    assert menu_item.checked is False
    assert menu_item.label.startswith("&Wstrzymaj albo wznów nagrywanie")


def test_mute_menu_names_and_marks_the_active_session_state() -> None:
    """Ctrl+M ma opisywac stan aktywnej sesji, bez technicznych etykiet."""
    from amc_wx_lite import menu_model

    entry = next(
        item
        for menu in menu_model.build_menus()
        for item in menu.items
        if item.action is Action.TOGGLE_MUTE
    )

    class FakeMenuItem:
        def __init__(self) -> None:
            self.enabled = True
            self.checked = False
            self.label = ""

        def IsEnabled(self):  # noqa: N802 - API wx
            return self.enabled

        def Enable(self, value):  # noqa: N802 - API wx
            self.enabled = value

        def Check(self, value):  # noqa: N802 - API wx
            self.checked = value

        def GetItemLabel(self):  # noqa: N802 - API wx
            return self.label

        def SetItemLabel(self, value):  # noqa: N802 - API wx
            self.label = value

    session = types.SimpleNamespace(
        view=View.LIST,
        library_view=LibraryView.ALL_FILES,
        library_playlist_id=None,
        model=types.SimpleNamespace(selected_row=None),
    )
    frame = object.__new__(LiteFrame)
    frame.navigator = types.SimpleNamespace(
        active=SessionId.FILES,
        session=session,
        sessions={SessionId.FILES: session},
    )
    frame._last_status = {"engine": "media"}
    frame._last_recording_status = {"recordings": []}
    frame._podcast_inbox_sort_mode = ""
    frame._tidal_effective_sort_modes = {}
    frame.options = types.SimpleNamespace(
        radio_recognition_monitoring=False,
        radio_recognition_announcements=False,
    )
    frame._keyboard_help_active = False

    menu_item = FakeMenuItem()
    frame._menu_items = [(menu_item, entry)]
    frame._transport_status_by_session = {
        SessionId.FILES: {"muted": False},
    }

    frame._refresh_menu_state()
    assert menu_item.enabled is True
    assert menu_item.checked is False
    assert menu_item.label.startswith("Wycisz bieżącą sesję")

    frame._transport_status_by_session[SessionId.FILES]["muted"] = True
    frame._refresh_menu_state()
    assert menu_item.enabled is True
    assert menu_item.checked is True
    assert menu_item.label.startswith("Włącz dźwięk bieżącej sesji")


def test_membership_menu_gates_follow_the_selected_user_facing_item() -> None:
    """Menu nie oferuje operacji, którą wykonawca zaraz musiałby odrzucić."""
    from amc_wx_lite import menu_model

    entries = {
        action: next(
            item
            for menu in menu_model.build_menus()
            for item in menu.items
            if item.action is action
        )
        for action in (
            Action.ADD_TO_QUEUE,
            Action.TOGGLE_PLAY_NEXT,
            Action.TOGGLE_PODCAST_LIBRARY,
            Action.TIDAL_TOGGLE_LIBRARY,
            Action.SPOTIFY_TOGGLE_LIBRARY,
            Action.APPLE_MUSIC_TOGGLE_LIBRARY,
        )
    }

    class FakeMenuItem:
        def __init__(self) -> None:
            self.enabled = True

        def IsEnabled(self):  # noqa: N802 - API wx
            return self.enabled

        def Enable(self, value):  # noqa: N802 - API wx
            self.enabled = value

    def enabled(
        action: Action,
        active: SessionId,
        *,
        apple_resource_type: str | None = None,
        apple_context: str = "songs",
        **row_values: object,
    ) -> bool:
        defaults = {
            "kind": "",
            "item_id": "item",
            "url": "",
            "parent_id": "",
            "service_id": "",
            "related_album_service_id": "",
        }
        defaults.update(row_values)
        apple_record = None
        if apple_resource_type is None:
            row = types.SimpleNamespace(**defaults)
        else:
            from amc_wx_lite.apple_music_source import records_from_resources

            apple_record = records_from_resources([{
                "id": "library.song" if apple_resource_type.startswith("library-")
                else "catalog.album",
                "type": apple_resource_type,
                "attributes": {
                    "name": "Materiał Apple Music",
                    "playParams": {"catalogId": "catalog.item"},
                },
            }])[0]
            row = apple_record.row
        view = {
            SessionId.PODCASTS: LibraryView.PODCAST_LIBRARY,
            SessionId.TIDAL: LibraryView.TIDAL_LIBRARY,
            SessionId.SPOTIFY: LibraryView.SPOTIFY_LIBRARY,
        }.get(active, LibraryView.ALL_FILES)
        session = types.SimpleNamespace(
            view=View.LIST,
            library_view=view,
            library_playlist_id=None,
            model=types.SimpleNamespace(selected_row=row),
            spotify_context=None,
            apple_music_context=apple_context,
        )
        frame = object.__new__(LiteFrame)
        frame.navigator = types.SimpleNamespace(
            active=active,
            session=session,
            sessions={active: session},
        )
        frame._last_status = {}
        frame._last_recording_status = {"recordings": []}
        frame._transport_status_by_session = {}
        frame._podcast_inbox_sort_mode = ""
        frame._tidal_effective_sort_modes = {}
        frame._apple_music_records = (
            {row.item_id: apple_record} if apple_record is not None else {}
        )
        frame._keyboard_help_active = False
        native = FakeMenuItem()
        frame._menu_items = [(native, entries[action])]
        frame._refresh_menu_state()
        return native.enabled

    assert enabled(Action.ADD_TO_QUEUE, SessionId.FILES, kind="track")
    assert enabled(Action.TOGGLE_PLAY_NEXT, SessionId.FILES, kind="track")
    assert enabled(
        Action.ADD_TO_QUEUE,
        SessionId.PODCASTS,
        kind="episode",
        parent_id="podcast",
    )
    assert enabled(
        Action.TOGGLE_PODCAST_LIBRARY,
        SessionId.PODCASTS,
        kind="episode",
        parent_id="podcast",
    )
    assert not enabled(
        Action.TOGGLE_PODCAST_LIBRARY,
        SessionId.PODCASTS,
        kind="episode",
    )
    assert enabled(
        Action.ADD_TO_QUEUE,
        SessionId.TIDAL,
        kind="track",
        service_id="tidal-track",
        related_album_service_id="tidal-album",
    )
    assert not enabled(
        Action.ADD_TO_QUEUE,
        SessionId.TIDAL,
        kind="track",
        service_id="tidal-track",
    )
    assert enabled(
        Action.TIDAL_TOGGLE_LIBRARY,
        SessionId.TIDAL,
        kind="album",
        service_id="tidal-album",
    )
    assert not enabled(
        Action.TIDAL_TOGGLE_LIBRARY,
        SessionId.TIDAL,
        kind="track",
        service_id="tidal-track",
        related_album_service_id="tidal-album",
    )
    assert enabled(
        Action.SPOTIFY_TOGGLE_LIBRARY,
        SessionId.SPOTIFY,
        kind="album",
        service_id="spotify-album",
    )
    assert enabled(
        Action.SPOTIFY_TOGGLE_LIBRARY,
        SessionId.SPOTIFY,
        kind="episode",
        service_id="spotify-episode",
    )
    assert not enabled(
        Action.SPOTIFY_TOGGLE_LIBRARY,
        SessionId.SPOTIFY,
        kind="track",
        service_id="spotify-track",
    )
    assert enabled(
        Action.APPLE_MUSIC_TOGGLE_LIBRARY,
        SessionId.APPLE_MUSIC,
        apple_resource_type="albums",
        apple_context="playlist:example",
    )
    assert enabled(
        Action.APPLE_MUSIC_TOGGLE_LIBRARY,
        SessionId.APPLE_MUSIC,
        apple_resource_type="library-songs",
        apple_context="songs",
    )
    assert not enabled(
        Action.APPLE_MUSIC_TOGGLE_LIBRARY,
        SessionId.APPLE_MUSIC,
        apple_resource_type="library-songs",
        apple_context="playlist:example",
    )
