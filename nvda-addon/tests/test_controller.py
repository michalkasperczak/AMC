import importlib.util
import ast
import json
from pathlib import Path
import subprocess
import threading
import time
import unittest
from unittest.mock import patch
import uuid

ROOT = Path(__file__).resolve().parents[2]
PLUGIN = ROOT / "nvda-addon/addon/globalPlugins/amcController"


def load(name):
    spec = importlib.util.spec_from_file_location(name, PLUGIN / (name + ".py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


transport = load("transport")
worker = load("worker")


class ControllerTests(unittest.TestCase):
    def test_scripts_have_approved_unique_default_gestures(self):
        expected = {
            "status": "i", "playPause": "p", "previous": "leftArrow", "next": "rightArrow",
            "volumeUp": "upArrow", "volumeDown": "downArrow", "mute": "m",
            "seekBack": "j", "seekForward": "k", "elapsed": "e", "remaining": "r", "total": "t",
            "sessionPrevious": "shift+tab", "sessionNext": "tab",
            "context": "alt+i",
            "muteAll": "shift+m",
            "seekBack30": "shift+j",
            "seekForward30": "shift+k",
            "seekBack60": "alt+j",
            "seekForward60": "alt+k",
            "rateDown": ",",
            "rateUp": ".",
            "rateReset": "shift+.",
            "trackStart": "home",
            "trackEnd": "end",
            "addBookmark": "b",
            "previousBookmark": "shift+pageUp",
            "nextBookmark": "shift+pageDown",
            "previousChapter": "alt+shift+pageUp",
            "nextChapter": "alt+shift+pageDown",
            "presetPrevious": "alt+pageUp",
            "presetNext": "alt+pageDown",
            "favorite": "shift+u",
            "queue": "shift+q",
            "recordToggle": "alt+r",
            "recordPause": "shift+r",
            "recordSplit": "shift+t",
            "showPlayer": "f6",
            "showLibrary": "l",
            "showFavorites": "u",
            "showQueue": "alt+q",
            "showPlaylists": "shift+p",
            "showHistory": "h",
            "showPresets": "alt+p",
            "showBookmarks": "alt+b",
            "showChapters": "alt+c",
            "showSessions": "shift+s",
            "showAudioOutput": "a",
            "showSearch": "alt+f",
            "showCommands": "f2",
            "showRecordings": "alt+h",
            "showSchedules": "shift+h",
            "showRecognitions": "alt+s",
        }
        expected.update({f"preset{slot}": "alt+" + key
                         for slot, key in enumerate("1234567890-=", 1)})
        tree = ast.parse((PLUGIN / "__init__.py").read_text(encoding="utf-8"))
        scripts = [node for node in ast.walk(tree)
                   if isinstance(node, ast.FunctionDef) and node.name.startswith("script_")]
        self.assertEqual(len(scripts), len(transport.COMMANDS))
        gestures = set()
        for method in scripts:
            decorator = method.decorator_list[0]
            self.assertEqual(decorator.func.id, "script")
            properties = {keyword.arg: keyword.value.value for keyword in decorator.keywords}
            self.assertGreater(len(properties["description"]), 12)
            command = method.name.removeprefix("script_")
            gesture = properties["gesture"]
            self.assertEqual(gesture, "kb:control+windows+" + expected[command])
            self.assertNotIn(gesture.lower(), gestures)
            gestures.add(gesture.lower())
            # The public NVDA script must dispatch the matching allow-listed command.
            call = method.body[0].value
            self.assertEqual(call.func.attr, "_send")
            self.assertEqual(call.args[0].value, command)
        self.assertNotIn("kb:control+windows+enter", gestures)
        for reserved in ("c", "d", "f", "n", "o", "q", "s", "v", "space",
                         "shift+b", "f4", *map(str, range(10))):
            self.assertNotIn("kb:control+windows+" + reserved, gestures)
        self.assertEqual(len(gestures), 65)
        for digit in "0123456789":
            self.assertNotIn("kb:control+windows+shift+" + digit, gestures)

    def test_foreground_permission_is_only_for_explicit_ui_commands(self):
        self.assertEqual(transport.FOREGROUND_COMMANDS,
                         {command for command in transport.COMMANDS if command.startswith("show")}
                         | transport.PRESET_COMMANDS)
        granted = []

        class Function:
            def __init__(self, callback):
                self.callback = callback
            def __call__(self, *args):
                return self.callback(*args)

        class Kernel:
            pass

        class User:
            pass

        kernel, user = Kernel(), User()
        def server_pid(handle, pointer):
            self.assertEqual(handle, 123)
            pointer._obj.value = 456
            return True
        kernel.GetNamedPipeServerProcessId = Function(server_pid)
        user.AllowSetForegroundWindow = Function(lambda pid: granted.append(pid) or True)
        with patch.object(transport.ctypes, "WinDLL", return_value=user):
            transport.allow_foreground(kernel, 123)
        self.assertEqual(granted, [456])  # Never ASFW_ANY.
        kernel.GetNamedPipeServerProcessId = Function(lambda *_: False)
        transport.allow_foreground(kernel, 123)
        self.assertEqual(granted, [456])
        transport.allow_foreground(object(), 123)  # Optional API failure cannot break transport.

    def test_reply_schema_and_labels(self):
        good = {"version": 1, "ok": True, "message": "Żółć 35%"}
        self.assertEqual(transport.parse_reply(json.dumps(good).encode()), good)
        for value in ([], {}, {**good, "version": True}, {**good, "message": {}},
                      {**good, "message": "a" * 2001}, {**good, "ok": 1}):
            with self.assertRaises(transport.BridgeError):
                transport.parse_reply(json.dumps(value).encode())

    def test_absent_program_and_unknown_command(self):
        start = time.monotonic()
        with self.assertRaises(transport.BridgeError):
            transport.exchange("status", "AMC.test.absent." + uuid.uuid4().hex)
        self.assertLess(time.monotonic() - start, 1)
        with self.assertRaises(transport.BridgeError):
            transport.exchange("shell")

    def test_worker_does_not_block_or_retry(self):
        entered, release, done = threading.Event(), threading.Event(), threading.Event()
        calls, results = [], []

        def exchange(command):
            calls.append(command)
            entered.set()
            release.wait(3)
            raise transport.BridgeError("Brak odpowiedzi")

        def deliver(value):
            results.append(value)
            done.set()

        controller = worker.CommandWorker(exchange, deliver)
        try:
            start = time.monotonic()
            self.assertTrue(controller.submit("playPause"))
            self.assertLess(time.monotonic() - start, .1)
            self.assertTrue(entered.wait(1))
            for _ in range(4):
                self.assertTrue(controller.submit("next"))
            self.assertFalse(controller.submit("next"))
            start = time.monotonic()
            controller.close()
            self.assertLess(time.monotonic() - start, .1)
            release.set()
            controller._thread.join(1)
            self.assertEqual(calls, ["playPause"])
            self.assertEqual(results, [])
        finally:
            release.set()
            controller.close()

    def test_failed_toggle_is_sent_only_once(self):
        calls, responses = [], []
        done = threading.Event()

        def exchange(command):
            calls.append(command)
            raise transport.BridgeError("Brak odpowiedzi")

        def deliver(message):
            responses.append(message)
            done.set()

        controller = worker.CommandWorker(exchange, deliver)
        try:
            controller.submit("playPause")
            self.assertTrue(done.wait(1))
            self.assertEqual(calls, ["playPause"])
            self.assertIsInstance(responses[0], transport.BridgeError)
        finally:
            controller.close()

    def test_python_dotnet_interop(self):
        executable = ROOT / "tests/AccessibleMediaController.Windows.SmokeTests/bin/Release/net8.0-windows/AccessibleMediaController.Windows.SmokeTests.exe"
        name = "AMC.NVDA.interop." + uuid.uuid4().hex
        host = subprocess.Popen([str(executable), "--nvda-interop-host", name],
                                stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        try:
            self.assertEqual(host.stdout.readline().strip(), b"READY")
            for command in sorted(transport.COMMANDS):
                reply = transport.exchange(command, name)
                self.assertTrue(reply["ok"])
                self.assertEqual(reply["message"], "Test połączenia: " + command)
        finally:
            host.communicate(b"\n", timeout=5)
            self.assertEqual(host.returncode, 0)


if __name__ == "__main__":
    unittest.main()
