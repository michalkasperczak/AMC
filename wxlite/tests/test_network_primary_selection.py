"""Shared wireless picker and source routing with isolated clients."""
from __future__ import annotations

from types import SimpleNamespace

import pytest

from amc_wx_lite.airplay_adapter import AirPlayDevice
from amc_wx_lite.cast_protocol import CastDevice
from amc_wx_lite.cast_session import UNHANDLED
from amc_wx_lite.host_client import LiteHostClient


class Primary:
    def __init__(self, selected=None):
        self.selected = selected
        self.routes = ({"radio": SimpleNamespace(volume=22,
            device=AirPlayDevice(selected, "Salon"))} if selected else {})
        self.calls = []

    def devices(self, *, refresh=False):
        return (AirPlayDevice("private-air", "Salon"),)

    def selected_device_id(self, session):
        return self.selected

    def select(self, session, device_id, *, volume_override=None):
        self.calls.append((session, device_id, volume_override))
        self.selected = device_id
        if device_id is None:
            self.routes.pop(session, None)
        else:
            self.routes[session] = SimpleNamespace(volume=volume_override or 22,
                device=AirPlayDevice(device_id, "Salon"))
        return {"name": "AirPlay: Salon" if device_id else "Ten komputer"}


def test_shared_picker_contains_protocol_labels_without_private_ids():
    client = LiteHostClient("unused")
    client._airplay_primary = Primary()
    client._network_native = lambda *_args: {"playing": False}
    client.cast_devices = lambda **_kwargs: (
        CastDevice("private-cast", "Salon", "192.0.2.1"),)
    snapshot = client.network_output_snapshot("radio")
    assert [(item.device_id, item.label) for item in snapshot["devices"]] == [
        ("cast:private-cast", "Google Cast: Salon"),
        ("airplay:private-air", "AirPlay: Salon"),
    ]
    assert all("private" not in item.label for item in snapshot["devices"])


def test_airplay_remains_available_when_cast_discovery_fails():
    client = LiteHostClient("unused")
    client._airplay_primary = Primary()
    client._network_native = lambda *_args: {"playing": False}
    client.cast_devices = lambda **_kwargs: (_ for _ in ()).throw(RuntimeError("network"))
    snapshot = client.network_output_snapshot("radio")
    assert [item.device_id for item in snapshot["devices"]] == ["airplay:private-air"]
    assert "Google Cast" in snapshot["information"]
    assert "network" not in snapshot["information"]


def test_tidal_dash_hides_cast_choice_but_keeps_airplay():
    client = LiteHostClient("unused")
    client._airplay_primary = Primary()
    client._network_native = lambda *_args: {"id": "tidal:123", "playing": True}
    client.network_source_available = lambda *_args, **_kwargs: False
    client.cast_devices = lambda **_kwargs: (
        CastDevice("private-cast", "Salon", "192.0.2.1"),)
    snapshot = client.network_output_snapshot("tidal")
    assert [item.device_id for item in snapshot["devices"]] == ["airplay:private-air"]


def test_offline_selected_airplay_remains_identifiable_in_picker():
    client = LiteHostClient("unused")
    primary = Primary("private-air")
    primary.devices = lambda **_kwargs: ()
    client._airplay_primary = primary
    client._network_native = lambda *_args: {"playing": False}
    client.cast_devices = lambda **_kwargs: ()
    snapshot = client.network_output_snapshot("radio")
    assert [(item.device_id, item.label, item.available)
            for item in snapshot["devices"]] == [
        ("airplay:private-air", "AirPlay: Salon, niedostępne", False)]
    assert client.network_selected_target_id("radio") == "airplay:private-air"


def test_unsupported_cast_format_keeps_airplay_but_hides_cast_choice():
    client = LiteHostClient("unused")
    client._airplay_primary = Primary()
    client._network_native = lambda *_args: {
        "source": r"C:\Music\episode.xyz", "playing": True}
    client.cast_devices = lambda **_kwargs: (
        CastDevice("private-cast", "Salon", "192.0.2.1"),)
    snapshot = client.network_output_snapshot("files")
    assert [item.device_id for item in snapshot["devices"]] == ["airplay:private-air"]


def test_primary_picker_hides_receiver_already_used_by_mirror():
    client = LiteHostClient("unused")
    client._network_native = lambda *_args: {"playing": False}
    client._airplay_primary = Primary()
    client._airplay_mirror = SimpleNamespace(routes={
        "files": SimpleNamespace(device=AirPlayDevice("private-air", "Salon"))})
    client.cast_devices = lambda **_kwargs: ()
    assert client.network_output_snapshot("radio")["devices"] == ()


def test_mirror_cannot_reuse_primary_airplay_receiver():
    client = LiteHostClient("unused")
    client._airplay_primary = Primary("private-air")
    with pytest.raises(ValueError, match="głównym wyjściem"):
        client.select_mirror_target("radio", "airplay:private-air")


def test_cast_failure_restores_previous_airplay_selection():
    client = LiteHostClient("unused")
    primary = Primary("private-air")
    client._airplay_primary = primary
    client.select_cast = lambda *_args: (_ for _ in ()).throw(ValueError("Cast unavailable"))
    with pytest.raises(ValueError, match="Cast unavailable"):
        client.select_network_output("radio", "cast:private-cast")
    assert primary.calls == [
        ("radio", None, None),
        ("radio", "private-air", 22),
    ]
    assert client.network_selected_target_id("radio") == "airplay:private-air"


def test_cast_to_airplay_returns_local_at_zero_before_receiver_starts():
    client = LiteHostClient("unused")
    route = SimpleNamespace(native_volume=47, device=CastDevice(
        "private-cast", "Salon", "192.0.2.1"))
    active = {"radio": route}
    client._cast = SimpleNamespace(routes=active,
                                   active=lambda session: session in active)
    primary = Primary()
    client._airplay_primary = primary
    seen = []

    def select_cast(session, device_id):
        seen.append(("local", route.native_volume))
        active.pop(session)
        return {"name": "Ten komputer"}

    client.select_cast = select_cast
    client._network_native = lambda op, args: seen.append((op, args["volume"]))
    client.mirror_selected_target_id = lambda _session: None
    client.select_network_output("radio", "airplay:private-air")
    assert seen == [("local", 0), ("transport.setVolume", 47)]
    assert primary.calls == [("radio", "private-air", 47)]
    assert route.native_volume == 47


def test_failed_cast_to_airplay_switch_reselects_cast():
    client = LiteHostClient("unused")
    route = SimpleNamespace(native_volume=47, device=CastDevice(
        "private-cast", "Salon", "192.0.2.1"))
    active = {"radio": route}
    client._cast = SimpleNamespace(routes=active,
                                   active=lambda session: session in active)
    primary = Primary()
    primary.select = lambda *_args, **_kwargs: (_ for _ in ()).throw(
        ValueError("AirPlay unavailable"))
    client._airplay_primary = primary
    calls = []

    def select_cast(session, device_id):
        calls.append((device_id, route.native_volume))
        if device_id is None:
            active.pop(session)
        else:
            active[session] = route
        return {"name": "Salon"}

    client.select_cast = select_cast
    client._network_native = lambda op, args: calls.append((op, args["volume"]))
    client.mirror_selected_target_id = lambda _session: None
    with pytest.raises(ValueError, match="AirPlay unavailable"):
        client.select_network_output("radio", "airplay:private-air")
    assert calls == [(None, 0), ("transport.setVolume", 47), ("private-cast", 47)]
    assert client.cast_active("radio")


def test_host_dispatches_primary_volume_and_native_play_in_order():
    client = LiteHostClient("unused")
    seen = []
    primary = SimpleNamespace(
        retire_displaced=lambda op, args: seen.append(("retire", op)),
        call=lambda op, args: {"volume": 12} if op == "transport.setVolume" else UNHANDLED,
        after_native=lambda op, args, result: seen.append(("after", op)),
    )
    client._airplay_primary = primary
    client._call_native = lambda op, args, **_kwargs: seen.append(("native", op)) or {"ok": True}
    client._notify_mirrors = lambda op, args, result: seen.append(("mirror", op))
    assert client.call("transport.setVolume", {"sessionId": "radio", "volume": 12}) == {
        "volume": 12}
    assert seen == [("retire", "transport.setVolume"), ("mirror", "transport.setVolume")]
    seen.clear()
    assert client.call("radio.play", {"url": "https://radio.example/live"}) == {"ok": True}
    assert seen == [("retire", "radio.play"), ("native", "radio.play"),
                    ("after", "radio.play"), ("mirror", "radio.play")]


def test_tidal_request_is_remembered_before_airplay_starts_new_track():
    client = LiteHostClient("unused")
    request = {"id": "123", "proxyUrl": "http://127.0.0.1:1234/audio/" + "A" * 32 + ".mpd"}
    seen = []
    client._airplay_primary = SimpleNamespace(
        retire_displaced=lambda *_args: None,
        call=lambda op, args: {"ok": True, "id": "tidal:123"},
        after_native=lambda op, args, result: seen.append(
            client._tidal_network_request == request),
    )
    client._notify_mirrors = lambda *_args: None
    client.call("tidal.nativePlay", request)
    assert seen == [True]
