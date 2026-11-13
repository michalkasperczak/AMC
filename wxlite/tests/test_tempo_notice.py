"""Test event handling intent; actual reader speech requires Windows/NVDA."""
import sys
import tempfile
from pathlib import Path
from types import SimpleNamespace
from typing import cast
sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub
install_wx_stub()
from amc_wx_lite.gui import LiteFrame
from amc_wx_lite.navigation import SessionId


HOST_SOURCE = (
    Path(__file__).parents[2]
    / "src"
    / "AccessibleMediaController.LiteHost"
    / "LiteEngineHandlers.cs"
).read_text(encoding="utf-8-sig")


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


def test_history_write_failure_never_speaks_technical_database_details():
    messages = []
    with tempfile.TemporaryDirectory() as directory:
        frame = cast(LiteFrame, SimpleNamespace(
            _window_alive=lambda: True,
            announcer=SimpleNamespace(say=messages.append),
            store=SimpleNamespace(directory=Path(directory)),
            _playing_session=None,
            _playing_sessions=set(),
            _schedule_radio_recognition_after_source_change=lambda: None,
        ))

        LiteFrame._handle_engine_event(frame, "playback.started", {
            "engine": "radio",
            "id": "techniczne-id",
            "title": "Stacja",
            "historyError": "SQLite: prywatna ścieżka i szczegóły bazy",
        })

        assert messages == [
            "Odtwarzanie działa, ale nie udało się zapisać pozycji "
            "w Historii odtwarzania"
        ]
        assert "SQLite" not in messages[0]
        assert SessionId.RADIO in frame._playing_sessions
        log = (Path(directory) / "logs" / "frontend.log").read_text(
            encoding="utf-8"
        )
        assert "SQLite" in log


def test_history_is_recorded_from_confirmed_engine_start_for_all_local_engines():
    files_started = HOST_SOURCE[
        HOST_SOURCE.index("_files.PlaybackStarted") :
        HOST_SOURCE.index("_files.PlaybackEnded")
    ]
    radio_started = HOST_SOURCE[
        HOST_SOURCE.index("_radio.PlaybackStarted") :
        HOST_SOURCE.index("_radio.NowPlayingChanged")
    ]

    assert "TryRecordPlaybackHistory" in files_started
    # Bieżący host zapamiętuje sesję silnika przy potwierdzonym starcie, także
    # gdy następny element uruchomiła kolejka, a dopiero potem mapuje ``files``
    # na historyczne ``local``. Stary test oczekiwał usuniętego skrótu, który
    # pomijał ten stan kolejki.
    assert "_filesSessionId = e.Item.Kind == MediaItemKind.Episode" in files_started
    assert 'sessionId == "podcasts" ? "podcasts" : "local"' in files_started
    assert 'TryRecordPlaybackHistory("radio", e.Item.Id)' in radio_started
    assert "historyError" in files_started and "historyError" in radio_started


def test_radio_title_uses_the_host_event_field():
    messages, labels = [], []
    refreshes = []
    frame = cast(LiteFrame, SimpleNamespace(_window_alive=lambda: True,
        announcer=SimpleNamespace(say=messages.append),
        now_playing=SimpleNamespace(SetLabel=labels.append),
        _refresh_status=lambda: refreshes.append(True)))
    LiteFrame._handle_engine_event(frame, "radio.nowPlaying", {
        "streamTitle": "Wykonawca — Utwór", "station": "Stacja"
    })
    assert labels == ["Wykonawca — Utwór"]
    assert messages == labels
    assert refreshes == [True], "pełny pasek NVDA+End ma zostać odświeżony"
