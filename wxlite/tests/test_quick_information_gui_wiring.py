"""Czy LEWA STRZALKA faktycznie DOCHODZI do obslugi w oknie.

Po co osobny plik: ``resolve`` moze juz oddawac ``Action.QUICK_INFORMATION``, a
klawisz nadal MILCZEC, bo ``_dispatch`` nie ma dla niego galezi. To dokladnie
ten objaw, ktory naprawiamy, wiec musi byc zmierzony, a nie zalozony.

Czym mierzymy: wxPython nie da sie zaimportowac w tym srodowisku (WSL, brak
pulpitu), wiec okna NIE budujemy. Zamiast tego czytamy DRZEWO SKLADNIOWE
``gui.py``: sprawdzamy istnienie galezi i realne wywolanie metody. To slabsze
niz zywy klawisz (odbior z zywym NVDA jest osobnym krokiem), ale mocniejsze od szukania
napisu w pliku -- komentarz albo martwy kod testu nie przejdzie, bo pytamy o
WEZLY ``elif`` i ``Call``, nie o tekst.
"""

from __future__ import annotations

import ast
import unittest
from pathlib import Path

GUI = Path(__file__).resolve().parents[1] / "amc_wx_lite" / "gui.py"
HANDLER = "_announce_quick_information"


def _frame_class() -> ast.ClassDef:
    tree = ast.parse(GUI.read_text(encoding="utf-8"))
    for node in ast.walk(tree):
        if isinstance(node, ast.ClassDef) and node.name == "LiteFrame":
            return node
    raise AssertionError("nie ma klasy LiteFrame w gui.py")


def _method(name: str) -> ast.FunctionDef:
    for node in _frame_class().body:
        if isinstance(node, ast.FunctionDef) and node.name == name:
            return node
    raise AssertionError(f"LiteFrame nie ma metody {name}")


def _quick_info_branch_calls() -> list[str]:
    """Nazwy metod wolanych w galezi ``action is Action.QUICK_INFORMATION``."""
    dispatch = _method("_dispatch")
    found: list[str] = []
    for node in ast.walk(dispatch):
        if not isinstance(node, ast.If):
            continue
        test = node.test
        if not isinstance(test, ast.Compare) or not test.comparators:
            continue
        right = test.comparators[0]
        if not (isinstance(right, ast.Attribute) and right.attr == "QUICK_INFORMATION"):
            continue
        for inner in ast.walk(ast.Module(body=node.body, type_ignores=[])):
            if isinstance(inner, ast.Call) and isinstance(inner.func, ast.Attribute):
                found.append(inner.func.attr)
    return found


class LeftArrowReachesTheHandler(unittest.TestCase):
    def test_dispatch_ma_galez_dla_krotkiej_informacji(self):
        self.assertIn(
            HANDLER,
            _quick_info_branch_calls(),
            "resolver oddaje akcje, ale okno jej NIE obsluguje -- klawisz dalej milczy",
        )

    def test_handler_istnieje_jako_metoda_okna(self):
        method = _method(HANDLER)
        self.assertEqual(method.name, HANDLER)

    def test_handler_pyta_host_poza_watkiem_interfejsu(self):
        """Pomiar ma 5 s (plik) i 6 s (strumien) budzetu -- w watku GUI
        zamrozilby okno, czyli i czytnik. Musi przejsc przez ``runner``."""
        calls = [
            node.func.attr
            for node in ast.walk(_method(HANDLER))
            if isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute)
        ]
        self.assertIn("submit", calls, "brak zlecenia do watku: okno zamarznie na pomiar")

    def test_handler_sprawdza_bramke_po_powrocie(self):
        """Bez ``quick_info_reply`` wynik moglby opisac CUDZY wiersz."""
        names = [
            node.func.id
            for node in ast.walk(_method(HANDLER))
            if isinstance(node, ast.Call) and isinstance(node.func, ast.Name)
        ]
        self.assertIn("quick_info_plan", names)
        self.assertIn("quick_info_reply", names)
        self.assertIn("quick_info_failure", names, "spozniony BLAD tez idzie przez bramke")


if __name__ == "__main__":
    unittest.main()
