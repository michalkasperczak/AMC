"""Targeted contract for AMC's NVDA status-bar lookup."""

import importlib.util
from pathlib import Path
import sys
from types import ModuleType, SimpleNamespace
import unittest
from unittest.mock import patch


MODULE = Path(__file__).resolve().parents[1] / "addon" / "appModules" / "python.py"
STATUSBAR = "statusbar"


def load_module(foreground):
    api = ModuleType("api")
    api.getForegroundObject = lambda: foreground
    handler = ModuleType("appModuleHandler")
    handler.AppModule = type("BaseAppModule", (), {})
    controls = ModuleType("controlTypes")
    controls.Role = SimpleNamespace(STATUSBAR=STATUSBAR)
    spec = importlib.util.spec_from_file_location("amc_python_status_bar", MODULE)
    module = importlib.util.module_from_spec(spec)
    with patch.dict(sys.modules, {
        "api": api, "appModuleHandler": handler, "controlTypes": controls,
    }):
        spec.loader.exec_module(module)
    return module.AppModule()


class StatusBarAppModuleTests(unittest.TestCase):
    def test_finds_bar_in_both_profiles_with_list_focus(self):
        for profile in ("AMC Test", "AMC Public"):
            with self.subTest(profile=profile):
                media_list = SimpleNamespace(role="list", children=[object()] * 1000)
                status_bar = SimpleNamespace(role=STATUSBAR, name="")
                foreground = SimpleNamespace(
                    name=f"{profile} — 10.11.2026, 02:39 — Utwór",
                    children=[media_list, status_bar],
                )
                self.assertIs(load_module(foreground)._get_statusBar(), status_bar)

    def test_unrelated_python_window_uses_nvda_default(self):
        foreground = SimpleNamespace(
            name="Edytor Python", children=[SimpleNamespace(role=STATUSBAR)],
        )
        with self.assertRaises(NotImplementedError):
            load_module(foreground)._get_statusBar()

    def test_missing_bar_uses_nvda_default(self):
        foreground = SimpleNamespace(
            name="AMC Test — 10.11.2026, 02:39", children=[SimpleNamespace(role="list")],
        )
        with self.assertRaises(NotImplementedError):
            load_module(foreground)._get_statusBar()


if __name__ == "__main__":
    unittest.main()
