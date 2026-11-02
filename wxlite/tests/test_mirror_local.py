"""Second local AMC output without wx or a real audio device."""
from __future__ import annotations

import unittest
from types import SimpleNamespace

from amc_wx_lite.mirror_local import MirrorLocalRouter
from amc_wx_lite.host_client import LiteHostClient
from amc_wx_lite.cast_protocol import CastDevice


class FakeHost:
    def __init__(self, *, fallback=False):
        self.calls = []
        self.started = False
        self.closed = False
        self.fallback = fallback

    def start(self):
        self.started = True

    def call(self, op, args, **kwargs):
        self.calls.append((op, args))
        if op == "audio.selectOutput":
            return {"usingDefault": self.fallback}
        return {"ok": True}

    def close(self):
        self.closed = True


class LocalMirrorTests(unittest.TestCase):
    def setUp(self):
        self.hosts = []
        self.native_calls = []
        self.status = {"id": "song", "title": "Utwór", "source": r"C:\Music\song.wav",
                       "playing": True, "paused": False, "positionSeconds": 4.5,
                       "volume": 40, "rate": 1.0}
        def native(op, args):
            self.native_calls.append((op, args))
            return self.status
        def create():
            host = FakeHost()
            self.hosts.append(host)
            return host
        self.router = MirrorLocalRouter(native, create)

    def tearDown(self):
        self.router.close()

    def test_second_host_selects_device_then_starts_at_current_position(self):
        result = self.router.enable("files", "usb-device", "Denon USB")
        self.assertEqual(result, {"name": "Denon USB", "synchronized": False})
        host = self.hosts[0]
        self.assertTrue(host.started)
        self.assertEqual([op for op, _ in host.calls], ["audio.selectOutput", "files.play"])
        self.assertEqual(host.calls[0][1]["deviceId"], "usb-device")
        self.assertEqual(host.calls[1][1]["positionSeconds"], 4.5)
        self.router.after_native("transport.pauseResume", {"sessionId": "files"}, {"paused": True})
        self.assertEqual(host.calls[-1][0], "transport.pauseResume")
        self.router.after_native("transport.stop", {"sessionId": "files"}, {"ok": True})
        self.assertTrue(host.closed)
        self.assertIsNone(self.router.selected_device_id("files"))

    def test_unavailable_second_device_keeps_primary(self):
        host = FakeHost(fallback=True)
        self.router.create_client = lambda: host
        with self.assertRaisesRegex(ValueError, "niedostępne"):
            self.router.enable("radio", "missing", "Denon")
        self.assertTrue(host.closed)
        self.assertIsNone(self.router.selected_device_id("radio"))
        self.assertEqual([op for op, _ in self.native_calls], ["transport.status"])

    def test_radio_uses_radio_operation_and_keeps_source(self):
        self.status["source"] = "https://radio.example/live"
        self.router.enable("radio", "speakers", "Głośniki")
        op, args = self.hosts[0].calls[-1]
        self.assertEqual(op, "radio.play")
        self.assertEqual(args["url"], self.status["source"])

    def test_target_labels_are_user_facing_and_discovery_survives_cast_failure(self):
        client = object.__new__(LiteHostClient)
        client.cast_devices = lambda **kwargs: (_ for _ in ()).throw(RuntimeError("mDNS"))
        client.audio_outputs = lambda *args: {"devices": [
            {"id": "primary", "name": "Denon USB", "available": True},
            {"id": "second", "name": "Głośniki komputera", "available": True},
        ]}
        choices = client.mirror_targets("files", "primary")
        self.assertEqual([(item.device_id, item.label) for item in choices],
                         [("local:second", "Wyjście lokalne: Głośniki komputera")])
        self.assertNotIn("second", choices[0].label)


if __name__ == "__main__":
    unittest.main()
