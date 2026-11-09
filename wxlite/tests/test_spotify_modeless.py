"""Check the Soloist setup surface without showing windows."""

from pathlib import Path
from types import SimpleNamespace

import amc_wx_lite.gui as gui_module
from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.navigation import SessionId


def test_soloist_setup_reuses_modeless_window_and_returns_focus(monkeypatch, tmp_path: Path):
    events = []
    account_windows = []
    setup_windows = []

    class Button:
        def SetFocus(self):  # noqa: N802
            events.append("account-button-focus")

    class Account:
        def __init__(self, _parent, _name, _status, _login, **options):
            self.options = options
            self.soloist_button = Button()
            self.pairing_active = False
            self._login_started = False
            account_windows.append(self)

        def Show(self):  # noqa: N802
            events.append("account-show")

        def Raise(self):  # noqa: N802
            events.append("account-raise")

        def IsBeingDeleted(self):  # noqa: N802
            return False

        def focus_primary_action(self):
            events.append("account-primary-focus")

    class Setup:
        def __init__(self, parent, _manager, _announce, *, on_closed):
            assert parent is frame
            self.on_closed = on_closed
            setup_windows.append(self)

        def Show(self):  # noqa: N802
            events.append("setup-show")

        def Raise(self):  # noqa: N802
            events.append("setup-raise")

        def IsBeingDeleted(self):  # noqa: N802
            return False

        def focus_primary_action(self):
            events.append("setup-focus")

    frame = SimpleNamespace(
        client=SimpleNamespace(spotify_account_status=lambda: {
            "catalog": {"configured": False, "label": "Brak konfiguracji."},
            "playback": {"label": "Brak połączenia."},
        }),
        announcer=SimpleNamespace(say=lambda _message: None),
        store=SimpleNamespace(path=tmp_path / "state.json"),
        _service_account_status_message=LiteFrame._service_account_status_message,
        _restore_focus_after_dialog=lambda: events.append("main-focus"),
        _window_alive=lambda: True,
    )
    monkeypatch.setattr(gui_module, "ServiceAccountDialog", Account)
    monkeypatch.setattr(gui_module, "SpotifySoloistDialog", Setup)
    monkeypatch.setattr(gui_module, "SoloistManager", lambda *_args, **_kwargs: object())
    monkeypatch.setattr(gui_module.i18n, "localize_window_tree", lambda _window: None)
    monkeypatch.setattr(gui_module.wx, "CallAfter", lambda callback, *args: callback(*args))

    LiteFrame._show_service_account(frame, SessionId.SPOTIFY)
    first = account_windows[0]
    first.options["open_spotify_soloist"](first)
    first.options["open_spotify_soloist"](first)
    assert len(setup_windows) == 1
    assert events.count("setup-focus") == 1

    first.options["on_closed"](first)
    LiteFrame._show_service_account(frame, SessionId.SPOTIFY)
    second = account_windows[1]
    second.options["open_spotify_soloist"](second)
    assert len(setup_windows) == 1
    setup_windows[0].on_closed()
    assert frame._spotify_soloist_dialog is None
    assert events[-1] == "account-button-focus"
