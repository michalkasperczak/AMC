#!/usr/bin/env python3
"""Uruchamiacz testow AMC-wx-Lite na samej bibliotece standardowej.

Dlaczego nie pytest: instalacja z sieci na tej maszynie idzie ~2 KB/s, a testy
maja dzialac TERAZ i w kazdym srodowisku (takze w prywatnym runtime 3.14.7 bez
pipa). Zbieramy funkcje ``test_*`` z plikow ``tests/test_*.py``, tak samo jak
pytest, wiec przejscie na pytest nie wymaga zmian w testach.

Uzycie:  python3 run_tests.py [fragment_nazwy ...]
"""

from __future__ import annotations

import importlib.util
import inspect
import os
import sys
import tempfile
import traceback
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent


def load_module(path: Path):
    spec = importlib.util.spec_from_file_location(path.stem, path)
    if spec is None or spec.loader is None:
        raise ImportError(f"nie moge wczytac {path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[path.stem] = module
    spec.loader.exec_module(module)
    return module


def collect(module, stem: str) -> list[tuple[str, object]]:
    """Zbiera testy: funkcje ``test_*`` ORAZ metody klas ``unittest.TestCase``.

    Wczesniej runner widzial tylko funkcje modulowe. Klasy ``TestCase`` byly
    po cichu POMIJANE -- nowy plik testow mogl wiec "przejsc" bez wykonania ani
    jednego sprawdzenia. To jest dokladnie falszywa zielen, wiec runner musi
    umiec jedno i drugie.
    """
    found: list[tuple[str, object]] = []
    for name in sorted(vars(module)):
        value = getattr(module, name)
        if name.startswith("test_") and callable(value) and not isinstance(value, type):
            found.append((f"{stem}::{name}", _function_runner(value)))
            continue
        if isinstance(value, type) and issubclass(value, unittest.TestCase):
            for method in sorted(dir(value)):
                if not method.startswith("test_"):
                    continue
                found.append((f"{stem}::{name}.{method}", _case_runner(value, method)))
    return found


def _function_runner(function):
    """Minimalna obsluga standardowego fixture ``tmp_path`` z pytest.

    Runner obiecuje uruchamiac te same funkcje ``test_*`` bez instalowania
    pytest. Dwie funkcje uzywaja tylko jego najprostszego fixture; przekazanie
    bezpiecznego katalogu tymczasowego jest lepsze niz ciche pomijanie albo
    staly czerwony wynik calego zestawu.
    """
    parameters = tuple(inspect.signature(function).parameters)
    if not parameters:
        return function
    if parameters != ("tmp_path",):
        def unsupported() -> None:
            raise TypeError(
                f"Nieobslugiwane argumenty testu {function.__name__}: "
                + ", ".join(parameters)
            )
        return unsupported

    def run_with_tmp_path() -> None:
        with tempfile.TemporaryDirectory(prefix="amc-wx-test-") as directory:
            function(Path(directory))

    return run_with_tmp_path


def _case_runner(case: type, method: str):
    """Jeden przebieg ``TestCase`` z setUp/tearDown i mapowaniem SkipTest."""

    def run() -> None:
        suite = unittest.TestLoader().loadTestsFromNames([method], case)
        result = unittest.TestResult()
        suite.run(result)
        if result.skipped:
            raise unittest.SkipTest(result.skipped[0][1])
        if result.failures:
            raise AssertionError(result.failures[0][1])
        if result.errors:
            raise AssertionError(result.errors[0][1])

    return run


def main(argv: list[str]) -> int:
    sys.path.insert(0, str(ROOT))
    filters = [arg for arg in argv if not arg.startswith("-")]

    passed: list[str] = []
    failures: list[tuple[str, str]] = []
    skipped: list[tuple[str, str]] = []

    for path in sorted((ROOT / "tests").glob("test_*.py")):
        try:
            module = load_module(path)
        except Exception:
            failures.append((f"{path.name} (import)", traceback.format_exc()))
            continue

        for label, func in sorted(collect(module, path.stem)):
            if filters and not any(f in label for f in filters):
                continue
            try:
                func()
            except Exception as error:
                # Test moze jawnie zglosic, ze warunku nie da sie spelnic
                # (np. brak zbudowanego serwera protokolu). POMINIETY nigdy
                # nie jest liczony jako zdany - inaczej brak buildu udawalby
                # zielony wynik.
                if type(error).__name__ == "SkipTest":
                    skipped.append((label, str(error)))
                    print(f"POMIN {label}: {error}")
                    continue
                failures.append((label, traceback.format_exc()))
                print(f"PADL  {label}")
            else:
                passed.append(label)
                print(f"OK    {label}")

    print()
    for label, text in failures:
        print("=" * 70)
        print(label)
        print(text)

    print(f"Zdane: {len(passed)}, padniete: {len(failures)}, pominiete: {len(skipped)}")
    for label, reason in skipped:
        print(f"  POMINIETY {label}: {reason}")
    return 1 if failures else 0


if __name__ == "__main__":
    os.environ.setdefault("AMC_WX_LITE_TESTS", "1")
    sys.exit(main(sys.argv[1:]))
