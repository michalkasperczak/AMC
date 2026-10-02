"""Uruchomienie AMC-wx-Lite.

Uzycie:
    python -m amc_wx_lite            # okno (wymaga wxPython i pulpitu)
    python -m amc_wx_lite --sprawdz  # sama diagnostyka, BEZ okna

``--sprawdz`` jest po to, zeby dalo sie potwierdzic srodowisko (wxPython,
silnik, folder stanu) bez otwierania okna - przydaje sie przy pierwszym
uruchomieniu i w zdalnej sesji bez pulpitu.
"""

from __future__ import annotations

import sys
from pathlib import Path


def diagnose() -> int:
    """Wypisz stan srodowiska. Zwraca 0, gdy da sie uruchomic okno."""
    problems: list[str] = []

    print(f"Python: {sys.version.split()[0]}  ({sys.executable})")

    try:
        import wx  # noqa: PLC0415
    except Exception as error:  # noqa: BLE001
        print(f"wxPython: BRAK ({error})")
        problems.append("wxPython nie jest dostepne")
    else:
        version = getattr(wx, "__version__", "?")
        print(f"wxPython: {version}")
        app = wx.GetApp()
        print(f"  wx.GetApp(): {app!r}")

    from .host_client import default_host_path  # noqa: PLC0415
    from .state_store import default_state_dir  # noqa: PLC0415

    host = default_host_path()
    if host.exists():
        print(f"Silnik: {host} ({host.stat().st_size} B)")
    else:
        print(f"Silnik: BRAK w {host}")
        problems.append("nie znaleziono amc_lite_host.exe")

    state_dir = default_state_dir()
    print(f"Prywatny stan: {state_dir}")
    if "AccessibleMediaController" in str(state_dir):
        problems.append("UWAGA: folder stanu koliduje z pelnym AMC")

    # Logika, ktora dziala wszedzie - potwierdza, ze pakiet jest spojny.
    from .shortcuts import describe  # noqa: PLC0415

    print(f"Skroty w pomocy: {len(describe())}")

    if problems:
        print("\nDo zrobienia przed uruchomieniem okna:")
        for problem in problems:
            print(f"  - {problem}")
        return 1
    print("\nSrodowisko gotowe.")
    return 0


def main(argv: list[str] | None = None) -> int:
    argv = list(sys.argv[1:] if argv is None else argv)
    if "--sprawdz" in argv or "--check" in argv:
        return diagnose()

    try:
        from .gui import main as gui_main  # noqa: PLC0415
    except Exception as error:  # noqa: BLE001
        print(f"Nie moge zaladowac interfejsu: {error}", file=sys.stderr)
        print("Sprawdz srodowisko:  python -m amc_wx_lite --sprawdz", file=sys.stderr)
        return 2
    return gui_main()


if __name__ == "__main__":
    raise SystemExit(main())
