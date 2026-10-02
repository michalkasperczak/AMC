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
import os
import sys
import traceback
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

        for name in sorted(vars(module)):
            if not name.startswith("test_"):
                continue
            func = getattr(module, name)
            if not callable(func):
                continue
            label = f"{path.stem}::{name}"
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
