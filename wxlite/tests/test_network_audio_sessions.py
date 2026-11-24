"""Session isolation, private FLAC relay, and keyboard surfaces; no playback."""
from __future__ import annotations

import http.client
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import threading
from types import SimpleNamespace
from urllib.parse import urlsplit

import pytest

from amc_wx_lite import command_palette, menu_model, i18n
from amc_wx_lite.cast_protocol import CastDevice, CastError
from amc_wx_lite.cast_session import CastSessionRouter, UNHANDLED, cast_session_available
from amc_wx_lite.cast_tidal import TidalCastShare
from amc_wx_lite.host_client import LiteHostClient
from amc_wx_lite.mirror_core import MirrorCastRouter
from amc_wx_lite.mirror_local import MirrorLocalRouter
from amc_wx_lite.mirror_airplay import MirrorAirPlayRouter
from amc_wx_lite.airplay_adapter import AirPlayDevice
from amc_wx_lite.navigation import SessionId
from amc_wx_lite.network_audio import play_session, valid_tidal_request
from amc_wx_lite.shortcuts import Action, Chord, command_presentation, describe, resolve


def tidal_request(port=43117, *, dash=False):
    return {"id": "123", "title": "Utwór", "durationSeconds": 100,
            "proxyUrl": f"http://127.0.0.1:{port}/audio/" + "a" * 32 + (".mpd" if dash else "")}


class Connection:
    def __init__(self, device):
        self.device = device
        self.commands = []
        self.state = "PLAYING"

    def connect(self): pass
    def close(self): pass
    def set_volume(self, *args, **kwargs): pass
    def load(self, url, mime, title, **kwargs):
        self.commands.append(("LOAD", url, mime, kwargs))
        self.state = "PLAYING" if kwargs.get("autoplay", True) else "PAUSED"
    def status(self): return {"playerState": self.state, "currentTime": 17, "media": {"duration": 100}}
    def receiver_status(self): return {"volume": {"level": 0.2, "muted": False}}
    def transport(self, command, **kwargs):
        self.commands.append((command, kwargs))
        self.state = {"PAUSE": "PAUSED", "PLAY": "PLAYING", "STOP": "IDLE"}.get(command, self.state)


class Share:
    url = "http://192.0.2.10/new-receiver-capability"
    mime = "audio/flac"
    def __init__(self, request, receiver): self.closed = False
    def close(self): self.closed = True


def router_fixture(session):
    calls, events = [], []
    device = CastDevice("cast", "Salon", "192.0.2.1")
    status = {"id": "tidal:123" if session == "tidal" else "recording",
              "source": "tidal:track:123" if session == "tidal" else "https://archive.example/hour.mp3",
              "title": "Materiał", "playing": True, "paused": False,
              "volume": 37, "rate": 1.0, "_tidal_request": tidal_request()}
    def native(op, args):
        calls.append((op, args))
        return dict(status) if op == "transport.status" else {}
    router = CastSessionRouter(native, lambda name, data: events.append((name, data)),
        discover=lambda: (device,), connect=Connection, tidal_share=Share)
    return router, calls, events


@pytest.mark.parametrize("player", [False, True])
@pytest.mark.parametrize("language", ["pl", "en"])
@pytest.mark.parametrize("session,context,profile", [
    (SessionId.RADIOARCHIWISTA, "radioarchiwista_session", "test"),
    (SessionId.TIDAL, "tidal_session", "test"),
    (SessionId.TIDAL, "tidal_session", "public"),
])
def test_network_actions_have_keyboard_menu_help_and_palette_parity(monkeypatch, player, language, session, context, profile):
    monkeypatch.setenv("AMC_RELEASE_PROFILE", profile)
    previous = i18n.active_language()
    i18n.configure(language)
    try:
        _assert_network_command_parity(player, session, context)
    finally: i18n.configure(previous)


def _assert_network_command_parity(player, session, context):
    for action, key in ((Action.CAST_SESSION, "C"), (Action.MIRROR_SESSION, "M")):
        presentation = command_presentation(action)
        assert resolve(Chord(key, shift=True), player_view=player, radio_session=False,
                       **{context: True}) is action
        assert (presentation.shortcut, presentation.description) in describe()
        palette = next(e for e in command_palette.entries_for_session(session) if e.action is action)
        menu = next(e for group in menu_model.menus_for_session(session) for e in group.items if e.action is action)
        assert (palette.name, palette.shortcut) == (presentation.name, presentation.shortcut)
        assert (menu.label, menu.shortcut) == (presentation.menu_label, presentation.shortcut)


def test_media_play_owner_is_explicit_and_does_not_default_archive_to_podcasts():
    assert play_session("media.play", {"sessionId": "radioarchiwista"}) == "radioarchiwista"
    assert play_session("media.play", {}) == "podcasts"
    assert play_session("media.play", {"sessionId": "spotify"}) is None
    router, calls, events = router_fixture("radioarchiwista")
    try:
        router.select("radioarchiwista", "cast")
        calls.clear()
        router.call("media.play", {"sessionId": "radioarchiwista", "source": "https://archive.example/new.mp3",
                                   "id": "new", "title": "Nowe nagranie"})
        assert events[-1][1]["sessionId"] == "radioarchiwista"
        assert not any(op == "media.play" for op, _ in calls)
        assert router.call("media.play", {"sessionId": "podcasts", "source": "https://podcast.example/episode.mp3"}) is not UNHANDLED
        assert router.routes["radioarchiwista"].stopped
    finally:
        router.close()


def test_archive_returns_to_same_session_and_remote_position():
    router, calls, _ = router_fixture("radioarchiwista")
    try:
        router.select("radioarchiwista", "cast")
        router.select("radioarchiwista", None)
        args = next(args for op, args in calls if op == "media.play")
        assert args["sessionId"] == "radioarchiwista"
        assert args["positionSeconds"] == 17
    finally:
        router.close()


def test_casting_a_remembered_archive_does_not_stop_another_native_decoder_owner():
    router, calls, _ = router_fixture("radioarchiwista")
    original = router.native
    router.native = lambda op, args: ({**original(op, args), "playing": False,
        "paused": True, "loadedId": "another-podcast"} if op == "transport.status" else original(op, args))
    try:
        router.select("radioarchiwista", "cast")
        assert not any(op in {"transport.stop", "transport.pauseResume"} for op, _ in calls)
        assert router.status("radioarchiwista")["paused"]
    finally: router.close()


def test_tidal_handoff_keeps_proxy_alive_and_return_restores_position_and_volume():
    router, calls, _ = router_fixture("tidal")
    try:
        router.select("tidal", "cast")
        assert not any(op == "transport.stop" for op, _ in calls)
        connection = router.routes["tidal"].connection
        load = connection.commands[0]
        assert load[1] == Share.url and "127.0.0.1" not in load[1]
        assert load[2] == "audio/flac"
        public_status = router.status("tidal")
        assert public_status["source"] == "tidal:track:123"
        assert "proxyUrl" not in str(public_status) and "_tidal_request" not in public_status
        router.select("tidal", None)
        args = next(args for op, args in calls if op == "tidal.nativePlay")
        assert args["id"] == "123" and args["positionSeconds"] == 17
        assert calls[-1] == ("transport.setVolume", {"sessionId": "tidal", "volume": 37})
    finally:
        router.close()


def test_tidal_next_track_uses_new_capability_without_native_replay():
    router, calls, events = router_fixture("tidal")
    try:
        router.select("tidal", "cast")
        request = {**tidal_request(), "id": "456", "proxyUrl": tidal_request()["proxyUrl"].replace("a" * 32, "b" * 32)}
        calls.clear()
        accepted = router.call("tidal.nativePlay", request)
        assert accepted["ok"] is True and accepted["id"] == "tidal:456"
        assert events[-1][1]["id"] == "tidal:456"
        assert events[-1][1]["sessionId"] == "tidal"
        assert not any(op == "tidal.nativePlay" for op, _ in calls)
    finally:
        router.close()


def test_tidal_requires_current_native_request_and_cast_rejects_dash():
    client = LiteHostClient("unused")
    client._tidal_network_request = tidal_request()
    status = {"id": "tidal:123", "playing": True}
    assert client.network_source_available("tidal", status, cast=True)
    assert not client.network_source_available("tidal", {**status, "id": "tidal:456"})
    client._tidal_network_request = tidal_request(dash=True)
    assert client.network_source_available("tidal", status)
    assert not client.network_source_available("tidal", status, cast=True)
    assert not cast_session_available("tidal", status)
    assert cast_session_available("tidal", status, source_available=True)
    assert cast_session_available("tidal", active=True)
    assert not cast_session_available("spotify")


@pytest.mark.parametrize("change", [
    {"proxyUrl": "http://localhost:12/audio/" + "a" * 32},
    {"proxyUrl": "https://example.com/audio/" + "a" * 32},
    {"proxyUrl": tidal_request()["proxyUrl"] + "?secret=x"},
    {"proxyUrl": tidal_request()["proxyUrl"] + ".mpd"},
    {"id": "not-a-track"},
])
def test_tidal_bridge_rejects_arbitrary_sources(change):
    request = {**tidal_request(), **change}
    assert not valid_tidal_request(request, cast=True)
    with pytest.raises(CastError): TidalCastShare(request, "127.0.0.1")


def test_tidal_relay_forwards_ranges_without_exposing_old_capability():
    requests = []
    class Upstream(BaseHTTPRequestHandler):
        def log_message(self, *_args): pass
        def do_HEAD(self): self._respond(False)
        def do_GET(self): self._respond(True)
        def _respond(self, body):
            requests.append((self.command, self.path, dict(self.headers)))
            ranged = self.headers.get("Range") == "bytes=1-3"
            self.send_response(206 if ranged else 200)
            self.send_header("Content-Length", "3" if ranged else "8")
            self.send_header("Accept-Ranges", "bytes")
            self.send_header("Content-Type", "audio/flac")
            if ranged: self.send_header("Content-Range", "bytes 1-3/8")
            self.send_header("X-Private-Capability", "do-not-forward")
            self.end_headers()
            if body: self.wfile.write(b"LAC" if ranged else b"fLaCdata")
    server = ThreadingHTTPServer(("127.0.0.1", 0), Upstream)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    share = TidalCastShare(tidal_request(server.server_port), "127.0.0.1")
    parsed = urlsplit(share.url)
    try:
        assert "a" * 32 not in share.url
        for method, path, headers, expected in [
            ("GET", parsed.path, {"Range": "bytes=1-3"}, 206),
            ("HEAD", parsed.path, {}, 200),
            ("GET", "/", {}, 404),
            ("GET", parsed.path, {"Range": "bytes=1-3,5-6"}, 416),
        ]:
            conn = http.client.HTTPConnection(parsed.hostname, parsed.port, timeout=3)
            try:
                conn.request(method, path, headers=headers)
                response = conn.getresponse()
                assert response.status == expected
                assert response.getheader("X-Private-Capability") is None
                if method == "GET" and expected == 206: assert response.read() == b"LAC"
                else: response.read()
            finally: conn.close()
        assert len(requests) == 3  # Private MIME probe, range GET, then HEAD.
        assert requests[0][1] == "/audio/" + "a" * 32
        assert "Authorization" not in requests[0][2]
    finally:
        share.close()
        share.close()
        server.shutdown()
        server.server_close()
        thread.join(timeout=2)
    assert not share._worker.is_alive()


def test_archive_mirror_does_not_take_over_podcast_route(tmp_path):
    status = {"source": "https://archive.example/hour.mp3", "playing": True,
              "paused": False, "positionSeconds": 5, "title": "Nagranie"}
    device = CastDevice("cast", "Salon", "192.0.2.1")
    mirror = MirrorCastRouter(lambda *_: status, discover=lambda: (device,), connect=Connection)
    try:
        mirror.enable("radioarchiwista", "cast")
        connection = mirror.routes["radioarchiwista"].connection
        before = len(connection.commands)
        mirror.after_native("media.play", {"sessionId": "radioarchiwista", "source": "https://archive.example/new.mp3"}, {})
        assert len(connection.commands) > before
        mirror.after_native("media.play", {"sessionId": "podcasts", "source": "https://podcast.example/new.mp3"}, {})
        assert "radioarchiwista" not in mirror.routes
        assert not any(command[0] == "LOAD" and "podcast.example" in command[1] for command in connection.commands)
    finally: mirror.close()
    ffmpeg = tmp_path / "ffmpeg.exe"
    ffmpeg.write_bytes(b"not-executed")
    async def discover(): return (AirPlayDevice("air", "Salon"),)
    airplay = MirrorAirPlayRouter(lambda *_: status, str(ffmpeg), discover=discover)
    starts = []
    airplay._start = lambda session, route, snapshot: starts.append(session)
    try:
        airplay.enable("radioarchiwista", "air")
        airplay.after_native("media.play", {"sessionId": "radioarchiwista"}, {})
        assert starts == ["radioarchiwista", "radioarchiwista"]
        airplay.after_native("media.play", {"sessionId": "podcasts"}, {})
        assert "radioarchiwista" not in airplay.routes
        assert starts == ["radioarchiwista", "radioarchiwista"]
    finally: airplay.close()


def test_receiver_scope_rejects_another_loopback_peer_before_contacting_upstream(monkeypatch):
    class Response:
        status = 200
        def getheader(self, name): return "audio/flac"
    class Probe:
        def __init__(self, *_args, **_kwargs): pass
        def request(self, *args, **kwargs): pass
        def getresponse(self): return Response()
        def close(self): pass
    monkeypatch.setattr("amc_wx_lite.cast_tidal.http.client.HTTPConnection", Probe)
    share = TidalCastShare(tidal_request(port=9), "127.0.0.2")
    parsed = urlsplit(share.url)
    monkeypatch.undo()
    conn = http.client.HTTPConnection(parsed.hostname, parsed.port, timeout=3)
    try:
        conn.request("GET", parsed.path)
        response = conn.getresponse()
        assert response.status == 404
        response.read()
    finally:
        conn.close()
        share.close()


def test_airplay_reopening_uses_cache_and_confirmed_selection_checks_again(tmp_path):
    ffmpeg = tmp_path / "ffmpeg.exe"
    ffmpeg.write_bytes(b"not-executed")
    discoveries = []
    async def discover():
        discoveries.append(1)
        return (AirPlayDevice("air", "Salon"),)
    status = {"source": "https://archive.example/hour.mp3", "paused": True}
    airplay = MirrorAirPlayRouter(lambda *_: status, str(ffmpeg), discover=discover)
    try:
        assert str(airplay.devices()[0]) == "Salon"
        assert airplay.devices()[0].device_id == "air"
        assert len(discoveries) == 1
        airplay.enable("radioarchiwista", "air")
        assert len(discoveries) == 2
        airplay.devices(refresh=True)
        assert len(discoveries) == 3
    finally: airplay.close()


def test_gui_tidal_availability_distinguishes_native_flac_dash_and_external_playback():
    from amc_wx_lite.gui import LiteFrame
    client = LiteHostClient("unused")
    client._tidal_network_request = tidal_request()
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.TIDAL), client=client,
        _in_download_history=lambda: False, _tidal_native_has_playback=True,
        _transport_status_by_session={SessionId.TIDAL: {"id": "tidal:123", "playing": True}},
    )
    available = lambda action: LiteFrame._action_available_in_active_session(frame, action)
    assert available(Action.CAST_SESSION) and available(Action.MIRROR_SESSION)
    client._tidal_network_request = tidal_request(dash=True)
    assert not available(Action.CAST_SESSION) and available(Action.MIRROR_SESSION)
    frame._tidal_native_has_playback = False
    assert not available(Action.CAST_SESSION) and not available(Action.MIRROR_SESSION)
    frame._tidal_native_has_playback = True
    frame._tidal_desktop_has_playback = True
    assert not available(Action.CAST_SESSION) and not available(Action.MIRROR_SESSION)
    client._airplay_mirror = SimpleNamespace(selected_device_id=lambda _: "active")
    assert available(Action.MIRROR_SESSION)  # Off remains reachable for a retired source.


def test_host_client_keeps_proxy_only_in_internal_network_snapshot_and_ignores_failed_play():
    client = LiteHostClient("unused")
    request = tidal_request()
    status = {"id": "tidal:123", "source": None, "playing": True}
    def native(op, args, **kwargs):
        if op == "tidal.nativePlay" and args["id"] == "999":
            raise ValueError("Nie udało się odtworzyć")
        return dict(status) if op == "transport.status" else {"ok": True, "id": "tidal:" + args.get("id", "")}
    client._call_native = native
    client.call("tidal.nativePlay", request)
    public = client.call("transport.status", {"sessionId": "tidal"})
    assert "_tidal_request" not in public and "proxyUrl" not in str(public)
    private = client._network_native("transport.status", {"sessionId": "tidal"})
    assert private["_tidal_request"]["proxyUrl"] == request["proxyUrl"]
    assert private["source"] == "tidal:track:123"
    with pytest.raises(ValueError): client.call("tidal.nativePlay", {**request, "id": "999"})
    assert client._tidal_network_request["id"] == "123"
    client.close()
    assert client._tidal_network_request is None


def test_airplay_remains_discoverable_when_local_outputs_fail(monkeypatch):
    client = LiteHostClient("unused")
    client._tidal_network_request = tidal_request(dash=True)
    client.cast_devices = lambda **kwargs: (CastDevice("cast", "Salon", "192.0.2.1"),)
    client.audio_outputs = lambda *_: (_ for _ in ()).throw(RuntimeError("private details"))
    client._airplay_mirror = SimpleNamespace(devices=lambda **_k: (AirPlayDevice("air", "Salon"),))
    monkeypatch.setattr("amc_wx_lite.airplay_adapter._library", lambda _: object())
    monkeypatch.setattr("shutil.which", lambda _: "ffmpeg")
    snapshot = client.mirror_target_snapshot("tidal", None)
    assert [choice.device_id for choice in snapshot["devices"]] == ["airplay:air"]
    assert [str(choice) for choice in snapshot["devices"]] == ["AirPlay: Salon"]
    assert "private" not in snapshot["information"]
    assert "TIDAL" in snapshot["information"]


def test_airplay_missing_components_have_a_spoken_reason(monkeypatch):
    from amc_wx_lite.airplay_adapter import AirPlayUnavailable
    client = LiteHostClient("unused")
    client.cast_devices = lambda **_k: ()
    client.audio_outputs = lambda *_: {}
    def unavailable(_): raise AirPlayUnavailable("private details")
    monkeypatch.setattr("amc_wx_lite.airplay_adapter._library", unavailable)
    snapshot = client.mirror_target_snapshot("radioarchiwista", None)
    assert "AirPlay" in snapshot["information"]
    assert "private" not in snapshot["information"]


def test_tidal_airplay_follows_an_accepted_track_during_native_preparation(tmp_path):
    client = LiteHostClient("unused")
    client._tidal_network_request = tidal_request()
    status = {"id": "tidal:123", "source": None, "playing": False, "preparing": True}
    client._call_native = lambda *_a, **_k: status
    ffmpeg = tmp_path / "ffmpeg.exe"
    ffmpeg.write_bytes(b"not-executed")
    from amc_wx_lite.mirror_airplay import AirPlayRoute
    from amc_wx_lite.network_audio import source_for_session
    router = MirrorAirPlayRouter(client._network_native, str(ffmpeg))
    router.routes["tidal"] = AirPlayRoute(AirPlayDevice("air", "Salon"), str(ffmpeg))
    starts = []
    router._start = lambda session, route, snapshot: starts.append(
        (source_for_session(session, snapshot), snapshot["playing"]))
    try:
        router.after_native("tidal.nativePlay", tidal_request(), {"preparing": True})
        assert starts == [(tidal_request()["proxyUrl"], False)]
        assert not client.network_source_available("tidal", status)
    finally: router.close()


@pytest.mark.parametrize("mirror", [False, True])
def test_stale_picker_results_do_not_open_in_another_session(monkeypatch, mirror):
    import amc_wx_lite.gui as gui
    pending = []
    opened = []
    frame = SimpleNamespace(
        navigator=SimpleNamespace(active=SessionId.RADIOARCHIWISTA),
        client=SimpleNamespace(), announcer=SimpleNamespace(say=lambda _: None),
        runner=SimpleNamespace(submit=lambda name, work, done, failed: pending.append(done)),
        _window_alive=lambda: True,
        state=SimpleNamespace(audio_output_device_ids_by_session={}), _profile_audio_outputs={},
    )
    monkeypatch.setattr(gui, "CastDeviceDialog", lambda *_a, **_k: opened.append(True))
    monkeypatch.setattr(gui, "MirrorDeviceDialog", lambda *_a, **_k: opened.append(True))
    choose = gui.LiteFrame._choose_cast_mirror if mirror else gui.LiteFrame._choose_cast_output
    choose(frame)
    frame.navigator.active = SessionId.PODCASTS
    pending.pop()({"devices": (), "information": ""} if mirror else ())
    assert not opened
    frame.navigator.active = SessionId.RADIOARCHIWISTA
    choose(frame)
    choose(frame)
    pending[0]({"devices": (), "information": ""} if mirror else ())
    assert not opened


def test_actual_picker_labels_are_friendly_and_reopening_restores_airplay():
    import wx
    from amc_wx_lite.mirror_dialog import MirrorDeviceDialog
    from amc_wx_lite.mirror_local import MirrorChoice
    app = wx.GetApp() or wx.App(False)
    parent = wx.Frame(None)
    choices = (MirrorChoice("cast:private-id", "Google Cast: Salon"),
               MirrorChoice("airplay:private-id", "AirPlay: Gabinet"))
    try:
        for _ in range(2):
            with MirrorDeviceDialog(parent, "Radio Archiwista", choices, "airplay:private-id",
                                    information="Nie znaleziono pozostałych odbiorników.") as dialog:
                assert dialog.devices.GetStringSelection() == "AirPlay: Gabinet"
                assert dialog.selected_device_id == "airplay:private-id"
                assert "private" not in " ".join(dialog.devices.GetStrings())
                assert not dialog.IsShown()
    finally:
        parent.Destroy()
        app.Yield()


def test_cast_picker_announces_selected_output_in_list_on_reopen():
    import wx
    from amc_wx_lite.cast_dialog import CastDeviceDialog
    from amc_wx_lite.cast_protocol import CastDevice

    app = wx.GetApp() or wx.App(False)
    parent = wx.Frame(None)
    choices = (CastDevice("private-id", "WiiM Sound", "127.0.0.1", 8009),)
    try:
        for _ in range(2):
            with CastDeviceDialog(parent, "Radio", choices, "private-id") as dialog:
                assert dialog.devices.GetStringSelection() == "WiiM Sound, wybrane"
                assert dialog.selected_device_id == "private-id"
                assert "private-id" not in " ".join(dialog.devices.GetStrings())
                assert not dialog.IsShown()
    finally:
        parent.Destroy()
        app.Yield()


def test_cast_playback_context_requires_active_material():
    router, _calls, _events = router_fixture("radio")
    assert router.playback_context("radio") is None
    route = SimpleNamespace(
        device=CastDevice("private-id", "WiiM Sound", "192.0.2.1"),
        item={"title": "Radio 357"},
        stopped=False, failed=False, ended=False,
    )
    router.routes["radio"] = route
    assert router.playback_context("radio") == ("Radio 357", "WiiM Sound")
    route.stopped = True
    assert router.playback_context("radio") is None
    route.stopped, route.failed = False, True
    assert router.playback_context("radio") is None


def test_successful_native_handoff_from_another_cast_session_notifies_mirrors_once():
    router, calls, _ = router_fixture("files")
    client = LiteHostClient("unused")
    client._cast = router
    seen = []
    client._mirror = SimpleNamespace(after_native=lambda op, args, result: seen.append(op), close=lambda: None)
    try:
        router.select("files", "cast")
        result = client.call("media.play", {"sessionId": "radioarchiwista", "source": "https://archive.example/new.mp3"})
        assert seen == ["media.play"]
        assert sum(op == "media.play" for op, _ in calls) == 1
        assert router.routes["files"].stopped
    finally: client.close()


def test_unconfirmed_cast_mirror_stop_is_reported_instead_of_plain_success():
    class CannotStop(Connection):
        def transport(self, command, **kwargs):
            if command == "STOP": raise CastError("cast.error.lost")
            return super().transport(command, **kwargs)
    status = {"source": "https://radio.example/live", "playing": True}
    device = CastDevice("cast", "Salon", "192.0.2.1")
    client = LiteHostClient("unused")
    client._mirror = MirrorCastRouter(lambda *_: status, discover=lambda: (device,), connect=CannotStop)
    client._mirror.enable("radio", "cast")
    result = client.select_mirror_target("radio", None)
    assert not result["active"] and result["warning"]
    assert "cast.error" not in result["warning"]
    client.close()
