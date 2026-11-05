"""Odpowiedzi wxPython dla gotowego dodatku AMC do NVDA.

To nie jest test żywej mowy. Sprawdza ostatnią warstwę tekstu, którą most
odsyła do dodatku: stan ma być prawdziwy i nie może zawierać nazw modelu.
"""

from __future__ import annotations

from types import SimpleNamespace

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite import gui  # noqa: E402
from amc_wx_lite.navigation import Navigator, SessionId, View  # noqa: E402


def _frame(session_id: SessionId) -> gui.LiteFrame:
    frame = gui.LiteFrame.__new__(gui.LiteFrame)
    frame.navigator = Navigator()
    frame.navigator.active = session_id
    frame.options = SimpleNamespace(volume=65)
    frame._transport_status_by_session = {}
    frame._last_recording_status = {}
    frame._wiim_snapshot = {}
    frame._sonos_snapshot = {}
    frame._tidal_native_has_playback = False
    frame._tidal_desktop_has_playback = False
    return frame


def test_local_status_uses_only_user_facing_playback_values() -> None:
    frame = _frame(SessionId.PODCASTS)
    frame.navigator.session.now_playing_title = "Rozmowa dnia"
    frame._transport_status_by_session[SessionId.PODCASTS] = {
        "paused": True,
        "muted": True,
        "volume": 35,
    }

    assert frame._nvda_status_message() == (
        "Rozmowa dnia, Podcasty i YouTube, pauza, wyciszone, głośność 35%."
    )


def test_bit_rate_reaches_nvda_now_playing_and_status_for_archive() -> None:
    frame = _frame(SessionId.RADIOARCHIWISTA)
    frame.navigator.session.now_playing_title = "Audycja"
    frame._transport_status_by_session[SessionId.RADIOARCHIWISTA] = {
        "title": "Audycja",
        "currentPlaybackText": "Audycja, około 128 kb/s, 48 kHz",
        "paused": False,
        "volume": 65,
    }
    assert frame._nvda_now_playing_message() == "Audycja, około 128 kb/s, 48 kHz"
    assert frame._nvda_status_message() == (
        "Audycja, około 128 kb/s, 48 kHz, Radio Archiwista, "
        "odtwarzanie, głośność 65%."
    )


def test_wiim_bit_rate_reaches_both_nvda_commands() -> None:
    frame = _frame(SessionId.WIIM)
    frame._wiim_snapshot = {
        "title": "Audycja", "artist": "Radio", "bitrateKbps": 192,
        "playbackState": "odtwarzanie", "volume": 45,
    }
    assert frame._nvda_now_playing_message() == "Audycja, Radio, 192 kb/s"
    assert frame._nvda_status_message() == (
        "Audycja, Radio, 192 kb/s, WiiM, odtwarzanie, głośność 45%."
    )


def test_radio_status_adds_only_the_current_stations_recording_state() -> None:
    frame = _frame(SessionId.RADIO)
    frame.navigator.session.now_playing_id = "radio-poznan"
    frame.navigator.session.now_playing_title = "Radio Poznań"
    frame._transport_status_by_session[SessionId.RADIO] = {
        "id": "radio-poznan",
        "title": "Radio Poznań",
        "paused": False,
        "volume": 40,
    }
    frame._last_recording_status = {
        "recordings": [
            {"stationId": "radio-opole", "state": "recording"},
            {"stationId": "radio-poznan", "state": "paused", "paused": True},
        ]
    }

    assert frame._nvda_status_message() == (
        "Radio Poznań, Radio, odtwarzanie, "
        "nagrywanie wstrzymane, głośność 40%."
    )

    frame._transport_status_by_session[SessionId.RADIO]["id"] = "radio-inne"
    assert "nagrywanie" not in frame._nvda_status_message()


def test_tidal_status_does_not_invent_play_or_pause_state() -> None:
    frame = _frame(SessionId.TIDAL)
    frame.navigator.session.now_playing_title = "Dawny tytuł"
    assert frame._nvda_status_message() == "TIDAL, brak otwartego nagrania."
    assert frame._nvda_now_playing_message() == (
        "TIDAL, brak bieżącego materiału w AMC."
    )

    frame._tidal_desktop_has_playback = True
    frame.navigator.session.now_playing_title = "Aktualny utwór"
    status = frame._nvda_status_message()
    assert status == (
        "Aktualny utwór, TIDAL, sterowanie w oryginalnej aplikacji."
    )
    assert "odtwarzanie" not in status
    assert "pauza" not in status
    assert frame._nvda_now_playing_message() == "Aktualny utwór"


def test_context_uses_explicit_control_name_not_model_identifiers() -> None:
    frame = _frame(SessionId.RADIO)
    frame.radio_list = SimpleNamespace(GetName=lambda: "Ulubione stacje radiowe")
    frame.navigator.session.view = View.LIST

    message = frame._nvda_context_message()
    assert message == "Radio, Ulubione stacje radiowe"
    assert "LibraryView" not in message
    assert "SessionId" not in message


def test_previous_and_next_preset_are_routed_without_opening_a_dialog() -> None:
    frame = _frame(SessionId.RADIO)
    directions = []
    frame._navigate_nvda_preset = directions.append
    original = getattr(gui.wx, "GetTopLevelWindows", None)
    gui.wx.GetTopLevelWindows = lambda: []
    try:
        assert frame._execute_nvda_command("presetNext") == (
            True,
            "Następny preset",
        )
        assert frame._execute_nvda_command("presetPrevious") == (
            True,
            "Poprzedni preset",
        )
    finally:
        if original is None:
            delattr(gui.wx, "GetTopLevelWindows")
        else:
            gui.wx.GetTopLevelWindows = original
    assert directions == [1, -1]
