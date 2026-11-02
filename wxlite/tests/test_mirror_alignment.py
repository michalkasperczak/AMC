from __future__ import annotations

import sys
import unittest
from unittest.mock import patch
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from amc_wx_lite.cast_protocol import CastDevice, CastError
from amc_wx_lite.mirror_core import MirrorCastRouter


class FakeConnection:
    instances = []
    remote_position = 2.0
    fail_status = False
    states = []

    def __init__(self, device):
        self.device = device
        self.commands = []
        self.closed = False
        self.instances.append(self)

    def connect(self):
        self.commands.append("CONNECT")

    def load(self, *_args, **kwargs):
        self.commands.append(("LOAD", kwargs))

    def status(self):
        self.commands.append("STATUS")
        if self.fail_status:
            raise CastError("cast.error.timeout")
        state = self.states.pop(0) if self.states else "PLAYING"
        return {"playerState": state, "currentTime": self.remote_position}

    def transport(self, command, **fields):
        self.commands.append((command, fields))

    def close(self):
        self.closed = True


class MirrorPositionTests(unittest.TestCase):
    def setUp(self):
        FakeConnection.instances.clear()
        FakeConnection.remote_position = 2.0
        FakeConnection.fail_status = False
        FakeConnection.states = []
        self.device = CastDevice("receiver", "Salon", "192.168.0.116")

    def router(self, positions, *, session="files", paused=False):
        calls = []
        source = "https://example.org/tone.mp3"
        iterator = iter(positions)

        def native(op, args):
            calls.append((op, args))
            return {"source": source, "playing": not paused, "paused": paused,
                    "positionSeconds": next(iterator)}

        router = MirrorCastRouter(native, discover=lambda: (self.device,),
                                  connect=FakeConnection)
        return router, calls

    def test_catches_up_file_to_fresh_local_position(self):
        router, calls = self.router((0.2, 4.4))
        try:
            router.enable("files", "receiver")
            commands = FakeConnection.instances[0].commands
            self.assertIn(("SEEK", {"currentTime": 4.4}), commands)
            self.assertEqual([op for op, _ in calls], ["transport.status"] * 2)
        finally:
            router.close()

    def test_waits_for_buffering_receiver_before_seek(self):
        FakeConnection.states = ["BUFFERING", "PLAYING"]
        router, calls = self.router((0.2, 4.4))
        try:
            with patch("amc_wx_lite.mirror_core.time.sleep"):
                router.enable("files", "receiver")
            commands = FakeConnection.instances[0].commands
            self.assertIn(("SEEK", {"currentTime": 4.4}), commands)
            self.assertEqual(commands.count("STATUS"), 2)
            self.assertEqual([op for op, _ in calls], ["transport.status"] * 2)
        finally:
            router.close()

    def test_no_seek_when_receiver_is_already_close(self):
        FakeConnection.remote_position = 4.1
        router, _ = self.router((0.2, 4.4))
        try:
            router.enable("files", "receiver")
            self.assertFalse(any(isinstance(cmd, tuple) and cmd[0] == "SEEK"
                                 for cmd in FakeConnection.instances[0].commands))
        finally:
            router.close()

    def test_radio_and_paused_file_do_not_seek(self):
        for session, paused in (("radio", False), ("files", True)):
            router, calls = self.router((0.2,), session=session, paused=paused)
            try:
                router.enable(session, "receiver")
                self.assertEqual([op for op, _ in calls], ["transport.status"])
                self.assertNotIn("STATUS", FakeConnection.instances[-1].commands)
            finally:
                router.close()

    def test_status_failure_keeps_second_target(self):
        FakeConnection.fail_status = True
        router, _ = self.router((0.2, 4.4))
        try:
            router.enable("files", "receiver")
            self.assertEqual(router.selected_device_id("files"), "receiver")
        finally:
            router.close()


if __name__ == "__main__":
    unittest.main()
