"""Profile choice, queue binding, and NVDA restart without a live reader."""

from pathlib import Path
import sys
import tempfile
import threading
import types
import unittest


ROOT = Path(__file__).resolve().parents[2]
PLUGIN = ROOT / "nvda-addon/addon/globalPlugins/amcController"
package = types.ModuleType("amcController_routing_test")
package.__path__ = [str(PLUGIN)]
sys.modules[package.__name__] = package
from amcController_routing_test import routing, transport, worker


class ProfileRoutingTests(unittest.TestCase):
    def test_pipe_names_match_both_hosts_and_never_use_shared_pipe(self):
        self.assertEqual(transport.pipe_name("test", 7), "AMC.NVDA.v1.7.test")
        self.assertEqual(transport.pipe_name("public", 7), "AMC.NVDA.v1.7.public")
        for invalid in ("", "Test", "other", "public.extra"):
            with self.assertRaises(transport.BridgeError):
                transport.pipe_name(invalid, 7)
        with self.assertRaises(transport.BridgeError):
            transport.exchange("status")

    def test_active_window_selects_its_profile_with_both_hosts_running(self):
        with tempfile.TemporaryDirectory() as folder:
            state = Path(folder) / "last.txt"
            foreground = [12, [(12, "AMC Test — 10.11.2026, 15:00")]]
            router = routing.ProfileRouter(
                state, window_snapshot=lambda: foreground,
                available=lambda profile: True,
            )
            self.assertEqual(router.select(), "test")
            foreground[:] = [13, [(13, "AMC Public — 10.11.2026, 15:01")]]
            self.assertEqual(router.select(), "public")
            self.assertEqual(state.read_text(encoding="ascii").strip(), "public")
            foreground[:] = [99, [(99, "Edytor")]]
            self.assertEqual(router.select(), "public")

    def test_restart_keeps_last_profile_outside_amc(self):
        with tempfile.TemporaryDirectory() as folder:
            state = Path(folder) / "last.txt"
            windows = [15, [(15, "AMC Test — 10.11.2026, 15:00")]]
            make = lambda: routing.ProfileRouter(
                state, window_snapshot=lambda: windows,
                available=lambda profile: True,
            )
            self.assertEqual(make().select(), "test")
            windows[:] = [99, [(99, "Edytor")]]
            self.assertEqual(make().select(), "test")

    def test_dialog_uses_its_owning_amc_process(self):
        router = routing.ProfileRouter(
            None,
            window_snapshot=lambda: (23, [
                (23, "Ustawienia"),
                (23, "AMC Public — 10.11.2026, 15:01"),
            ]),
            available=lambda profile: True,
        )
        self.assertEqual(router.select(), "public")

    def test_ambiguous_or_stopped_target_fails_closed(self):
        outside = lambda: (99, [(99, "Edytor")])
        both = routing.ProfileRouter(None, window_snapshot=outside,
                                     available=lambda profile: True)
        with self.assertRaisesRegex(transport.BridgeError, "Wybierz okno"):
            both.select()
        none = routing.ProfileRouter(None, window_snapshot=outside,
                                     available=lambda profile: False)
        with self.assertRaisesRegex(transport.BridgeError, "Uruchom"):
            none.select()
        one = routing.ProfileRouter(None, window_snapshot=outside,
                                    available=lambda profile: profile == "public")
        self.assertEqual(one.select(), "public")
        one._available = lambda profile: profile == "test"
        with self.assertRaisesRegex(transport.BridgeError, "Ostatnio używany AMC Public"):
            one.select()
        unknown = routing.ProfileRouter(None,
            window_snapshot=lambda: (15, [(15, "AMC — dialog")]),
            available=lambda profile: True)
        with self.assertRaisesRegex(transport.BridgeError, "Nie można rozpoznać"):
            unknown.select()

    def test_queued_command_keeps_profile_when_focus_changes(self):
        entered = threading.Event()
        release = threading.Event()
        done = threading.Event()
        calls = []

        def exchange(item):
            calls.append(item)
            if len(calls) == 1:
                entered.set()
                release.wait(2)
            return {"message": "OK"}

        controller = worker.CommandWorker(exchange, lambda message: done.set())
        try:
            self.assertTrue(controller.submit("playPause", target="test"))
            self.assertTrue(entered.wait(1))
            self.assertTrue(controller.submit("playPause", target="public"))
            release.set()
            for _ in range(40):
                if len(calls) == 2:
                    break
                done.wait(.05)
            self.assertEqual(calls, [("test", "playPause"), ("public", "playPause")])
        finally:
            release.set()
            controller.close()


if __name__ == "__main__":
    unittest.main()
