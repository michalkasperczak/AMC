"""AMC must not install an appModule or bind native NVDA gestures."""
import ast
from pathlib import Path
import unittest

ADDON = Path(__file__).resolve().parents[1] / 'addon'


def declared_gestures(root):
    found = []
    for path in root.rglob('*.py'):
        tree = ast.parse(path.read_text(encoding='utf-8'))
        for node in ast.walk(tree):
            if isinstance(node, ast.Call):
                for kw in node.keywords:
                    if kw.arg == 'gesture':
                        found.append((path.name, ast.literal_eval(kw.value)))
                    elif kw.arg == 'gestures':
                        found.extend((path.name, item) for item in ast.literal_eval(kw.value))
            elif isinstance(node, ast.Assign) and any(
                    isinstance(target, ast.Name) and target.id.endswith('__gestures')
                    for target in node.targets):
                found.extend((path.name, item) for item in ast.literal_eval(node.value))
    return found


class GestureScopeTests(unittest.TestCase):
    def test_no_amc_app_module(self):
        self.assertFalse((ADDON / 'appModules/accessiblemediacontroller.py').exists(),
                         'AMC must leave native NVDA commands to the reader')

    def test_all_declared_gestures_require_control_and_windows(self):
        gestures = declared_gestures(ADDON)
        self.assertTrue(gestures, 'No scripts examined')
        for file, gesture in gestures:
            with self.subTest(file=file, gesture=gesture):
                chord = gesture.split(':', 1)[1].lower().split('+')
                self.assertIn('control', chord)
                self.assertIn('windows', chord)
                self.assertNotIn('nvda', chord)


if __name__ == '__main__':
    unittest.main()
