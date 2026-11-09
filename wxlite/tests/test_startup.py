"""Startup must schedule I/O rather than wait inside the GUI constructor."""
import importlib
import os
import sys
from pathlib import Path
from types import SimpleNamespace
sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_gui_logic import install_wx_stub
install_wx_stub()
import amc_wx_lite.gui as gui
from amc_wx_lite.state_store import LiteState, Options
from amc_wx_lite.host_client import LiteHostClient, HostUnavailable


def test_public_profile_defines_shared_collection_scopes_without_tidal_adapter():
    """Wspolne menu nie moze zalezec od prywatnego modulu TIDAL."""
    previous_profile = os.environ.get("AMC_RELEASE_PROFILE")
    names = (
        "TIDAL_VIEW_LIBRARY",
        "TIDAL_VIEW_FAVORITES",
        "TIDAL_VIEW_PLAYLISTS",
    )
    try:
        os.environ["AMC_RELEASE_PROFILE"] = "public"
        for name in names:
            gui.__dict__.pop(name, None)
        importlib.reload(gui)

        assert gui.TIDAL_VIEW_LIBRARY == "library"
        assert gui.TIDAL_VIEW_FAVORITES == "favorites"
        assert gui.TIDAL_VIEW_PLAYLISTS == "playlists"
    finally:
        if previous_profile is None:
            os.environ.pop("AMC_RELEASE_PROFILE", None)
        else:
            os.environ["AMC_RELEASE_PROFILE"] = previous_profile
        importlib.reload(gui)


def test_wx_single_instance_names_are_separate_for_public_and_test_profiles():
    previous_profile = os.environ.get("AMC_RELEASE_PROFILE")
    old_user = getattr(gui.wx, "GetUserId", None)
    try:
        gui.wx.GetUserId = lambda: "test-user"
        os.environ["AMC_RELEASE_PROFILE"] = "test"
        test_name = gui._instance_checker_name()
        os.environ["AMC_RELEASE_PROFILE"] = "public"
        public_name = gui._instance_checker_name()
    finally:
        if previous_profile is None:
            os.environ.pop("AMC_RELEASE_PROFILE", None)
        else:
            os.environ["AMC_RELEASE_PROFILE"] = previous_profile
        if old_user is None:
            delattr(gui.wx, "GetUserId")
        else:
            gui.wx.GetUserId = old_user

    assert test_name != public_name
    assert "org.accessible-media-controller.test" in test_name
    assert "org.accessible-media-controller.public" in public_name
    assert test_name.endswith("-test-user")
    assert public_name.endswith("-test-user")


def test_second_instance_is_kept_when_safe_close_cannot_be_confirmed():
    calls = []

    class Checker:
        def __init__(self, name):
            calls.append(("checker", name))

        def IsAnotherRunning(self):  # noqa: N802 - wx API
            return True

    app = SimpleNamespace(
        SetAppName=lambda name: calls.append(("app-name", name)),
        Bind=lambda *_args: None,
        _on_activate_app=lambda _event: None,
    )
    old_checker = getattr(gui.wx, "SingleInstanceChecker", None)
    old_user = getattr(gui.wx, "GetUserId", None)
    old_icon = gui.wx.ICON_INFORMATION
    old_message = gui.wx.MessageBox
    old_store = gui.StateStore
    old_request = gui._request_previous_instance_close
    old_activate = gui._activate_window_handle
    old_window = gui._windows_main_window
    try:
        gui.wx.SingleInstanceChecker = Checker
        gui.wx.GetUserId = lambda: "test-user"
        gui.wx.ICON_INFORMATION = 0x0800
        gui.wx.MessageBox = lambda message, title, flags: calls.append(
            ("message", message, title, flags)
        )
        gui._request_previous_instance_close = lambda: False
        gui._activate_window_handle = lambda handle: calls.append(
            ("activate", handle)
        ) or True
        gui._windows_main_window = lambda: 123
        gui.StateStore = lambda: (_ for _ in ()).throw(
            AssertionError("Stan ani host nie mogą być otwierane w drugim oknie")
        )

        assert gui.LiteApp.OnInit(app) is False
        assert app._instance_checker is not None
        assert any(call[0] == "checker" for call in calls)
        message = next(call for call in calls if call[0] == "message")
        assert "pozostała uruchomiona" in message[1]
        assert "nagrywanie" in message[1]
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
        gui._request_previous_instance_close = old_request
        gui._activate_window_handle = old_activate
        gui._windows_main_window = old_window


def test_main_window_starts_maximized_visible_and_in_foreground():
    calls = []

    frame = SimpleNamespace(
        Maximize=lambda value: calls.append(("maximize", value)),
        Show=lambda value: calls.append(("show", value)),
        Raise=lambda: calls.append(("raise",)),
        SetFocus=lambda: calls.append(("focus",)),
        GetHandle=lambda: 456,
    )
    old_activate = gui._activate_window_handle
    try:
        gui._activate_window_handle = lambda handle: calls.append(
            ("foreground", handle)
        ) or True
        gui._present_main_window(frame)
    finally:
        gui._activate_window_handle = old_activate

    assert calls == [
        ("maximize", True),
        ("show", True),
        ("raise",),
        ("focus",),
        ("foreground", 456),
    ]


def test_safe_previous_instance_is_replaced_before_new_window_opens():
    calls = []
    checker_results = iter((True, False))

    class Checker:
        def __init__(self, name):
            self.running = next(checker_results)
            calls.append(("checker", name, self.running))

        def IsAnotherRunning(self):  # noqa: N802 - wx API
            return self.running

    frame = SimpleNamespace()
    app = SimpleNamespace(
        SetAppName=lambda name: calls.append(("app-name", name)),
        SetTopWindow=lambda value: calls.append(("top", value)),
        Bind=lambda *_args: None,
        _on_activate_app=lambda _event: None,
    )
    originals = (
        getattr(gui.wx, "SingleInstanceChecker", None),
        getattr(gui.wx, "GetUserId", None),
        gui.wx.CallAfter,
        gui.StateStore,
        gui.LiteFrame,
        gui._request_previous_instance_close,
        gui._wait_for_previous_instance,
        gui._present_main_window,
    )
    try:
        gui.wx.SingleInstanceChecker = Checker
        gui.wx.GetUserId = lambda: "test-user"
        gui.wx.CallAfter = lambda function, *args: function(*args)
        gui.StateStore = lambda: SimpleNamespace(load=lambda: "state")
        gui.LiteFrame = lambda store, state: calls.append(
            ("frame", store, state)
        ) or frame
        gui._request_previous_instance_close = lambda: calls.append(
            ("close-request",)
        ) or True
        gui._wait_for_previous_instance = lambda checker: calls.append(
            ("wait", checker.running)
        ) or True
        gui._present_main_window = lambda value: calls.append(
            ("present", value)
        )

        assert gui.LiteApp.OnInit(app) is True
    finally:
        (
            old_checker,
            old_user,
            gui.wx.CallAfter,
            gui.StateStore,
            gui.LiteFrame,
            gui._request_previous_instance_close,
            gui._wait_for_previous_instance,
            gui._present_main_window,
        ) = originals
        if old_checker is None:
            delattr(gui.wx, "SingleInstanceChecker")
        else:
            gui.wx.SingleInstanceChecker = old_checker
        if old_user is None:
            delattr(gui.wx, "GetUserId")
        else:
            gui.wx.GetUserId = old_user

    assert ("close-request",) in calls
    assert len([entry for entry in calls if entry[0] == "checker"]) == 2
    assert ("top", frame) in calls
    assert calls.count(("present", frame)) == 2


def test_close_is_refused_while_recording_is_active():
    calls = []
    frame = SimpleNamespace(
        client=SimpleNamespace(
            alive=True,
            radio_recording_status=lambda: {"count": 1},
        ),
        _last_recording_status={},
        announcer=SimpleNamespace(say=lambda text: calls.append(("say", text))),
    )
    event = SimpleNamespace(
        CanVeto=lambda: True,
        Veto=lambda: calls.append(("veto",)),
    )
    old_present = gui._present_main_window
    try:
        gui._present_main_window = lambda _frame: calls.append(("present",))
        gui.LiteFrame._on_close(frame, event)
    finally:
        gui._present_main_window = old_present

    assert ("veto",) in calls
    assert ("present",) in calls
    assert any("trwa nagrywanie" in entry[1] for entry in calls if entry[0] == "say")


def test_close_is_refused_when_recording_state_cannot_be_confirmed():
    calls = []

    def unavailable():
        raise RuntimeError("host nie odpowiada")

    frame = SimpleNamespace(
        client=SimpleNamespace(alive=True, radio_recording_status=unavailable),
        _last_recording_status={},
        announcer=SimpleNamespace(say=lambda text: calls.append(("say", text))),
    )
    event = SimpleNamespace(
        CanVeto=lambda: True,
        Veto=lambda: calls.append(("veto",)),
    )
    old_present = gui._present_main_window
    try:
        gui._present_main_window = lambda _frame: calls.append(("present",))
        gui.LiteFrame._on_close(frame, event)
    finally:
        gui._present_main_window = old_present

    assert ("veto",) in calls
    assert any("nie można potwierdzić" in entry[1] for entry in calls if entry[0] == "say")


def test_engine_handshake_is_deferred_to_background(tmp_path):
    queued = []
    calls = []
    client_arguments = []
    class Client:
        def __init__(self, *args, **kwargs):
            client_arguments.append((args, kwargs))
        def start(self):
            calls.append("start")
        def hello(self):
            calls.append("hello")
            return {"protocol": 1}
        def configure_audio(self, **options):
            calls.append(options)
            return options
        def configure_local_resume(self, remember, **_modes):
            calls.append(("local-resume", remember))
            return {"remember": remember}
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
    profile_paths = SimpleNamespace(
        library_db=Path("missing-profile/library.db"),
        podcasts_db=Path("missing-profile/podcasts.db"),
        state_json=Path("missing-profile/state.json"),
        may_write_profile=False,
    )
    frame = SimpleNamespace(options=_options, state=LiteState(options=_options),
        store=SimpleNamespace(directory=tmp_path),
        layout=profile_paths,
        client=None,
        runner=SimpleNamespace(submit=lambda *args: queued.append(args)),
        timer=SimpleNamespace(Start=lambda n: calls.append("timer")),
        announcer=SimpleNamespace(say=lambda text: calls.append(text)),
        _on_engine_event=lambda *args: None,
        # Po udanym uscisku dloni ramka podpina kolacje hosta -- atrapa musi
        # miec Biblioteke, inaczej test mierzy brak pola, a nie odroczenie I/O.
        library=SimpleNamespace(
            use_collation=lambda collation: calls.append("collation")),
        _load_initial_content=lambda: calls.append("content"),
        _schedule_automatic_application_update_check=(
            lambda: calls.append("application-updates")
        ),
        _update_media_components=lambda **_kwargs: calls.append("components"),
        _remember_local_playback_positions=True)
    # Tryb trwalosci kolejki wyprowadza sie z UKLADU PROFILU; ten test mierzy
    # odroczenie I/O, a nie wybor trybu, wiec deklarujemy zwykly start bez
    # zapisu (tak jak domyslny, wspolny profil tylko do odczytu).
    frame._queue_persistence_arguments = lambda: {}
    frame._initialize_tidal_native_adapter = lambda _client: None
    try:
        gui.LiteFrame._start_engine(frame)
        assert not calls, "The GUI thread must not perform startup I/O"
        assert client_arguments[0][1]["library_db"] == str(profile_paths.library_db)
        assert client_arguments[0][1]["podcasts_db"] == str(profile_paths.podcasts_db)
        assert client_arguments[0][1]["state_json"] == str(profile_paths.state_json)
        assert len(queued) == 1
        _, work, done, failed = queued[0]
        result = work()
        done(result)
        assert calls[:2] == ["start", "hello"]
        assert calls[2]["tempoAlgorithm"] == 1
        assert ("local-resume", True) in calls
        assert "timer" in calls and "content" in calls
        # Kolejnosc listy ma byc podpieta ZANIM wczytamy tresc.
        assert calls.index("collation") < calls.index("content")
        assert calls.index("content") < calls.index("application-updates")
    finally:
        gui.LiteHostClient = original


def test_optional_profile_sync_failure_does_not_disable_the_audio_engine(tmp_path):
    """Stare AMC może legalnie odmówić współbieżnego zapisu Podcastów.

    Taka odmowa chroni bazę, ale Radio i pozostałe odtwarzanie mają nadal
    dostać żywego hosta oraz wczytać początkową treść.
    """

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
            calls.append("audio")
            return options

        def configure_local_resume(self, remember, **_modes):
            calls.append("local-resume")
            return {"remember": remember}

        def configure_podcast_global_settings(self, **options):
            calls.append("podcast-settings")
            raise gui.HostError(
                "Zamknij najpierw główne AMC. Równoczesna zmiana ustawień "
                "Podcastów z dwóch wersji mogłaby utracić dane."
            )

        def close(self):
            calls.append("close")

        def call(self, op, args=None, timeout=None):
            calls.append(("call", op))
            return {"keys": []}

    options = Options(
        podcast_downloads_folder="C:\\Podcasty",
        podcast_rss_refresh_interval_minutes=60,
        podcast_youtube_refresh_interval_minutes=120,
        podcast_automatic_refresh_batch_size=150,
    )
    frame = SimpleNamespace(
        options=options,
        state=LiteState(options=options),
        store=gui.StateStore(tmp_path),
        client=None,
        runner=SimpleNamespace(submit=lambda *args: queued.append(args)),
        timer=SimpleNamespace(Start=lambda n: calls.append("timer")),
        announcer=SimpleNamespace(say=lambda text: calls.append(("say", text))),
        _on_engine_event=lambda *args: None,
        library=SimpleNamespace(
            use_collation=lambda collation: calls.append("collation")
        ),
        _load_initial_content=lambda: calls.append("content"),
        _schedule_automatic_application_update_check=(
            lambda: calls.append("application-updates")
        ),
        _update_media_components=lambda **_kwargs: calls.append("components"),
        _remember_local_playback_positions=True,
    )
    frame._queue_persistence_arguments = lambda: {}
    frame._initialize_tidal_native_adapter = lambda _client: None

    original = gui.LiteHostClient
    gui.LiteHostClient = Client
    try:
        gui.LiteFrame._start_engine(frame)
        _, work, done, _failed = queued[0]
        result = work()
        done(result)
    finally:
        gui.LiteHostClient = original

    assert "podcast-settings" in calls
    assert "close" not in calls
    assert "timer" in calls
    assert "content" in calls
    log = (tmp_path / "logs" / "frontend.log").read_text(encoding="utf-8")
    assert "Pominięto synchronizację ustawień Podcastów" in log
    assert "Zamknij najpierw główne AMC" in log


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
