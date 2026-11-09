from __future__ import annotations

import os
from pathlib import Path
import subprocess
import sys
from types import SimpleNamespace

import pytest

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite import gui as gui_module
from amc_wx_lite.gui import LiteFrame, SonosAccountDialog
from amc_wx_lite.navigation import Navigator, SessionId, View


SOURCE = Path(__file__).parents[1] / "amc_wx_lite" / "gui.py"


def test_sonos_account_message_uses_only_explicit_labels() -> None:
    payload = {
        "message": "Konto Sonos zostało połączone.",
        "label": "Stan zapasowy",
        "token": "sekret",
        "technicalObject": {"state": "Connected"},
    }
    assert LiteFrame._sonos_account_message(payload) == (
        "Konto Sonos zostało połączone."
    )
    assert LiteFrame._sonos_account_message({"label": "Brak konta."}) == (
        "Brak konta."
    )


def test_sonos_dialog_has_one_explicit_information_field() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    dialog = source[source.index("class SonosAccountDialog") :]
    dialog = dialog[:dialog.index("class LibraryFoldersDialog")]
    assert 'SetName("Konto Sonos")' in dialog
    assert 'SetName("Informacje o logowaniu Sonos")' in dialog
    assert "self.instructions = wx.TextCtrl" not in dialog
    assert "self.status = wx.TextCtrl" in dialog
    assert "wx.TE_READONLY" in dialog
    assert "wx.TE_MULTILINE" in dialog
    assert 'SetName("Adres logowania Sonos")' in dialog
    assert 'label="Rozpocznij logowanie"' in dialog
    assert 'label="Otwórz stronę ponownie"' in dialog
    assert 'label="Sprawdź logowanie"' in dialog
    assert 'label="Odśwież dostęp"' in dialog
    assert 'label="Głośniki i grupy Sonos"' in dialog


def test_sonos_dialog_hides_login_when_public_service_is_unavailable() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    dialog = source[source.index("class SonosAccountDialog") :]
    dialog = dialog[:dialog.index("class LibraryFoldersDialog")]
    assert 'payload.get("canLogin") is not False' in dialog
    assert (
        "self.login_button.Show(can_login and not awaiting and not connected)"
        in dialog
    )
    assert "self.disconnect_button.Show(connected and can_disconnect)" in dialog
    assert 'return "status"' in dialog
    assert "wx.LaunchDefaultBrowser" in dialog
    assert 'open_browser=action == "loginStart"' in source
    finish = dialog[dialog.index("    def finish") :]
    finish = finish[:finish.index("    def fail")]
    assert "wx.LaunchDefaultBrowser(self._authorize_url)" in finish
    assert "self.check_button.SetFocus()" in finish
    assert "self.browser_opened = True" in finish
    assert "self.Close()" in finish
    assert "self.EndModal" not in dialog
    assert "str(payload)" not in dialog


def test_sonos_dialog_focuses_a_meaningful_control_for_every_account_state() -> None:
    focus = SonosAccountDialog._initial_focus_key

    assert focus(
        awaiting=True, connected=False, can_login=True
    ) == "check"
    assert focus(
        awaiting=False, connected=True, can_login=True
    ) == "targets"
    assert focus(
        awaiting=False, connected=False, can_login=True
    ) == "login"
    assert focus(
        awaiting=False, connected=False, can_login=False
    ) == "status"


def test_configure_sonos_starts_local_target_selection_without_account_dialog() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    section = source[source.index("    def _configure_session") :]
    section = section[:section.index("    def _show_library_folders")]
    assert "if session_id is SessionId.SONOS:" in section
    assert "self._select_sonos_target()" in section
    assert "self._show_sonos_account()" not in section
    assert 'SessionId.SONOS: "Wybierz dom i grupę Sonos"' not in section
    assert "elif isinstance(intent, OpenSonosAccount):" in source


def test_sonos_account_window_is_modeless_reusable_and_leaves_one_pending_row() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    show = source[source.index("    def _show_sonos_account") :]
    show = show[:show.index("    def _show_session_list")]
    assert "self._sonos_account_dialog" in show
    assert "existing.Raise()" in show
    assert "dialog.Bind(wx.EVT_CLOSE, close_account)" in show
    assert "dialog.Show()" in show
    assert "dialog.ShowModal()" not in show
    assert "self.navigator.apply_sonos_account_pending_prompt()" in show
    assert "if browser_opened and self.navigator.active is SessionId.SONOS" in show

    navigator = Navigator()
    navigator.active = SessionId.SONOS
    events = navigator.apply_sonos_account_pending_prompt()

    state = navigator.sessions[SessionId.SONOS]
    assert state.view is View.LIST
    assert len(state.model.rows) == 1
    row = state.model.rows[0]
    assert row.title == "Sprawdź logowanie do Sonos"
    assert row.detail == "Enter otwiera konto Sonos"
    assert row.kind == "sonosAccount"
    assert row.service_kind == "sonosAccount"
    assert "Po powrocie" in events[0].text


def test_main_close_releases_modeless_sonos_window_before_async_shutdown() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    close = source[source.rindex("    def _on_close(self, event: wx.CloseEvent)") :]
    close = close[: close.index("\n\n\nclass LiteApp")]
    assert "account_dialog = self._sonos_account_dialog" in close
    assert "self._sonos_account_dialog = None" in close
    assert "account_dialog.Destroy()" in close


def test_successful_browser_launch_releases_modal_parent() -> None:
    calls: list[object] = []
    original_launch = getattr(gui_module.wx, "LaunchDefaultBrowser", None)
    gui_module.wx.LaunchDefaultBrowser = lambda address: calls.append(address) or True
    dialog = SimpleNamespace(
        set_busy=lambda busy: calls.append(("busy", busy)),
        apply_payload=lambda payload, focus: calls.append(("payload", payload, focus)),
        _authorize_url="https://example.invalid/authorize",
        status=SimpleNamespace(SetFocus=lambda: calls.append("status-focus")),
        check_button=SimpleNamespace(
            IsShown=lambda: True,
            SetFocus=lambda: calls.append("check-focus"),
        ),
        fail=lambda message: calls.append(("fail", message)),
        browser_opened=False,
        Close=lambda: calls.append("close"),
    )
    try:
        SonosAccountDialog.finish(dialog, {"awaitingBrowser": True}, open_browser=True)
    finally:
        if original_launch is None:
            delattr(gui_module.wx, "LaunchDefaultBrowser")
        else:
            gui_module.wx.LaunchDefaultBrowser = original_launch

    assert dialog.browser_opened is True
    assert "https://example.invalid/authorize" in calls
    assert "close" in calls
    assert not any(isinstance(call, tuple) and call[0] == "fail" for call in calls)


def test_failed_browser_launch_keeps_modal_and_focuses_readable_status() -> None:
    calls: list[object] = []
    original_launch = getattr(gui_module.wx, "LaunchDefaultBrowser", None)
    gui_module.wx.LaunchDefaultBrowser = lambda address: calls.append(address) or False

    def fail(message: str) -> None:
        calls.append(("fail", message))
        calls.append("status-focus")

    dialog = SimpleNamespace(
        set_busy=lambda busy: calls.append(("busy", busy)),
        apply_payload=lambda payload, focus: calls.append(("payload", payload, focus)),
        _authorize_url="https://example.invalid/authorize",
        status=SimpleNamespace(SetFocus=lambda: calls.append("status-focus")),
        check_button=SimpleNamespace(
            IsShown=lambda: True,
            SetFocus=lambda: calls.append("check-focus"),
        ),
        fail=fail,
        browser_opened=False,
        Close=lambda: calls.append("close"),
    )
    try:
        SonosAccountDialog.finish(dialog, {"awaitingBrowser": True}, open_browser=True)
    finally:
        if original_launch is None:
            delattr(gui_module.wx, "LaunchDefaultBrowser")
        else:
            gui_module.wx.LaunchDefaultBrowser = original_launch

    assert dialog.browser_opened is False
    assert "status-focus" in calls
    assert any(isinstance(call, tuple) and call[0] == "fail" for call in calls)
    assert "close" not in calls


@pytest.mark.skipif(sys.platform != "win32", reason="Wymaga rzeczywistego wxPython na Windows")
def test_real_wx_modeless_sonos_window_keeps_parent_enabled() -> None:
    probe = Path(__file__).with_name("fixtures") / "sonos_modeless_probe.py"
    environment = dict(os.environ)
    environment["PYTHONPATH"] = str(SOURCE.parents[1])
    completed = subprocess.run(
        [sys.executable, str(probe)],
        cwd=SOURCE.parents[1],
        env=environment,
        text=True,
        capture_output=True,
        timeout=20,
        check=False,
        creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
    )
    assert completed.returncode == 0, completed.stdout + completed.stderr
    assert "MODELLESS_OK" in completed.stdout
