"""Test event handling intent; actual reader speech requires Windows/NVDA."""
import sys
from pathlib import Path
from types import SimpleNamespace
from typing import cast
sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub
install_wx_stub()
from amc_wx_lite.gui import LiteFrame


def test_fallback_is_not_silent_and_does_not_read_loader_trace():
    messages = []
    frame = cast(LiteFrame, SimpleNamespace(_window_alive=lambda: True,
                            announcer=SimpleNamespace(say=messages.append)))
    LiteFrame._handle_engine_event(frame, "playback.started", {
        "tempoFallbackReason": "Loader trace: private path and technical details"
    })
    assert messages == ["Wybrany algorytm tempa jest niedostępny. Używany SoundTouch."]


def test_successful_algorithm_does_not_add_a_message():
    messages = []
    frame = cast(LiteFrame, SimpleNamespace(_window_alive=lambda: True,
                            announcer=SimpleNamespace(say=messages.append)))
    LiteFrame._handle_engine_event(frame, "playback.started", {"tempoFallbackReason": None})
    assert messages == []


def test_radio_title_uses_the_host_event_field():
    messages, labels = [], []
    frame = cast(LiteFrame, SimpleNamespace(_window_alive=lambda: True,
        announcer=SimpleNamespace(say=messages.append),
        now_playing=SimpleNamespace(SetLabel=labels.append)))
    LiteFrame._handle_engine_event(frame, "radio.nowPlaying", {
        "streamTitle": "Wykonawca — Utwór", "station": "Stacja"
    })
    assert labels == ["Wykonawca — Utwór"]
    assert messages == labels
