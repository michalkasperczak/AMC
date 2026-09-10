import importlib.util
import ast
import json
from pathlib import Path
import subprocess
import threading
import time
import unittest
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
    def test_scripts_are_labelled_and_do_not_steal_gestures(self):
        tree = ast.parse((PLUGIN / "__init__.py").read_text(encoding="utf-8"))
        scripts = [node for node in ast.walk(tree)
                   if isinstance(node, ast.FunctionDef) and node.name.startswith("script_")]
        self.assertEqual(len(scripts), len(transport.COMMANDS))
        for method in scripts:
            decorator = method.decorator_list[0]
            self.assertEqual(decorator.func.id, "script")
            self.assertEqual([keyword.arg for keyword in decorator.keywords], ["description"])
            self.assertGreater(len(decorator.keywords[0].value.value), 12)

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
