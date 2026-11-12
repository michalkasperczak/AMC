from __future__ import annotations

from pathlib import Path
from types import SimpleNamespace

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite.gui import LiteFrame, SessionSelectionDialog
from amc_wx_lite.navigation import SessionId


SOURCE = Path(__file__).parents[1] / "amc_wx_lite" / "gui.py"


class _Announcer:
    def __init__(self) -> None:
        self.messages: list[str] = []

    def say(self, message: str) -> None:
        self.messages.append(message)


class _Control:
    def __init__(self) -> None:
        self.enabled = None
        self.label = ""
        self.name = ""
        self.focused = False

    def Enable(self, enabled: bool) -> None:  # noqa: N802 - API wx
        self.enabled = bool(enabled)

    def SetLabel(self, label: str) -> None:  # noqa: N802 - API wx
        self.label = label

    def SetName(self, name: str) -> None:  # noqa: N802 - API wx
        self.name = name

    def SetFocus(self) -> None:  # noqa: N802 - API wx
        self.focused = True


def _frame(order: list[str], current: SessionId) -> SimpleNamespace:
    frame = SimpleNamespace(
        options=SimpleNamespace(session_order=list(order)),
        navigator=SimpleNamespace(active=current),
        announcer=_Announcer(),
        _playing_sessions=set(),
        _pending_playback_session=None,
        _last_recording_status={},
        _transient_preview_return="unchanged",
        save_snapshots=[],
        menu_rebuilds=0,
        switches=[],
    )
    frame._sessions_blocked_from_disabling = lambda: (
        LiteFrame._sessions_blocked_from_disabling(frame)
    )
    frame._save_state = lambda: frame.save_snapshots.append((
        list(frame.options.session_order), frame.navigator.active
    )) or True
    frame._build_menu = lambda: setattr(
        frame, "menu_rebuilds", frame.menu_rebuilds + 1
    )

    def switch(session: SessionId) -> None:
        frame.switches.append(session)
        frame.navigator.active = session

    frame._switch_session = switch
    return frame


def test_disabling_external_sonos_or_wiim_never_sends_a_device_command() -> None:
    for session in (SessionId.SONOS, SessionId.WIIM):
        frame = _frame(["files", session.value], SessionId.FILES)

        changed, message = LiteFrame._toggle_session_enabled(frame, session)

        assert tuple(item.value for item in changed) == ("files",)
        expected_name = "Sonos" if session is SessionId.SONOS else "WiiM"
        assert message == f"Wyłączono sesję {expected_name}"
        assert frame.switches == []
        assert frame.save_snapshots == [(["files"], SessionId.FILES)]
        assert frame.menu_rebuilds == 1
        assert not hasattr(frame, "client")


def test_disabling_current_session_saves_valid_fallback_before_switching() -> None:
    frame = _frame(["files", "radio", "podcasts"], SessionId.RADIO)

    changed, message = LiteFrame._toggle_session_enabled(frame, SessionId.RADIO)

    assert tuple(item.value for item in changed) == ("files", "podcasts")
    assert frame.save_snapshots == [(["files", "podcasts"], SessionId.PODCASTS)]
    assert frame.switches == [SessionId.PODCASTS]
    assert frame.navigator.active is SessionId.PODCASTS
    assert "Aktywna sesja: Podcasty i YouTube, Ctrl+2" in message


def test_enabling_apple_music_as_tenth_session_is_saved_with_ctrl_zero() -> None:
    first_nine = [
        session.value
        for session in SessionId
        if session is not SessionId.APPLE_MUSIC
    ]
    frame = _frame(first_nine, SessionId.FILES)

    changed, message = LiteFrame._toggle_session_enabled(frame, SessionId.APPLE_MUSIC)

    assert len(changed) == 10
    assert changed[-1] is SessionId.APPLE_MUSIC
    assert message == "Włączono sesję Apple Music, Ctrl+0"
    assert frame.options.session_order[-1] == "appleMusic"
    assert frame.save_snapshots[-1][0][-1] == "appleMusic"
    assert frame.menu_rebuilds == 1


def test_internal_playback_and_recording_block_only_the_owned_sessions() -> None:
    frame = _frame(["files", "radio", "sonos", "wiim"], SessionId.FILES)
    frame._playing_sessions = {SessionId.FILES, SessionId.SONOS, SessionId.WIIM}
    frame._pending_playback_session = SessionId.PODCASTS
    frame._last_recording_status = {"count": 1, "recordings": []}

    blocked = LiteFrame._sessions_blocked_from_disabling(frame)

    assert blocked == {SessionId.FILES, SessionId.PODCASTS, SessionId.RADIO}
    assert SessionId.SONOS not in blocked
    assert SessionId.WIIM not in blocked


def test_direct_switch_to_disabled_session_is_refused_with_user_facing_help() -> None:
    frame = _frame(["files", "radio", "podcasts"], SessionId.FILES)

    LiteFrame._switch_session(frame, SessionId.SPOTIFY)

    assert frame.announcer.messages == [
        "Sesja Spotify jest wyłączona. Otwórz Menedżer sesji skrótem Ctrl+Shift+S"
    ]
    assert frame.switches == []


def test_manager_dialog_has_explicit_names_keys_and_focus_restoration_contract() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    dialog = source[source.index("class SessionSelectionDialog") :]
    dialog = dialog[:dialog.index("class LiteFrame")]
    assert 'title="Menedżer sesji"' in dialog
    assert 'self.SetName("Menedżer sesji")' in dialog
    assert 'self.session_list.SetName("Sesje")' in dialog
    assert 'SetName("Przełącz do wybranej sesji")' in dialog
    assert 'SetName("Włącz lub wyłącz wybraną sesję")' in dialog
    assert 'SetName("Ustawienia sesji dla wybranej sesji")' in dialog
    assert 'SetName("Osobna konfiguracja wybranej sesji")' in dialog
    assert "self.settings_button.Bind(wx.EVT_BUTTON, self._settings)" in dialog
    assert "session_options.capabilities_for(selected).has_options" in dialog
    assert "self._settings_session(selected, self)" in dialog
    assert "wx.CallAfter(self.session_list.SetFocus)" in dialog
    assert "_CONFIGURATION_LABELS" in dialog
    assert "event.AltDown()" in dialog
    assert "event.GetKeyCode() == wx.WXK_UP" in dialog
    assert "event.GetKeyCode() == wx.WXK_DOWN" in dialog
    assert "self._refresh_rows(selected)" in dialog
    assert "self.session_list.SetFocus()" in dialog
    assert "wx.CallAfter(self._announce, message)" in dialog
    assert "selected is SessionId.FRONTIER_SMART" in dialog
    assert "self.configure_session = selected" in dialog
    assert "self.EndModal(wx.ID_APPLY)" in dialog


def test_manager_separates_session_settings_from_source_configuration() -> None:
    settings_button = _Control()
    configure_button = _Control()
    dialog = SimpleNamespace(
        selected_session=SessionId.FILES,
        _order=(SessionId.FILES, SessionId.RADIO),
        _settings_session=lambda *_args: None,
        _CONFIGURATION_LABELS=SessionSelectionDialog._CONFIGURATION_LABELS,
        settings_button=settings_button,
        configure_button=configure_button,
    )

    SessionSelectionDialog._update_action_buttons(dialog)

    assert settings_button.enabled is True
    assert configure_button.enabled is True
    assert configure_button.label == "&Foldery Biblioteki…"
    assert configure_button.name == "Foldery Biblioteki dla Plików lokalnych"

    dialog.selected_session = SessionId.RADIO
    SessionSelectionDialog._update_action_buttons(dialog)
    assert settings_button.enabled is True
    assert configure_button.enabled is False
    assert configure_button.name == "Brak osobnej konfiguracji dla wybranej sesji"


def test_manager_opens_settings_for_selected_session_and_returns_list_focus() -> None:
    calls: list[tuple[SessionId, object]] = []
    session_list = _Control()
    dialog = SimpleNamespace(
        selected_session=SessionId.PODCASTS,
        _order=(SessionId.PODCASTS,),
        _settings_session=lambda session, parent: calls.append((session, parent)),
        session_list=session_list,
        _say=lambda _message: None,
    )

    SessionSelectionDialog._settings(dialog, None)

    assert calls == [(SessionId.PODCASTS, dialog)]
    assert session_list.focused is True


def test_opening_manager_reads_only_cached_readiness_and_does_not_authenticate() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    readiness = source[source.index("    def _session_readiness") :]
    readiness = readiness[:readiness.index("    def _sessions_blocked_from_disabling")]
    assert "load_view" in readiness
    assert "login" not in readiness.casefold()
    assert "oauth" not in readiness.casefold()
    assert "open_browser" not in readiness.casefold()
