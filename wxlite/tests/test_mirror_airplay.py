from __future__ import annotations

import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

from amc_wx_lite.airplay_adapter import (
    AirPlayUnavailable, discover_airplay_devices, stream_local_file,
)
from amc_wx_lite.cast_protocol import CastDevice, CastError
from amc_wx_lite.mirror_core import MirrorCastRouter
from amc_wx_lite.host_client import LiteHostClient


class FakeCastConnection:
    fail_load = False
    instances = []

    def __init__(self, device):
        self.device = device
        self.commands = []
        self.closed = False
        self.instances.append(self)

    def connect(self):
        self.commands.append("CONNECT")

    def load(self, url, mime, title, **kwargs):
        if self.fail_load:
            raise CastError("cast.error.notStarted")
        self.commands.append(("LOAD", url, kwargs))

    def transport(self, command, **kwargs):
        self.commands.append((command, kwargs))

    def close(self):
        self.closed = True


class MirrorCoreTests(unittest.TestCase):
    def setUp(self):
        FakeCastConnection.instances.clear()
        FakeCastConnection.fail_load = False
        self.native_calls = []
        self.events = []
        self.device = CastDevice("cast-1", "Salon", "192.168.1.20")

        def native(op, args):
            self.native_calls.append((op, args))
            return {"source": "https://radio.example/live", "playing": True,
                    "paused": False, "title": "Radio", "positionSeconds": 0}

        self.router = MirrorCastRouter(
            native, lambda name, data: self.events.append((name, data)),
            discover=lambda: (self.device,), connect=FakeCastConnection,
        )

    def tearDown(self):
        self.router.close()

    def test_enabling_keeps_local_playback_and_stopping_mirror_keeps_it(self):
        result = self.router.enable("radio", "cast-1")
        self.assertEqual(result, {"name": "Salon", "synchronized": False})
        self.assertEqual([op for op, _ in self.native_calls], ["transport.status"])
        self.assertEqual(self.router.selected_device_id("radio"), "cast-1")
        self.router.disable("radio")
        self.assertEqual([op for op, _ in self.native_calls], ["transport.status"])
        self.assertIn(("STOP", {}), FakeCastConnection.instances[0].commands)
        self.assertTrue(FakeCastConnection.instances[0].closed)

    def test_successful_local_commands_are_reflected_to_second_target(self):
        self.router.enable("radio", "cast-1")
        route = FakeCastConnection.instances[0]
        self.router.after_native("transport.pauseResume", {"sessionId": "radio"},
                                 {"paused": True})
        self.router.after_native("radio.play", {"url": "https://radio.example/next",
                                                "title": "Następne"}, {"playing": True})
        self.assertIn(("PAUSE", {}), route.commands)
        self.assertEqual(route.commands[-1][0], "LOAD")
        self.assertEqual(self.router.selected_device_id("radio"), "cast-1")

    def test_receiver_failure_drops_only_mirror(self):
        self.router.enable("radio", "cast-1")
        route = FakeCastConnection.instances[0]
        def failed(*_args, **_kwargs):
            raise CastError("cast.error.lost")
        route.transport = failed
        self.router.after_native("transport.pauseResume", {"sessionId": "radio"},
                                 {"paused": True})
        self.assertIsNone(self.router.selected_device_id("radio"))
        self.assertTrue(route.closed)
        self.assertEqual(self.events[0][0], "mirror.failed")
        self.assertEqual([op for op, _ in self.native_calls], ["transport.status"])

    def test_rejected_start_keeps_previous_route(self):
        self.router.enable("radio", "cast-1")
        previous = FakeCastConnection.instances[0]
        self.router._discover = lambda: (self.device, CastDevice("cast-2", "Gabinet", "192.168.1.21"))
        FakeCastConnection.fail_load = True
        with self.assertRaises(CastError):
            self.router.enable("radio", "cast-2")
        self.assertEqual(self.router.selected_device_id("radio"), "cast-1")
        self.assertFalse(previous.closed)


class MirrorClientTests(unittest.TestCase):
    def test_client_forwards_successful_native_transport_to_mirror(self):
        seen = []
        client = object.__new__(LiteHostClient)
        client._cast = None
        client._mirror = SimpleNamespace(after_native=lambda op, args, result:
                                         seen.append((op, args, result)))
        client._call_native = lambda op, args, timeout: {"paused": True}
        result = client.call("transport.pauseResume", {"sessionId": "radio"})
        self.assertEqual(result, {"paused": True})
        self.assertEqual(seen, [("transport.pauseResume", {"sessionId": "radio"}, result)])

    def test_direct_cast_selection_rejects_active_mirror_without_stopping_it(self):
        client = object.__new__(LiteHostClient)
        client._mirror = SimpleNamespace(selected_device_id=lambda session: "cast-1")
        with self.assertRaisesRegex(ValueError, "Shift\\+M"):
            client.select_cast("radio", "cast-1")

    def test_direct_cast_cannot_take_receiver_used_by_other_mirror_session(self):
        client = object.__new__(LiteHostClient)
        client._cast = None
        client._mirror = SimpleNamespace(
            selected_device_id=lambda session: None,
            routes={"files": SimpleNamespace(device=CastDevice("cast-1", "Salon", "192.168.1.20"))},
        )
        with self.assertRaisesRegex(ValueError, "już odtwarza"):
            client.select_cast("radio", "cast-1")

    def test_mirror_cannot_take_receiver_used_by_other_direct_cast_session(self):
        client = object.__new__(LiteHostClient)
        client._mirror = None
        client._cast = SimpleNamespace(
            active=lambda session: False,
            routes={"radio": SimpleNamespace(device=CastDevice("cast-1", "Salon", "192.168.1.20"))},
        )
        with self.assertRaisesRegex(ValueError, "inną sesję"):
            client.select_cast_mirror("files", "cast-1")


class FakeAirPlayLibrary:
    def __init__(self):
        protocol = object()
        self.const = SimpleNamespace(Protocol=SimpleNamespace(RAOP=protocol))
        self.configs = []
        self.scans = []
        self.receiver = SimpleNamespace(stream=SimpleNamespace(stream_file=self._stream_file),
                                        close=self._close)
        self.streamed = []
        self.closed = False

    async def scan(self, loop, **kwargs):
        self.scans.append(kwargs)
        return self.configs

    async def connect(self, config, loop, **kwargs):
        return self.receiver

    async def _stream_file(self, path):
        self.streamed.append(path)

    def _close(self):
        self.closed = True


def config(identifier, name, protocol, available=True):
    return SimpleNamespace(identifier=identifier, name=name,
                           get_service=lambda requested: object() if available and requested is protocol else None)


def complete_immediately(coroutine):
    """Drive fake async dependencies without opening a sandbox network loop."""
    with patch("amc_wx_lite.airplay_adapter.asyncio.get_running_loop", return_value=object()):
        try:
            coroutine.send(None)
        except StopIteration as completed:
            return completed.value
        raise AssertionError("An unexpected external await was reached")


class AirPlayAdapterTests(unittest.TestCase):
    def test_discovery_exposes_names_and_keeps_ids_internal(self):
        library = FakeAirPlayLibrary()
        protocol = library.const.Protocol.RAOP
        library.configs = [config("private-id", " Salon ", protocol),
                           config("private-id", "Duplikat", protocol),
                           config("other", "Bez RAOP", protocol, False)]
        devices = complete_immediately(discover_airplay_devices(module=library))
        self.assertEqual([(device.device_id, device.label) for device in devices],
                         [("private-id", "Salon")])
        self.assertEqual(str(devices[0]), "Salon")
        self.assertNotIn("private-id", str(devices[0]))

    def test_file_proof_rescans_and_closes_receiver(self):
        library = FakeAirPlayLibrary()
        protocol = library.const.Protocol.RAOP
        library.configs = [config("air-1", "Gabinet", protocol)]
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "clip.wav"
            path.write_bytes(b"RIFF")
            complete_immediately(stream_local_file("air-1", path, module=library))
            self.assertEqual(library.streamed, [str(path)])
            self.assertTrue(library.closed)
            self.assertEqual(library.scans[-1]["identifier"], "air-1")

    def test_missing_file_never_connects(self):
        library = FakeAirPlayLibrary()
        with self.assertRaises(AirPlayUnavailable):
            complete_immediately(stream_local_file("air-1", "missing.wav", module=library))
        self.assertEqual(library.scans, [])


if __name__ == "__main__":
    unittest.main()
