"""Primary AirPlay owns one audible output; tests use no network or audio."""
from __future__ import annotations

from pathlib import Path

import pytest

from amc_wx_lite.airplay_adapter import AirPlayDevice
from amc_wx_lite.airplay_primary import PrimaryAirPlayRouter
from amc_wx_lite.cast_session import UNHANDLED


class Native:
    def __init__(self, *, playing=True, muted=False, volume=42):
        self.status = {"source": "https://radio.example/live" if playing else None,
                       "title": "Radio 357" if playing else "",
                       "playing": playing, "paused": False, "muted": muted,
                       "volume": volume, "positionSeconds": 0}
        self.calls = []

    def __call__(self, op, args):
        self.calls.append((op, dict(args)))
        if op == "transport.status":
            return dict(self.status)
        if op == "transport.toggleMute":
            self.status["muted"] = not self.status["muted"]
            return {"muted": self.status["muted"]}
        if op == "transport.setVolume":
            self.status["volume"] = args["volume"]
            return {"volume": args["volume"]}
        if op == "radio.play":
            self.status.update(source=args["url"], title=args["title"], playing=True,
                               paused=False, volume=args["volume"])
            return {"playing": True, "volume": args["volume"]}
        if op == "transport.pauseResume":
            self.status["paused"] = not self.status["paused"]
            self.status["playing"] = not self.status["paused"]
            return {"paused": self.status["paused"]}
        if op == "transport.stop":
            self.status["playing"] = False
            self.status["paused"] = False
            return {"playing": False}
        raise AssertionError(op)


class Stream:
    def __init__(self, native):
        self.native = native
        self.selected = {}
        self.volumes = []
        self.fail = False
        self.events = []

    def devices(self, *, refresh=False):
        return (AirPlayDevice("private-one", "WiiM Sound"),
                AirPlayDevice("private-two", "Gabinet"))

    def selected_device_id(self, session):
        return self.selected.get(session)

    def enable(self, session, device_id, *, volume=None):
        assert self.native.status["muted"] is True
        if self.fail:
            raise RuntimeError("stream failed")
        self.selected[session] = device_id
        self.volumes.append((session, volume))
        return {"name": device_id}

    def disable(self, session):
        self.selected.pop(session, None)
        return {"active": False}

    def set_volume(self, session, value):
        self.volumes.append((session, value))

    def after_native(self, op, args, result):
        self.events.append((op, dict(args)))

    def close(self):
        self.selected.clear()


@pytest.fixture
def route(tmp_path: Path):
    ffmpeg = tmp_path / "ffmpeg.exe"
    ffmpeg.write_bytes(b"fake")
    native = Native()
    stream = Stream(native)
    events = []
    router = PrimaryAirPlayRouter(native, str(ffmpeg),
                                  lambda name, data: events.append((name, data)), stream=stream)
    yield router, native, stream, events
    router.close()


def test_select_mutes_local_then_starts_receiver_and_restores_on_return(route):
    router, native, stream, _events = route
    assert router.select("radio", "private-one") == {"name": "AirPlay: WiiM Sound"}
    assert native.status["muted"] is True
    assert stream.volumes == [("radio", 30)]
    assert router.playback_context("radio") == ("Radio 357", "WiiM Sound")
    assert router.call("transport.status", {"sessionId": "radio"})["muted"] is False
    assert router.select("radio", None) == {"name": "Ten komputer"}
    assert native.status["muted"] is False
    assert stream.selected == {}


def test_existing_user_mute_is_preserved(route):
    router, native, stream, _events = route
    native.status["muted"] = True
    router.select("radio", "private-one")
    assert stream.volumes[-1] == ("radio", 0)
    router.select("radio", None)
    assert native.status["muted"] is True


def test_unmuting_airplay_carries_to_local_on_return(route):
    router, native, _stream, _events = route
    native.status["muted"] = True
    router.select("radio", "private-one")
    router.call("transport.toggleMute", {"sessionId": "radio"})
    router.select("radio", None)
    assert native.status["muted"] is False


def test_initial_airplay_level_is_limited_when_switching_from_loud_cast(route):
    router, _native, stream, _events = route
    router.select("radio", "private-one", volume_override=80)
    assert stream.volumes[-1] == ("radio", 30)


def test_rejected_receiver_restores_local_sound(route):
    router, native, stream, _events = route
    stream.fail = True
    with pytest.raises(RuntimeError, match="Nie udało się uruchomić AirPlay na WiiM Sound"):
        router.select("radio", "private-one")
    assert router.selected_device_id("radio") is None
    assert native.status["muted"] is False


def test_idle_selection_starts_next_station_silently_locally(route):
    router, native, stream, _events = route
    native.status.update(source=None, title="", playing=False)
    router.select("radio", "private-one")
    assert native.status["muted"] is False
    assert stream.selected == {}
    result = router.call("radio.play", {"url": "https://radio.example/next",
                                        "title": "Następna", "volume": 55})
    assert result["playing"] is True
    assert ("radio.play", {"url": "https://radio.example/next",
                           "title": "Następna", "volume": 0}) in native.calls
    assert native.status["muted"] is True
    assert stream.selected == {"radio": "private-one"}
    assert router.playback_context("radio") == ("Następna", "WiiM Sound")
    assert router.call("transport.pauseResume", {"sessionId": "radio"}) is UNHANDLED


def test_receiver_failure_on_first_idle_play_restores_local(route):
    router, native, stream, _events = route
    native.status.update(source=None, title="", playing=False)
    router.select("radio", "private-one")
    stream.fail = True
    with pytest.raises(RuntimeError, match="Utracono AirPlay"):
        router.call("radio.play", {"url": "https://radio.example/next",
                                   "title": "Następna", "volume": 55})
    assert router.selected_device_id("radio") is None
    assert native.status["muted"] is False


def test_volume_and_user_mute_control_airplay_not_local_mute(route):
    router, native, stream, _events = route
    router.select("radio", "private-one")
    assert router.call("transport.setVolume", {"sessionId": "radio", "volume": 18}) == {
        "volume": 18, "muted": False}
    assert native.status["muted"] is True
    assert stream.volumes[-1] == ("radio", 18)
    assert router.call("transport.toggleMute", {"sessionId": "radio"}) == {
        "volume": 18, "muted": True}
    assert stream.volumes[-1] == ("radio", 0)
    assert router.call("transport.status", {"sessionId": "radio"})["muted"] is True
    router.select("radio", None)
    assert native.status["muted"] is True


def test_lost_receiver_restores_local_and_reports_failure(route):
    router, native, stream, events = route
    router.select("radio", "private-one")
    router._on_stream_event("mirror.failed", {"sessionId": "radio"})
    assert router.selected_device_id("radio") is None
    assert native.status["muted"] is False
    assert events[0][0] == "playback.failed"
    assert "private-one" not in events[0][1]["message"]


def test_status_detects_automatic_next_material_and_restarts_raop(route):
    router, native, stream, _events = route
    router.select("radio", "private-one")
    native.status.update(source="https://radio.example/next", title="Następna",
                         playing=True)
    router.call("transport.status", {"sessionId": "radio"})
    assert stream.events[-1][0] == "transport.seek"
    assert router.playback_context("radio") == ("Następna", "WiiM Sound")


def test_new_shared_decoder_owner_restores_local_before_play(route):
    router, native, stream, _events = route
    router.select("files", "private-one")
    assert native.status["muted"] is True
    router.retire_displaced("media.play", {"sessionId": "podcasts"})
    assert router.selected_device_id("files") is None
    assert stream.selected == {}
    assert native.status["muted"] is False


def test_stopped_tidal_restores_local_mute(tmp_path: Path):
    ffmpeg = tmp_path / "ffmpeg.exe"
    ffmpeg.write_bytes(b"fake")
    native = Native()
    native.status["_tidal_request"] = {
        "id": "123", "proxyUrl": "http://127.0.0.1:1234/audio/" + "A" * 32 + ".mpd"}
    stream = Stream(native)
    router = PrimaryAirPlayRouter(native, str(ffmpeg), stream=stream)
    router.select("tidal", "private-one")
    native("transport.stop", {"sessionId": "tidal"})
    router.after_native("transport.stop", {"sessionId": "tidal"}, {})
    router.select("tidal", None)
    assert native.status["muted"] is False
