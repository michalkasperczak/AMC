"""Startup must schedule I/O rather than wait inside the GUI constructor."""
import sys
from pathlib import Path
from types import SimpleNamespace
sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub
install_wx_stub()
import amc_wx_lite.gui as gui
from amc_wx_lite.state_store import LiteState, Options
from amc_wx_lite.host_client import LiteHostClient, HostUnavailable


def test_second_instance_is_refused_before_opening_an_engine_less_window():
    calls = []

    class Checker:
        def __init__(self, name):
            calls.append(("checker", name))

        def IsAnotherRunning(self):  # noqa: N802 - wx API
            return True

    app = SimpleNamespace(
        SetAppName=lambda name: calls.append(("app-name", name)),
    )
    old_checker = getattr(gui.wx, "SingleInstanceChecker", None)
    old_user = getattr(gui.wx, "GetUserId", None)
    old_icon = gui.wx.ICON_INFORMATION
    old_message = gui.wx.MessageBox
    old_store = gui.StateStore
    try:
        gui.wx.SingleInstanceChecker = Checker
        gui.wx.GetUserId = lambda: "test-user"
        gui.wx.ICON_INFORMATION = 0x0800
        gui.wx.MessageBox = lambda message, title, flags: calls.append(
            ("message", message, title, flags)
        )
        gui.StateStore = lambda: (_ for _ in ()).throw(
            AssertionError("Stan ani host nie mogą być otwierane w drugim oknie")
        )

        assert gui.LiteApp.OnInit(app) is False
        assert app._instance_checker is not None
        assert any(call[0] == "checker" for call in calls)
        message = next(call for call in calls if call[0] == "message")
        assert "już uruchomione" in message[1]
        assert "AMC-wx-Lite" not in message[1]
    finally:
        if old_checker is None:
            delattr(gui.wx, "SingleInstanceChecker")
        else:
            gui.wx.SingleInstanceChecker = old_checker
        if old_user is None:
            delattr(gui.wx, "GetUserId")
        else:
            gui.wx.GetUserId = old_user
        gui.wx.ICON_INFORMATION = old_icon
        gui.wx.MessageBox = old_message
        gui.StateStore = old_store


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
        def call(self, op, args=None, timeout=None):
            # Kolacja rozmawia z hostem ta droga; atrapa musi ja miec,
            # inaczej test wywraca sie na braku metody zamiast mierzyc start.
            calls.append(("call", op))
            return {"keys": []}
    original = gui.LiteHostClient
    gui.LiteHostClient = Client
    _options = Options()
    # ``_start_engine`` liczy payload audio ze stanu (wspolna funkcja
    # ``session_options.engine_audio_payload``), zeby nowy proces dostal
    # ustawienia PO nalozeniu Opcji sesji. Atrapa musi miec ``state``.
    frame = SimpleNamespace(options=_options, state=LiteState(options=_options),
        client=None,
        runner=SimpleNamespace(submit=lambda *args: queued.append(args)),
        timer=SimpleNamespace(Start=lambda n: calls.append("timer")),
        announcer=SimpleNamespace(say=lambda text: calls.append(text)),
        _on_engine_event=lambda *args: None,
        # Po udanym uscisku dloni ramka podpina kolacje hosta -- atrapa musi
        # miec Biblioteke, inaczej test mierzy brak pola, a nie odroczenie I/O.
        library=SimpleNamespace(
            use_collation=lambda collation: calls.append("collation")),
        _load_initial_content=lambda: calls.append("content"))
    # Tryb trwalosci kolejki wyprowadza sie z UKLADU PROFILU; ten test mierzy
    # odroczenie I/O, a nie wybor trybu, wiec deklarujemy zwykly start bez
    # zapisu (tak jak domyslny, wspolny profil tylko do odczytu).
    frame._queue_persistence_arguments = lambda: {}
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
        # Kolejnosc listy ma byc podpieta ZANIM wczytamy tresc.
        assert calls.index("collation") < calls.index("content")
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
