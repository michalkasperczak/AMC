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
APP_MODULE = ROOT / "nvda-addon/addon/appModules/accessiblemediacontroller.py"

# Odczyt diagnostyczny potoku, bez skryptu ani gestu w dodatku.
# Polecenia samego NVDA pozostaja w czytniku, nie w appModule AMC.
DIAGNOSTIC_COMMANDS = frozenset(("nowPlaying",))


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
            "sessionPrevious": ["shift+tab", "shift+leftArrow"],
            "sessionNext": ["tab", "shift+rightArrow"],
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
            "refreshPodcastLibrary": "f5",
        }
        expected.update({f"preset{slot}": "alt+" + key
                         for slot, key in enumerate("1234567890-=", 1)})
        tree = ast.parse((PLUGIN / "__init__.py").read_text(encoding="utf-8"))
        scripts = [node for node in ast.walk(tree)
                   if isinstance(node, ast.FunctionDef) and node.name.startswith("script_")]
        # nowPlaying pozostaje dostepne diagnostyce potoku, ale dodatek
        # nie ma dla niego gestu: natywne polecenia NVDA nie sa przejmowane.
        self.assertEqual(len(scripts), len(transport.COMMANDS) - len(DIAGNOSTIC_COMMANDS))
        gestures = set()
        for method in scripts:
            decorator = method.decorator_list[0]
            self.assertEqual(decorator.func.id, "script")
            properties = {}
            for keyword in decorator.keywords:
                if isinstance(keyword.value, ast.List):
                    properties[keyword.arg] = [element.value for element in keyword.value.elts]
                else:
                    properties[keyword.arg] = keyword.value.value
            self.assertGreater(len(properties["description"]), 12)
            command = method.name.removeprefix("script_")
            # JEDNA KOMENDA MOZE MIEC KILKA GESTOW.  NVDA przyjmuje albo
            # gesture= (jeden), albo gestures= (lista) - patrz scriptHandler.py
            # w zrodlach NVDA.  Przelaczanie sesji ma dwa warianty: historyczny
            # Tab i wygodniejsze jedna reka strzalki, wiec test musi znac OBA
            # ksztalty, inaczej kazdy drugi gest bylby niewidoczny dla kontroli
            # unikalnosci i kolizja przeszlaby niezauwazona.
            expected_gestures = expected[command]
            if not isinstance(expected_gestures, list):
                expected_gestures = [expected_gestures]
            declared = properties.get("gestures")
            if declared is None:
                declared = [properties["gesture"]]
                self.assertEqual(len(expected_gestures), 1,
                                 f"{command}: oczekiwano kilku gestow, a wtyczka deklaruje jeden")
            else:
                self.assertNotIn("gesture", properties,
                                 f"{command}: gesture= i gestures= naraz - NVDA wezmie tylko jeden")
            self.assertEqual(declared,
                             ["kb:control+windows+" + suffix for suffix in expected_gestures])
            for gesture in declared:
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
        # 68 = 65 wyjsciowych + 2 drugie gesty przelaczania sesji
        # (strzalki obok Tab) + 1 odswiezenie biblioteki podcastow (F5).
        self.assertEqual(len(gestures), 68)
        for digit in "0123456789":
            self.assertNotIn("kb:control+windows+shift+" + digit, gestures)

    def test_native_nvda_gestures_are_not_overridden(self):
        self.assertFalse(APP_MODULE.exists(), "Dodatek nie instaluje appModule AMC")
        globalny = (PLUGIN / "__init__.py").read_text(encoding="utf-8").lower()
        self.assertNotIn("nvda+uparrow", globalny)
        self.assertNotIn("insert+uparrow", globalny)
        self.assertNotIn("script_nowplaying", globalny)
        self.assertIn("nowPlaying", transport.COMMANDS)

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

    def test_held_volume_key_is_coalesced_instead_of_refused(self):
        """Trzymany Ctrl+Windows+strzalka w gore nie moze dawac
        "poczekaj na poprzednie polecenie" ani gubic krokow glosnosci."""
        entered, release = threading.Event(), threading.Event()
        calls, results = [], []
        finished = threading.Event()

        def exchange(command):
            calls.append(command)
            entered.set()
            release.wait(3)
            return {"message": "Głośność %d%%" % (30 + 5 * len(calls))}

        def deliver(message):
            results.append(message)
            finished.set()

        controller = worker.CommandWorker(exchange, deliver)
        try:
            self.assertTrue(controller.submit("volumeUp"))
            self.assertTrue(entered.wait(1))
            # Pierwsze polecenie wisi w wymianie; kolejnych dwanascie to
            # autopowtarzanie klawisza.  Kolejka ma miejsce na 4 wpisy, wiec
            # bez scalania juz piate zwrocilo by False.
            for _ in range(12):
                self.assertTrue(controller.submit("volumeUp"))
            self.assertLessEqual(controller._queue.qsize(), 1)
            release.set()
            self.assertTrue(finished.wait(2))
            for _ in range(20):
                if len(calls) >= 13:
                    break
                time.sleep(.02)
            # Kazdy krok glosnosci wykonany.  Wypowiedzi jest tyle, ile PARTII
            # scalonych (tu dwie: polecenie juz w wymianie i zebrane
            # autopowtorzenia), nigdy tyle, ile nacisniec - i ostatnia podaje
            # stan koncowy, a nie stan sprzed powtorzen.
            self.assertEqual(calls, ["volumeUp"] * 13)
            self.assertLessEqual(len(results), 3)
            self.assertEqual(results[-1], "Głośność 95%")
        finally:
            release.set()
            controller.close()

    def test_coalescing_has_an_upper_bound_and_spares_other_commands(self):
        release = threading.Event()
        calls = []

        def exchange(command):
            calls.append(command)
            release.wait(3)
            return {"message": ""}

        controller = worker.CommandWorker(exchange, lambda message: None)
        try:
            for _ in range(worker.MAX_REPEAT + 30):
                controller.submit("seekForward")
            with controller._gate:
                waiting = controller._repeatable.get("seekForward")
            self.assertIsNotNone(waiting)
            self.assertLessEqual(waiting.count, worker.MAX_REPEAT)
            # Polecenia jednorazowe (przelaczniki, widoki) NIE moga sie scalac -
            # dwa nacisniecia playPause to dwa przelaczenia, nie jedno.
            self.assertNotIn("playPause", worker.REPEATABLE)
            self.assertNotIn("recordToggle", worker.REPEATABLE)
            self.assertNotIn("next", worker.REPEATABLE)
            for command in worker.REPEATABLE:
                self.assertIn(command, transport.COMMANDS)
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
        # Katalog docelowy zalezy od wersji Windows SDK (net8.0-windows,
        # net8.0-windows10.0.19041.0...). Sztywna sciezka cicho brala STARY
        # plik i test klamal o obslugiwanych poleceniach - bierzemy najnowszy.
        builds = sorted(
            (ROOT / "tests/AccessibleMediaController.Windows.SmokeTests/bin/Release").glob(
                "net8.0-windows*/AccessibleMediaController.Windows.SmokeTests.exe"),
            key=lambda path: path.stat().st_mtime,
            reverse=True)
        self.assertTrue(builds, "Brak zbudowanego hosta testowego - najpierw dotnet build")
        executable = builds[0]
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
