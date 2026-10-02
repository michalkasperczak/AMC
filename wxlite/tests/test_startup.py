"""Startup must schedule I/O rather than wait inside the GUI constructor."""
import sys
from pathlib import Path
from types import SimpleNamespace
sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub
install_wx_stub()
import amc_wx_lite.gui as gui
from amc_wx_lite.state_store import Options
from amc_wx_lite.host_client import LiteHostClient, HostUnavailable


def test_engine_handshake_is_deferred_to_background():
    queued = []
    calls = []
    class Client:
        def __init__(self, *args, **kwargs):
            pass
        def start(self):
            calls.append("start")
        def hello(self):
            calls.append("hello")
            return {"protocol": 1}
        def configure_audio(self, **options):
            calls.append(options)
            return options
        def close(self):
            calls.append("close")
    original = gui.LiteHostClient
    gui.LiteHostClient = Client
    frame = SimpleNamespace(options=Options(), client=None,
        runner=SimpleNamespace(submit=lambda *args: queued.append(args)),
        timer=SimpleNamespace(Start=lambda n: calls.append("timer")),
        announcer=SimpleNamespace(say=lambda text: calls.append(text)),
        _on_engine_event=lambda *args: None,
        _load_initial_content=lambda: calls.append("content"))
    try:
        gui.LiteFrame._start_engine(frame)
        assert not calls, "The GUI thread must not perform startup I/O"
        assert len(queued) == 1
        _, work, done, failed = queued[0]
        result = work()
        done(result)
        assert calls[:2] == ["start", "hello"]
        assert calls[2]["tempoAlgorithm"] == 1
        assert "timer" in calls and "content" in calls
    finally:
        gui.LiteHostClient = original


def test_closed_client_cannot_spawn_a_late_orphan():
    spawned = []
    client = LiteHostClient(__file__, spawn=lambda *args, **kwargs: spawned.append(True))
    client.close()
    try:
        client.start()
    except HostUnavailable:
        pass
    else:
        raise AssertionError("A closed client must refuse late startup")
    assert not spawned
