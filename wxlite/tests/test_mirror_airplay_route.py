"""AirPlay mirror lifecycle and offset contract without network or audio."""
from __future__ import annotations

import tempfile
import unittest
from pathlib import Path

from amc_wx_lite.airplay_adapter import AirPlayDevice
from amc_wx_lite.mirror_airplay import AirPlayRoute, MirrorAirPlayRouter, ffmpeg_command


class AirPlayRouteTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.ffmpeg = Path(self.temporary.name) / "ffmpeg.exe"
        self.ffmpeg.write_bytes(b"test")
        self.device = AirPlayDevice("private-id", "WiiM Pro Plus")
        self.events = []
        self.status = {"source": r"C:\Music\song.flac", "playing": True,
                       "paused": False, "positionSeconds": 12.5}
        async def discover():
            return (self.device,)
        self.router = MirrorAirPlayRouter(
            lambda op, args: self.status, str(self.ffmpeg),
            lambda name, data: self.events.append((name, data)),
            discover=discover)
        self.starts = []
        self.router._start = lambda session, route, status: self.starts.append(
            (session, route.device.device_id, status["positionSeconds"]))

    def tearDown(self):
        self.router.close()
        self.temporary.cleanup()

    def test_uses_ffmpeg_with_current_position_and_no_shell(self):
        command = ffmpeg_command("ffmpeg.exe", r"C:\Music\song.flac", 12.5)
        self.assertEqual(command[0], "ffmpeg.exe")
        self.assertEqual(command[command.index("-ss") + 1], "12.5")
        self.assertEqual(command[command.index("-i") + 1], r"C:\Music\song.flac")
        self.assertEqual(command[-1], "pipe:1")

    def test_selection_uses_spoken_name_and_resumes_after_seek(self):
        result = self.router.enable("files", "private-id")
        self.assertEqual(result, {"name": "WiiM Pro Plus", "synchronized": False})
        self.assertEqual(self.router.selected_device_id("files"), "private-id")
        self.assertEqual(self.starts, [("files", "private-id", 12.5)])
        self.status["positionSeconds"] = 30
        self.router.after_native("transport.seek", {"sessionId": "files"},
                                 {"positionSeconds": 30})
        self.assertEqual(self.starts[-1], ("files", "private-id", 30))
        self.router.after_native("transport.stop", {"sessionId": "files"}, {})
        self.assertIsNone(self.router.selected_device_id("files"))

    def test_start_failure_keeps_previous_receiver(self):
        self.router.enable("files", "private-id")
        other = AirPlayDevice("other", "Gabinet")
        self.router.devices = lambda: (self.device, other)
        self.router._start = lambda *args: (_ for _ in ()).throw(RuntimeError("RAOP"))
        with self.assertRaisesRegex(RuntimeError, "RAOP"):
            self.router.enable("files", "other")
        self.assertEqual(self.router.selected_device_id("files"), "private-id")

    def test_failure_after_primary_seek_drops_only_airplay(self):
        self.router.enable("files", "private-id")
        self.router._start = lambda *args: (_ for _ in ()).throw(RuntimeError("RAOP"))
        self.router.after_native("transport.seek", {"sessionId": "files"}, {})
        self.assertIsNone(self.router.selected_device_id("files"))
        self.assertEqual(self.events[0][0], "mirror.failed")

    def test_startup_failure_uses_spoken_device_name_without_protocol_dump(self):
        async def fail(route, position):
            raise TimeoutError("no response to GET /info (RTSP/1.0)")

        self.router._stream = fail
        self.router._start = MirrorAirPlayRouter._start.__get__(self.router)
        route = AirPlayRoute(self.device, str(self.ffmpeg))
        with self.assertRaisesRegex(RuntimeError, "WiiM Pro Plus nie odpowiada przez AirPlay"):
            self.router._start("files", route, self.status)


class AirPlayRetryTests(unittest.IsolatedAsyncioTestCase):
    async def test_retries_once_when_receiver_does_not_answer_before_playback(self):
        router = MirrorAirPlayRouter(lambda op, args: {}, "unused")
        route = AirPlayRoute(AirPlayDevice("id", "WiiM"), "unused")
        attempts = []

        async def stream_once(current, position):
            attempts.append(position)
            if len(attempts) == 1:
                raise TimeoutError("no response to GET /info")

        router._stream_once = stream_once
        await router._stream(route, 12.5)
        self.assertEqual(attempts, [12.5, 12.5])

    async def test_does_not_retry_after_receiver_confirmed_playback(self):
        router = MirrorAirPlayRouter(lambda op, args: {}, "unused")
        route = AirPlayRoute(AirPlayDevice("id", "WiiM"), "unused")
        attempts = []

        async def stream_once(current, position):
            attempts.append(position)
            route.ready.set()
            raise TimeoutError("stream was interrupted")

        router._stream_once = stream_once
        with self.assertRaises(TimeoutError):
            await router._stream(route, 0)
        self.assertEqual(attempts, [0])


if __name__ == "__main__":
    unittest.main()
