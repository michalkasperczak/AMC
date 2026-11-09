from __future__ import annotations

from pathlib import Path
from types import SimpleNamespace

from test_gui_logic import install_wx_stub

install_wx_stub()

import amc_wx_lite.gui as gui_module
from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.navigation import SessionId


SOURCE = Path(__file__).parents[1] / "amc_wx_lite" / "gui.py"


def test_account_status_uses_only_explicit_user_facing_labels() -> None:
    spotify = LiteFrame._service_account_status_message(
        SessionId.SPOTIFY,
        {
            "catalog": {"label": "Katalog Spotify jest połączony: Michał."},
            "playback": {"label": "Konto odtwarzania Spotify jest zapisane."},
            "technicalObject": {"id": "secret"},
        },
    )
    tidal = LiteFrame._service_account_status_message(
        SessionId.TIDAL,
        {"label": "Konto TIDAL jest połączone.", "token": "nie czytaj"},
    )

    assert spotify == (
        "Katalog Spotify jest połączony: Michał. "
        "Konto odtwarzania Spotify jest zapisane."
    )
    assert tidal == "Konto TIDAL jest połączone."
    assert "secret" not in spotify
    assert "token" not in tidal.casefold()


def test_dialog_explains_browser_login_without_code_and_has_explicit_names() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    dialog = source[source.index("class ServiceAccountDialog") :]
    dialog = dialog[:dialog.index("class SessionSelectionDialog")]
    assert 'self.login_button.Show(not self._connected)' in dialog
    assert 'label="Zaloguj w przeglądarce"' in dialog
    assert '"Wyloguj ze Spotify w AMC"' in dialog
    assert '"Wyloguj z katalogu TIDAL w AMC"' in dialog
    assert '"Kopiuj identyfikator aplikacji"' in dialog
    assert 'self.disconnect_button.Show(self._connected)' in dialog
    assert 'self.disconnect_button.SetFocus()' in dialog
    assert 'wx.NO_DEFAULT' in dialog
    assert "Po zalogowaniu AMC pokaże " in dialog
    assert "osobne zatwierdzenie kodu odtwarzacza " in dialog
    assert '"Logowanie do katalogu nie wymaga przepisywania kodu."' in dialog
    assert 'SetName(f"Stan konta {service_name}: {status}")' in dialog
    assert 'f"Zaloguj konto {service_name} w przeglądarce"' in dialog
    assert "self.login_button.SetFocus" in dialog
    assert "self.close_button.Enable(True)" in dialog
    assert "self.SetEscapeId(wx.ID_CANCEL)" in dialog
    assert 'self.SetTitle(i18n.ui_text(f"Konto {service_name}"))' in dialog


def test_connected_account_exposes_logout_and_saved_identifier_only() -> None:
    class Control:
        shown = True
        editable = True
        label = ""
        name = ""

        def Show(self, shown):
            self.shown = shown

        def SetEditable(self, editable):
            self.editable = editable

        def SetLabel(self, label):
            self.label = label

        def SetName(self, name):
            self.name = name

    dialog = SimpleNamespace(
        _connected=False,
        _saved_client_id="0123456789abcdef0123456789abcdef",
        _service_name="Spotify",
        login_button=Control(),
        disconnect_button=Control(),
        client_id=Control(),
        save_button=Control(),
        copy_client_id_button=Control(),
        Layout=lambda: None,
    )
    gui_module.ServiceAccountDialog.set_connected(dialog, True)
    assert not dialog.login_button.shown
    assert dialog.disconnect_button.shown
    assert not dialog.client_id.editable
    assert not dialog.save_button.shown
    assert dialog.copy_client_id_button.shown

    gui_module.ServiceAccountDialog.set_connected(dialog, False)
    assert dialog.login_button.shown
    assert not dialog.disconnect_button.shown
    assert dialog.client_id.editable
    assert dialog.save_button.shown
    assert "Zmień" in dialog.save_button.name


def test_tidal_login_refreshes_visible_collection_and_disconnect_stays_separate() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    section = source[source.index("    def _show_service_account") :]
    section = section[:section.index("    def _show_youtube_account")]
    assert 'dialog.set_connected(True)' in section
    assert 'self._open_tidal_view(OpenTidalView(' in section
    assert 'client.tidal_disconnect' in section
    assert 'dialog.set_connected(False)' in section
    assert '"tidal:playlists"' in section
    assert 'client.spotify_disconnect' in section


def test_configure_routes_spotify_tidal_and_wiim_without_opening_browser_itself() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    section = source[source.index("    def _configure_session") :]
    section = section[:section.index("    def _show_session_list")]
    assert "self._show_service_account(session_id)" in section
    assert "client.spotify_login()" in section
    assert "client.tidal_login()" in section
    assert "self._load_wiim_devices" in section
    assert "LaunchDefaultBrowser" not in section
    assert "webbrowser" not in section.casefold()


def test_account_window_stays_modeless_during_external_login() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    section = source[source.index("    def _show_service_account") :]
    section = section[:section.index("    def _show_youtube_account")]
    assert "dialog.Show()" in section
    assert "dialog.ShowModal()" not in section
    assert "existing.focus_primary_action()" in section
    assert "i18n.localize_window_tree(dialog)" in section


def test_opening_spotify_account_announces_current_status_without_login(monkeypatch) -> None:
    monkeypatch.setattr(gui_module.wx, "CallAfter", lambda callback, *args: callback(*args))
    spoken: list[str] = []
    shown: list[tuple[str, str]] = []
    configurations: list[dict] = []

    class FakeDialog:
        def __init__(self, _parent, service_name, status, _start_login, **options) -> None:
            self.service_name = service_name
            self.status = status
            configurations.append(options)

        def IsBeingDeleted(self) -> bool:  # noqa: N802 - API wx
            return False

        def Raise(self) -> None:  # noqa: N802 - API wx
            pass

        def Show(self) -> None:  # noqa: N802 - API wx
            shown.append((self.service_name, self.status))

        def focus_primary_action(self) -> None:
            pass

    original_dialog = gui_module.ServiceAccountDialog
    gui_module.ServiceAccountDialog = FakeDialog
    try:
        frame = SimpleNamespace(
            client=SimpleNamespace(
                spotify_account_status=lambda: {
                    "catalog": {
                        "connected": True,
                        "label": "Katalog Spotify jest połączony: Michał.",
                    },
                    "playback": {
                        "connected": True,
                        "label": "Konto odtwarzania Spotify jest zapisane i gotowe.",
                    },
                }
            ),
            runner=SimpleNamespace(submit=lambda *_args: None),
            announcer=SimpleNamespace(say=spoken.append),
            _service_account_status_message=(
                LiteFrame._service_account_status_message
            ),
            _restore_focus_after_dialog=lambda: None,
            _window_alive=lambda: True,
        )

        LiteFrame._show_service_account(frame, SessionId.SPOTIFY)
        LiteFrame._show_service_account(frame, SessionId.SPOTIFY)
    finally:
        gui_module.ServiceAccountDialog = original_dialog

    expected = (
        "Katalog Spotify jest połączony: Michał. "
        "Konto odtwarzania Spotify jest zapisane i gotowe."
    )
    assert shown == [("Spotify", expected), ("Spotify", expected)]
    assert spoken == [expected]
    assert configurations[0]["connected"] is True
    assert callable(configurations[0]["disconnect_account"])



def test_spotify_panel_opens_on_button_click_and_reports_failure(monkeypatch) -> None:
    opened: list[str] = []
    focused: list[bool] = []
    messages: list[str] = []
    panel = SimpleNamespace(
        GetValue=lambda: "https://developer.spotify.com/dashboard",
        SetFocus=lambda: focused.append(True),
    )
    dialog = SimpleNamespace(
        _busy=False,
        developer_url=panel,
        set_status=messages.append,
    )
    monkeypatch.setattr(
        gui_module.wx, "LaunchDefaultBrowser",
        lambda url: opened.append(url) or True,
        raising=False,
    )
    gui_module.ServiceAccountDialog._on_open_developer_panel(dialog, None)
    assert opened == ["https://developer.spotify.com/dashboard"]
    assert messages == []

    monkeypatch.setattr(gui_module.wx, "LaunchDefaultBrowser", lambda _url: False)
    gui_module.ServiceAccountDialog._on_open_developer_panel(dialog, None)
    assert "Skopiuj adres" in messages[-1]
    assert focused == [True]


def test_empty_client_id_does_not_start_browser_login() -> None:
    messages: list[str] = []
    focused: list[bool] = []
    started: list[bool] = []
    dialog = SimpleNamespace(
        _busy=False,
        _needs_setup=True,
        _service_name="Spotify",
        _login_started=False,
        client_id=SimpleNamespace(SetFocus=lambda: focused.append(True)),
        set_status=messages.append,
        _start_login=lambda _dialog: started.append(True),
    )
    gui_module.ServiceAccountDialog._on_login(dialog, None)
    assert messages == ["Najpierw wpisz i zapisz Client ID aplikacji Spotify."]
    assert focused == [True]
    assert started == []
    assert dialog._login_started is False
