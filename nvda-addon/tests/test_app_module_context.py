"""The NVDA line-reading gesture must remain native in AMC dialogs."""
import importlib.util
from pathlib import Path
from types import SimpleNamespace
import sys
import unittest
from unittest.mock import patch


class BaseModule:
    def getScript(self, gesture):
        return self.script_amcNowPlaying


class ContextTests(unittest.TestCase):
    def setUp(self):
        self.focus = SimpleNamespace(treeInterceptor=None, role='listitem')
        self.foreground = SimpleNamespace(name='Utwór — Spotify — AMC 0.1.0-alpha.393')
        modules = {
            'appModuleHandler': SimpleNamespace(AppModule=BaseModule),
            'api': SimpleNamespace(getFocusObject=lambda: self.focus,
                                   getForegroundObject=lambda: self.foreground),
            'controlTypes': SimpleNamespace(Role=SimpleNamespace(EDITABLETEXT='edit', COMBOBOX='combo', MENUITEM='menuitem', POPUPMENU='popupmenu', MENUBAR='menubar')),
            'ui': SimpleNamespace(message=lambda text: None),
            'wx': SimpleNamespace(CallAfter=lambda *args: None),
            'scriptHandler': SimpleNamespace(script=lambda **kwargs: lambda fn: fn),
            'globalPlugins.amcController.transport': SimpleNamespace(BridgeError=Exception, exchange=lambda command: {}),
        }
        path = Path(__file__).resolve().parents[1]/'addon/appModules/accessiblemediacontroller.py'
        with patch.dict(sys.modules, modules):
            spec = importlib.util.spec_from_file_location('context_under_test', path)
            self.module = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(self.module)
        self.app = self.module.AppModule()

    def test_main_list_keeps_now_playing(self):
        self.assertIsNotNone(self.app.getScript(object()))

    def test_properties_do_not_override_read_line(self):
        self.foreground.name = 'Właściwości i informacje'
        self.assertIsNone(self.app.getScript(object()))

    def test_html_document_keeps_reader_keys(self):
        self.focus.treeInterceptor = object()
        self.assertIsNone(self.app.getScript(object()))

    def test_search_edit_keeps_reader_keys(self):
        self.focus.role = 'edit'
        self.assertIsNone(self.app.getScript(object()))

    def test_unknown_context_fails_safe(self):
        self.foreground = None
        self.assertIsNone(self.app.getScript(object()))
