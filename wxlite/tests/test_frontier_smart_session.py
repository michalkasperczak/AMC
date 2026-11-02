from __future__ import annotations

from pathlib import Path
from types import MethodType, SimpleNamespace

import amc_wx_lite.gui as gui_module
from amc_wx_lite.command_palette import entries_for_session
from amc_wx_lite.frontier_smart_source import (
    FrontierSmartConfigurationPhase,
    configuration_state,
    device_rows,
    discovery_finished,
    discovery_started,
    mode_choices,
    now_playing_label,
    preset_choices,
    should_auto_discover_devices,
    snapshot,
)
from amc_wx_lite.host_client import LiteHostClient
from amc_wx_lite.gui import FrontierSmartDevicesDialog, LiteFrame
from amc_wx_lite.menu_model import menus_for_session
from amc_wx_lite.navigation import (
    ActivateFrontierSmartDevice,
    Navigator,
    SessionId,
    View,
)
from amc_wx_lite.release_profile import PUBLIC_PROFILE, TEST_PROFILE
from amc_wx_lite.shortcuts import (
    Action,
    Chord,
    FRONTIER_SMART_SUPPORTED_ACTIONS,
    describe,
    resolve,
)


def test_frontier_smart_exists_in_both_profiles_and_sonos_not_in_public() -> None:
    assert "frontierSmart" in TEST_PROFILE.session_ids
    assert "session.frontier-smart" in TEST_PROFILE.capabilities
    assert "frontierSmart" in PUBLIC_PROFILE.session_ids
    assert "session.frontier-smart" in PUBLIC_PROFILE.capabilities
    assert "sonos" not in PUBLIC_PROFILE.session_ids
    assert "session.sonos" not in PUBLIC_PROFILE.capabilities


def test_frontier_smart_device_activation_keeps_ids_out_of_spoken_rows() -> None:
    rows = device_rows({
        "items": [{
            "itemId": "frontier-smart:internal-device-id",
            "deviceId": "internal-device-id",
            "title": "Radio w salonie",
            "detail": "Firmware ir-mmi-FS2026-0500-088",
            "address": "192.168.1.55",
        }]
    })
    nav = Navigator()
    nav.apply_frontier_smart_devices(list(rows))
    nav.switch_session(SessionId.FRONTIER_SMART)

    intents = nav.activate_selected()

    assert len(intents) == 1
    assert isinstance(intents[0], ActivateFrontierSmartDevice)
    assert intents[0].title == "Radio w salonie"
    assert intents[0].device_id == "internal-device-id"
    assert nav.view is View.PLAYER
    spoken_cells = (rows[0].title, rows[0].kind_label, rows[0].detail)
    assert spoken_cells == (
        "Radio w salonie",
        "urządzenie",
        "Firmware ir-mmi-FS2026-0500-088",
    )
    assert "192.168.1.55" not in " ".join(spoken_cells)
    assert "internal-device-id" not in " ".join(spoken_cells)


def test_frontier_smart_payloads_are_allowlisted_for_accessible_text() -> None:
    state = snapshot({
        "deviceId": "internal-device-id",
        "deviceTitle": "Radio w salonie",
        "title": "Trójka",
        "subtitle": "Audycja wieczorna",
        "artist": "Polskie Radio",
        "playbackState": "odtwarzanie",
        "volume": 42,
        "muted": False,
        "positionSeconds": 15,
        "durationSeconds": 3600,
        "address": "192.168.1.55",
        "runtimeObject": "FrontierSmartDeviceSettings { Address = 192.168.1.55 }",
    })
    assert now_playing_label(state) == (
        "Trójka, Audycja wieczorna, Polskie Radio"
    )
    assert "address" not in state
    assert "runtimeObject" not in state
    assert "192.168.1.55" not in repr(state)

    title, presets = preset_choices({
        "deviceTitle": "Radio w salonie",
        "items": [{"presetKey": 0, "name": "Trójka", "label": "Trójka, preset 1"}],
    })
    assert title == "Radio w salonie"
    assert presets[0].label == "Trójka, preset 1"

    title, modes, selected = mode_choices({
        "deviceTitle": "Radio w salonie",
        "items": [
            {"key": 0, "label": "Radio internetowe", "selected": True, "enabled": True},
            {"key": 1, "label": "DAB, niedostępny do bezpośredniego wyboru", "selected": False, "enabled": False},
        ],
    })
    assert title == "Radio w salonie"
    assert selected == 0
    assert [choice.label for choice in modes] == [
        "Radio internetowe",
        "DAB, niedostępny do bezpośredniego wyboru",
    ]


def test_frontier_smart_host_client_uses_narrow_named_operations() -> None:
    calls: list[tuple[str, dict | None, float]] = []
    client = LiteHostClient(Path("host.exe"))
    client.call = lambda op, args=None, *, timeout=20.0: (
        calls.append((op, args, timeout)) or {}
    )

    client.frontier_smart_devices()
    client.frontier_smart_discover()
    client.frontier_smart_identify("192.168.1.55")
    client.frontier_smart_add("192.168.1.55", "1234")
    client.frontier_smart_select("device-1")
    client.frontier_smart_snapshot("device-1")
    client.frontier_smart_transport("device-1", "setVolume", volume=37)
    client.frontier_smart_modes("device-1")
    client.frontier_smart_set_mode("device-1", 2)
    client.frontier_smart_presets("device-1")
    client.frontier_smart_activate_preset("device-1", 4)
    client.frontier_smart_remove("device-1")

    assert calls == [
        ("frontierSmart.devices", None, 15.0),
        ("frontierSmart.discover", None, 15.0),
        ("frontierSmart.identify", {"address": "192.168.1.55"}, 15.0),
        ("frontierSmart.add", {"address": "192.168.1.55", "pin": "1234"}, 30.0),
        ("frontierSmart.select", {"deviceId": "device-1"}, 15.0),
        ("frontierSmart.snapshot", {"deviceId": "device-1"}, 30.0),
        (
            "frontierSmart.transport",
            {"deviceId": "device-1", "command": "setVolume", "volume": 37},
            30.0,
        ),
        ("frontierSmart.modes", {"deviceId": "device-1"}, 30.0),
        ("frontierSmart.modeSet", {"deviceId": "device-1", "modeKey": 2}, 30.0),
        ("frontierSmart.presets", {"deviceId": "device-1"}, 30.0),
        (
            "frontierSmart.presetActivate",
            {"deviceId": "device-1", "presetKey": 4},
            30.0,
        ),
        ("frontierSmart.remove", {"deviceId": "device-1"}, 15.0),
    ]


def test_frontier_smart_commands_have_menu_help_palette_and_execution_route() -> None:
    assert resolve(
        Chord("F5", ctrl=True),
        player_view=False,
        radio_session=False,
        frontier_smart_session=True,
    ) is Action.SESSION_CONFIGURE_CURRENT
    assert resolve(
        Chord("M", ctrl=True, alt=True),
        player_view=True,
        radio_session=False,
        frontier_smart_session=True,
    ) is Action.FRONTIER_SMART_SELECT_MODE

    actions = {
        item.action
        for menu in menus_for_session(SessionId.FRONTIER_SMART)
        for item in menu.items
        if item.action is not None
    }
    assert {
        Action.SESSION_CONFIGURE_CURRENT,
        Action.FRONTIER_SMART_SELECT_MODE,
        Action.VIEW_PRESETS,
        Action.PLAY_PAUSE,
        Action.TOGGLE_MUTE,
    } <= actions
    assert Action.RECORD_TOGGLE not in actions
    assert Action.SELECT_AUDIO_OUTPUT in actions

    mode_item = next(
        item
        for menu in menus_for_session(SessionId.FRONTIER_SMART)
        for item in menu.items
        if item.action is Action.FRONTIER_SMART_SELECT_MODE
    )
    assert mode_item.shortcut == "Ctrl+Alt+M"
    assert not mode_item.accelerator

    help_entries = describe(
        "Frontier Smart",
        available_actions=set(FRONTIER_SMART_SUPPORTED_ACTIONS),
    )
    assert dict(help_entries)["Ctrl+F5"] == (
        "Pokaż zapisane urządzenia Frontier Smart i opcjonalnie wyszukaj nowe"
    )
    assert dict(help_entries)["Ctrl+Alt+M"] == (
        "Wybierz tryb urządzenia Frontier Smart"
    )

    palette = entries_for_session(
        SessionId.FRONTIER_SMART,
        [session.value for session in SessionId],
    )
    palette_actions = {entry.action for entry in palette}
    assert Action.SESSION_CONFIGURE_CURRENT in palette_actions
    assert Action.FRONTIER_SMART_SELECT_MODE in palette_actions
    mode_entry = next(
        entry
        for entry in palette
        if entry.action is Action.FRONTIER_SMART_SELECT_MODE
    )
    assert mode_entry.name == "Wybierz tryb Frontier Smart…"
    assert mode_entry.shortcut == "Ctrl+Alt+M"

    calls: list[str] = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.FRONTIER_SMART),
        announcer=SimpleNamespace(say=lambda _message: None),
        _show_frontier_smart_modes=lambda: calls.append("mode-picker"),
    )
    LiteFrame._dispatch(frame, Action.FRONTIER_SMART_SELECT_MODE)
    assert calls == ["mode-picker"]

    source = (
        Path(__file__).parents[1] / "amc_wx_lite" / "gui.py"
    ).read_text(encoding="utf-8")
    mode_picker = source[source.index("def _show_frontier_smart_modes") :]
    mode_picker = mode_picker[:mode_picker.index("def _show_frontier_smart_presets")]
    assert 'self.announcer.say("Pobieram tryby Frontier Smart")' in mode_picker


def test_frontier_smart_device_dialog_has_explicit_names_and_keyboard_contract() -> None:
    source = (
        Path(__file__).parents[1] / "amc_wx_lite" / "gui.py"
    ).read_text(encoding="utf-8")
    dialog = source[source.index("class FrontierSmartDevicesDialog") :]
    dialog = dialog[:dialog.index("class SessionSelectionDialog")]

    assert 'self.devices.SetName("Zapisane urządzenia Frontier Smart")' in dialog
    assert 'self.address.SetName("Lokalny adres IP urządzenia")' in dialog
    assert (
        'self.pin.SetName("PIN NetRemote urządzenia Frontier Smart, pole chronione")'
        in dialog
    )
    assert 'self.manual_button.SetName("Wpisz lokalny adres IP ręcznie")' in dialog
    assert (
        'self.discover_button.SetName("Szukaj nowych urządzeń w sieci lokalnej")'
        in dialog
    )
    assert "self.Bind(wx.EVT_CHAR_HOOK, self._on_key)" in dialog
    assert "self.devices.Bind(wx.EVT_LISTBOX_DCLICK, self._on_select)" in dialog
    assert "self.address.Bind(wx.EVT_TEXT, self._on_address_changed)" in dialog
    assert "self.devices.HasFocus()" in dialog
    assert "event.HasAnyModifiers()" in dialog
    assert "self.save_button.SetDefault()" not in dialog
    assert "self.Bind(wx.EVT_INIT_DIALOG, self._on_init_dialog)" in dialog
    assert "wx.CallAfter(self._begin_initial_flow)" in dialog
    assert "self.devices.SetFocus()" in dialog
    assert "self.discover_button.SetFocus()" in dialog
    assert "self.address_label.Hide()" in dialog
    assert "self.address.Hide()" in dialog
    assert "self.pin_label.Hide()" in dialog
    assert "self.pin.Hide()" in dialog
    assert "self.save_button.MoveAfterInTabOrder(self.pin)" in dialog
    assert "self.manual_button.SetFocus()" in dialog
    assert "self.pin.SetFocus()" in dialog
    assert 'self.save_button.SetLabel(i18n.ui_text("&Sprawdź urządzenie"))' in dialog
    assert "if not self._state.pin_allowed:" in dialog
    assert "close_after=True" not in dialog


class _FakeControl:
    def __init__(self) -> None:
        self.focused = False
        self.shown = False
        self.hidden = False
        self.enabled = True
        self.value = ""
        self.name = ""
        self.label = ""

    def SetFocus(self) -> None:
        self.focused = True

    def Show(self) -> None:
        self.shown = True
        self.hidden = False

    def Hide(self) -> None:
        self.hidden = True
        self.shown = False

    def SetValue(self, value: str) -> None:
        self.value = value

    def SetName(self, name: str) -> None:
        self.name = name

    def GetValue(self) -> str:
        return self.value

    def SetLabel(self, label: str) -> None:
        self.label = label

    def Enable(self, enabled: bool) -> None:
        self.enabled = enabled


def test_frontier_smart_empty_session_starts_discovery_without_focusing_close() -> None:
    devices = _FakeControl()
    discover = _FakeControl()
    started: list[bool] = []
    dialog = SimpleNamespace(
        _devices=[],
        _state=configuration_state({"items": []}),
        _automatic_discovery_started=False,
        devices=devices,
        discover_button=discover,
        _start_discovery=lambda: started.append(True),
    )

    FrontierSmartDevicesDialog._begin_initial_flow(dialog)

    assert devices.focused is False
    assert discover.focused is True
    assert dialog._automatic_discovery_started is True
    assert started == [True]


def test_frontier_smart_reopening_saved_devices_focuses_list_without_discovery() -> None:
    devices = _FakeControl()
    discover = _FakeControl()
    started: list[bool] = []
    dialog = SimpleNamespace(
        _devices=[("radio-1", "Radio w salonie")],
        _state=configuration_state({
            "items": [{
                "itemId": "frontier:radio-1",
                "deviceId": "radio-1",
                "title": "Radio w salonie",
            }]
        }),
        _automatic_discovery_started=False,
        devices=devices,
        discover_button=discover,
        _start_discovery=lambda: started.append(True),
    )

    FrontierSmartDevicesDialog._begin_initial_flow(dialog)

    assert devices.focused is True
    assert discover.focused is False
    assert started == []


def test_frontier_smart_discovery_keeps_a_named_focus_target_enabled() -> None:
    discover = _FakeControl()
    manual = _FakeControl()
    save = _FakeControl()
    select = _FakeControl()
    remove = _FakeControl()
    close = _FakeControl()
    address = _FakeControl()
    dialog = SimpleNamespace(
        selected_device_id=None,
        _busy=True,
        _manual_address_visible=False,
        _state=discovery_started(configuration_state({"items": []})),
        address=address,
        discover_button=discover,
        manual_button=manual,
        save_button=save,
        select_button=select,
        remove_button=remove,
        close_button=close,
    )

    FrontierSmartDevicesDialog._update_buttons(dialog)

    assert discover.enabled is True
    assert discover.label == "&Wyszukiwanie urządzeń, czekaj"
    assert discover.name == "Wyszukiwanie urządzeń w sieci lokalnej, czekaj"
    assert manual.enabled is False
    assert close.enabled is True


def test_frontier_smart_discovery_announces_device_before_pin_and_focuses_pin() -> None:
    address = _FakeControl()
    pin = _FakeControl()
    pin_label = _FakeControl()
    save_button = _FakeControl()
    statuses: list[tuple[str, bool]] = []
    state = discovery_finished(
        discovery_started(configuration_state({"items": []})),
        {
            "items": [{
                "address": "192.168.1.55",
                "label": "Teufel RADIO 3SIXTY",
            }],
            "message": "Wykryto urządzenie Teufel RADIO 3SIXTY.",
        },
    )
    dialog = SimpleNamespace(
        _busy=True,
        _state=state,
        address=address,
        pin=pin,
        pin_label=pin_label,
        save_button=save_button,
        Layout=lambda: None,
        _update_buttons=lambda: None,
        set_status=lambda message, announce=False: statuses.append(
            (message, announce)
        ),
    )
    dialog._show_pin_field = MethodType(
        FrontierSmartDevicesDialog._show_pin_field,
        dialog,
    )

    FrontierSmartDevicesDialog._accept_identified_device(
        dialog,
        "192.168.1.55",
        "Teufel RADIO 3SIXTY",
    )

    assert dialog._state.phase is FrontierSmartConfigurationPhase.WAITING_FOR_PIN
    assert address.value == "192.168.1.55"
    assert pin_label.shown is True
    assert pin.shown is True
    assert pin.focused is True
    assert pin.name == "PIN NetRemote urządzenia Teufel RADIO 3SIXTY, pole chronione"
    assert statuses == [(
        "Wykryto urządzenie Teufel RADIO 3SIXTY. "
        "Wpisz czterocyfrowy PIN NetRemote, nie hasło Wi-Fi. "
        "W Teufel RADIO 3SIXTY ustawienie fabryczne to 1234; można je "
        "sprawdzić w menu radia: Ustawienia systemowe, Sieć, Ustawienia "
        "NetRemote PIN. Następnie wybierz Połącz i zapisz.",
        True,
    )]
    assert "192.168.1.55" not in statuses[0][0]


def test_frontier_smart_single_discovery_requires_explicit_choice() -> None:
    pin = _FakeControl()
    pin_label = _FakeControl()
    discover = _FakeControl()
    save_button = _FakeControl()
    statuses: list[tuple[str, bool]] = []
    accepted: list[tuple[str, str]] = []
    shown: list[tuple[str, list[str], int]] = []

    class ChoiceDialog:
        def __init__(self, _parent, _message, title, choices) -> None:
            self.title = title
            self.choices = list(choices)
            self.selection = -1

        def __enter__(self):
            return self

        def __exit__(self, *_args) -> None:
            return None

        def SetName(self, name: str) -> None:
            assert name == "Wykryte urządzenia Frontier Smart"

        def SetSelection(self, selection: int) -> None:
            self.selection = selection

        def ShowModal(self) -> int:
            shown.append((self.title, self.choices, self.selection))
            return gui_module.wx.ID_CANCEL

        def GetSelection(self) -> int:
            return self.selection

    original_choice_dialog = getattr(gui_module.wx, "SingleChoiceDialog", None)
    gui_module.wx.SingleChoiceDialog = ChoiceDialog
    dialog = SimpleNamespace(
        _state=discovery_started(configuration_state({"items": []})),
        _busy=True,
        _manual_address_visible=False,
        pin=pin,
        pin_label=pin_label,
        discover_button=discover,
        save_button=save_button,
        Layout=lambda: None,
        _update_buttons=lambda: None,
        set_status=lambda message, announce=False: statuses.append(
            (message, announce)
        ),
        _accept_identified_device=lambda address, label: accepted.append(
            (address, label)
        ),
    )
    dialog._hide_pin_field = MethodType(
        FrontierSmartDevicesDialog._hide_pin_field,
        dialog,
    )

    try:
        FrontierSmartDevicesDialog.use_discovered(
            dialog,
            {
                "items": [{
                    "address": "192.168.1.55",
                    "label": "Teufel RADIO 3SIXTY",
                }],
                "message": "Wykryto urządzenie Teufel RADIO 3SIXTY.",
            },
        )
    finally:
        if original_choice_dialog is None:
            delattr(gui_module.wx, "SingleChoiceDialog")
        else:
            gui_module.wx.SingleChoiceDialog = original_choice_dialog

    assert shown == [(
        "Wykryte urządzenia Frontier Smart",
        ["Teufel RADIO 3SIXTY"],
        0,
    )]
    assert accepted == []
    assert pin.shown is False and pin.hidden is True
    assert discover.focused is True
    assert statuses[-1][1] is True
    assert "naciśnij Enter" in statuses[-1][0]


def test_frontier_smart_confirmed_choice_enters_pin_step() -> None:
    pin = _FakeControl()
    pin_label = _FakeControl()
    discover = _FakeControl()
    save_button = _FakeControl()
    accepted: list[tuple[str, str]] = []

    class ChoiceDialog:
        def __init__(self, _parent, _message, _title, choices) -> None:
            assert list(choices) == ["Radio w kuchni"]
            self.selection = -1

        def __enter__(self):
            return self

        def __exit__(self, *_args) -> None:
            return None

        def SetName(self, _name: str) -> None:
            pass

        def SetSelection(self, selection: int) -> None:
            self.selection = selection

        def ShowModal(self) -> int:
            return gui_module.wx.ID_OK

        def GetSelection(self) -> int:
            return self.selection

    original_choice_dialog = getattr(gui_module.wx, "SingleChoiceDialog", None)
    gui_module.wx.SingleChoiceDialog = ChoiceDialog
    dialog = SimpleNamespace(
        _state=discovery_started(configuration_state({"items": []})),
        _busy=True,
        _manual_address_visible=False,
        pin=pin,
        pin_label=pin_label,
        discover_button=discover,
        save_button=save_button,
        Layout=lambda: None,
        _update_buttons=lambda: None,
        set_status=lambda _message, announce=False: None,
        _accept_identified_device=lambda address, label: accepted.append(
            (address, label)
        ),
    )
    dialog._hide_pin_field = MethodType(
        FrontierSmartDevicesDialog._hide_pin_field,
        dialog,
    )

    try:
        FrontierSmartDevicesDialog.use_discovered(
            dialog,
            {
                "items": [{
                    "address": "192.168.1.77",
                    "label": "Radio w kuchni",
                }],
            },
        )
    finally:
        if original_choice_dialog is None:
            delattr(gui_module.wx, "SingleChoiceDialog")
        else:
            gui_module.wx.SingleChoiceDialog = original_choice_dialog

    assert accepted == [("192.168.1.77", "Radio w kuchni")]


def test_frontier_smart_empty_result_has_no_pin_and_focuses_retry() -> None:
    pin = _FakeControl()
    pin_label = _FakeControl()
    discover = _FakeControl()
    save_button = _FakeControl()
    statuses: list[tuple[str, bool]] = []
    dialog = SimpleNamespace(
        _state=discovery_started(configuration_state({"items": []})),
        _busy=True,
        _manual_address_visible=False,
        pin=pin,
        pin_label=pin_label,
        discover_button=discover,
        save_button=save_button,
        Layout=lambda: None,
        _update_buttons=lambda: None,
        set_status=lambda message, announce=False: statuses.append(
            (message, announce)
        ),
    )
    dialog._hide_pin_field = MethodType(
        FrontierSmartDevicesDialog._hide_pin_field,
        dialog,
    )

    FrontierSmartDevicesDialog.use_discovered(
        dialog,
        {
            "items": [],
            "message": "Nie wykryto potwierdzonego urządzenia.",
        },
    )

    assert dialog._state.phase is FrontierSmartConfigurationPhase.EMPTY_RESULT
    assert pin.shown is False
    assert pin.hidden is True
    assert discover.focused is True
    assert "aby ponowić" in statuses[0][0]


def test_frontier_smart_discovery_error_has_no_pin_and_focuses_retry() -> None:
    address = _FakeControl()
    pin = _FakeControl()
    pin_label = _FakeControl()
    discover = _FakeControl()
    save_button = _FakeControl()
    statuses: list[tuple[str, bool]] = []
    dialog = SimpleNamespace(
        _state=discovery_started(configuration_state({"items": []})),
        _busy=True,
        _manual_address_visible=False,
        address=address,
        pin=pin,
        pin_label=pin_label,
        discover_button=discover,
        save_button=save_button,
        Layout=lambda: None,
        _update_buttons=lambda: None,
        set_status=lambda message, announce=False: statuses.append(
            (message, announce)
        ),
    )
    dialog._hide_pin_field = MethodType(
        FrontierSmartDevicesDialog._hide_pin_field,
        dialog,
    )

    FrontierSmartDevicesDialog.discovery_failed(
        dialog,
        "Nie udało się wykryć urządzeń.",
    )

    assert dialog._state.phase is FrontierSmartConfigurationPhase.ERROR
    assert pin.hidden is True
    assert discover.focused is True
    assert statuses[0][1] is True


def test_frontier_smart_manual_address_is_verified_before_pin() -> None:
    address = _FakeControl()
    address_label = _FakeControl()
    pin = _FakeControl()
    pin_label = _FakeControl()
    save_button = _FakeControl()
    identify_calls: list[str] = []
    busy_messages: list[str] = []
    dialog = SimpleNamespace(
        _state=configuration_state({"items": []}),
        _busy=False,
        _manual_address_visible=False,
        address=address,
        address_label=address_label,
        pin=pin,
        pin_label=pin_label,
        save_button=save_button,
        Layout=lambda: None,
        set_status=lambda *_args, **_kwargs: None,
        _update_buttons=lambda: None,
        set_busy=lambda message: busy_messages.append(message),
        _identify=lambda value, _dialog: identify_calls.append(value),
    )
    dialog._hide_pin_field = MethodType(
        FrontierSmartDevicesDialog._hide_pin_field,
        dialog,
    )

    FrontierSmartDevicesDialog._on_manual(dialog, None)

    assert dialog._state.phase is FrontierSmartConfigurationPhase.MANUAL_ADDRESS
    assert address.focused is True
    assert pin.shown is False
    assert pin.hidden is True
    assert save_button.label == "&Sprawdź urządzenie"

    address.SetValue("192.168.1.55")
    FrontierSmartDevicesDialog._on_save(dialog, None)

    assert dialog._state.phase is FrontierSmartConfigurationPhase.IDENTIFYING
    assert identify_calls == ["192.168.1.55"]
    assert busy_messages == ["Sprawdzanie urządzenia pod podanym adresem"]
    assert pin.shown is False


def test_frontier_smart_session_entry_opens_first_configuration_only_when_empty(
    monkeypatch,
) -> None:
    scheduled: list[object] = []
    monkeypatch.setattr(
        gui_module.wx,
        "CallAfter",
        lambda callback, *args: callback(*args),
    )

    class ImmediateRunner:
        @staticmethod
        def submit(_name, work, done, _failed) -> None:
            done(work())

    def exercise(payload: object) -> list[object]:
        opened: list[object] = []
        frame = SimpleNamespace(
            client=SimpleNamespace(frontier_smart_devices=lambda: payload),
            announcer=SimpleNamespace(say=lambda message: scheduled.append(message)),
            runner=ImmediateRunner(),
            navigator=SimpleNamespace(active=SessionId.FRONTIER_SMART),
            _apply_frontier_smart_devices_payload=lambda *_args: None,
            _show_frontier_smart_devices=lambda initial: opened.append(initial),
        )
        LiteFrame._load_frontier_smart_devices(
            frame,
            configure_if_empty=True,
        )
        return opened

    empty = {"items": [], "selectedDeviceId": "", "message": ""}
    saved = {
        "items": [{
            "itemId": "frontier:radio-1",
            "deviceId": "radio-1",
            "title": "Radio w salonie",
            "detail": "Frontier Smart",
        }],
        "selectedDeviceId": "radio-1",
        "message": "Gotowe",
    }

    assert exercise(empty) == [empty]
    assert exercise(saved) == []

    source = (
        Path(__file__).parents[1] / "amc_wx_lite" / "gui.py"
    ).read_text(encoding="utf-8")
    switch = source[source.index("    def _switch_session") :]
    switch = switch[:switch.index("    def _session_readiness")]
    assert "configure_if_empty=True" in switch


def test_frontier_smart_ctrl_f5_uses_management_flow_without_second_auto_open(
    monkeypatch,
) -> None:
    switched: list[tuple[SessionId, bool]] = []
    opened: list[bool] = []
    monkeypatch.setattr(
        gui_module.wx,
        "CallAfter",
        lambda callback, *args: callback(*args),
    )
    frame = SimpleNamespace(
        options=SimpleNamespace(
            session_order=[session.value for session in SessionId]
        ),
        announcer=SimpleNamespace(say=lambda _message: None),
        _switch_session=lambda session, *, configure_frontier_if_empty=True: (
            switched.append((session, configure_frontier_if_empty))
        ),
        _show_frontier_smart_devices=lambda: opened.append(True),
    )

    LiteFrame._configure_session(frame, SessionId.FRONTIER_SMART)

    assert switched == [(SessionId.FRONTIER_SMART, False)]
    assert opened == [True]


def test_frontier_smart_auto_discovers_only_without_saved_devices() -> None:
    assert should_auto_discover_devices({"items": []}) is True
    assert should_auto_discover_devices({
        "items": [
            {
                "itemId": "frontier:radio-1",
                "deviceId": "radio-1",
                "title": "Radio w salonie",
                "detail": "Frontier Smart",
            }
        ]
    }) is False

    old_false_result = discovery_finished(
        discovery_started(configuration_state({"items": []})),
        {
            "items": [{
                "address": "192.168.1.55",
                "label": (
                    "Wykryte urządzenie Frontier Smart, adres 192.168.1.55"
                ),
            }]
        },
    )
    assert old_false_result.phase is FrontierSmartConfigurationPhase.EMPTY_RESULT
    assert old_false_result.discovered_devices == ()

    source = (
        Path(__file__).parents[1] / "amc_wx_lite" / "gui.py"
    ).read_text(encoding="utf-8")
    flow = source[source.index("    def _show_frontier_smart_devices") :]
    flow = flow[:flow.index("    def _activate_frontier_smart_device")]
    assert "auto_discover=" not in flow
    assert "with FrontierSmartDevicesDialog(" in flow
    assert "self._state = frontier_smart_configuration_state(payload)" in source
    assert 'label="&Szukaj nowych urządzeń"' in source


def test_frontier_smart_success_closes_configuration_and_returns_to_device_list() -> None:
    source = (
        Path(__file__).parents[1] / "amc_wx_lite" / "gui.py"
    ).read_text(encoding="utf-8")
    flow = source[source.index("    def _show_frontier_smart_devices") :]
    flow = flow[:flow.index("    def _activate_frontier_smart_device")]

    assert "announce=self.announcer.say" in flow
    assert "lambda payload: finish(payload, dialog, close_after=True)" in flow
    assert "close_after=True" in flow
    assert "dialog.fail(str(error), focus_pin=focus_pin)" in flow
    assert "return not dialog.IsBeingDeleted()" in flow
    assert "self._restore_focus_after_dialog()" in flow


def test_frontier_smart_volume_keeps_only_the_latest_waiting_value() -> None:
    submissions: list[tuple[object, object, object]] = []
    sent: list[int] = []
    slider_values: list[int] = []
    applied: list[tuple[int | None, bool]] = []

    class DeferredRunner:
        @staticmethod
        def submit(_name, work, done, failed) -> None:
            submissions.append((work, done, failed))

    def transport(_device_id, _command, *, volume=None):
        sent.append(volume)
        return {
            "deviceId": "radio-1",
            "deviceTitle": "Teufel",
            "title": "Radio Poznań",
            "playbackState": "odtwarzanie",
            "volume": volume,
        }

    frame = SimpleNamespace(
        client=SimpleNamespace(frontier_smart_transport=transport),
        _frontier_smart_active_device_id="radio-1",
        _frontier_smart_request_pending=False,
        _frontier_smart_volume_pending=None,
        _frontier_smart_requested_volume=20,
        _frontier_smart_snapshot={"volume": 20},
        volume_slider=SimpleNamespace(SetValue=slider_values.append),
        announcer=SimpleNamespace(say=lambda _message: None),
        navigator=SimpleNamespace(active=SessionId.FRONTIER_SMART),
        runner=DeferredRunner(),
        _apply_frontier_smart_snapshot=lambda state, *, announce=False: (
            applied.append((state.get("volume"), announce))
        ),
    )
    frame._queue_frontier_smart_volume = MethodType(
        LiteFrame._queue_frontier_smart_volume,
        frame,
    )

    frame._queue_frontier_smart_volume(25)
    frame._queue_frontier_smart_volume(30)
    frame._queue_frontier_smart_volume(35)
    assert len(submissions) == 1
    assert frame._frontier_smart_volume_pending == 35
    assert slider_values[-1] == 35

    work, done, _failed = submissions.pop(0)
    done(work())
    assert sent == [25]
    assert len(submissions) == 1

    work, done, _failed = submissions.pop(0)
    done(work())
    assert sent == [25, 35]
    assert frame._frontier_smart_request_pending is False
    assert frame._frontier_smart_volume_pending is None
    assert applied[-1] == (35, True)
