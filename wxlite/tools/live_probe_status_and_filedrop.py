"""WASKA proba LIVE dla rodzica -- jedyna rzecz, ktorej nie da sie zmierzyc w WSL.

CZEGO TA PROBA NIE ROBI
-----------------------
Nie powtarza niczego, co juz jest zielone bez pulpitu (726 testow w
``wxlite/run_tests.py``). Mierzy DOKLADNIE dwie rzeczy, ktore wymagaja
prawdziwego okna Windows i prawdziwego schowka:

  A. czy okno MA natywny pasek stanu (klasa ``msctls_statusbar32``) i czy
     siedzi w nim aktualny tekst statusu -- to jest to, czego szuka
     ``NVDA+End``;
  B. czy po ``Ctrl+Shift+C`` w schowku Windows lezy format ``CF_HDROP``
     (lista plikow) z wlasciwa sciezka, a nie sam tekst.

URUCHOMIENIE (na pulpicie Windows, w srodowisku z wxPython)
-----------------------------------------------------------
    python wxlite\\tools\\live_probe_status_and_filedrop.py

Skrypt sam tworzy okno, sam wola ``_copy_address`` na przygotowanym pliku
tymczasowym i sam sie zamyka. NIE klika w zadnym cudzym programie i niczego
nie instaluje. Wypisuje JSON z werdyktem; kod wyjscia 0 tylko wtedy, gdy obie
proby wyszly.

CZEGO SKRYPT NIE DOWODZI -- do zrobienia recznie przez rodzica
--------------------------------------------------------------
  * ze NVDA faktycznie WYMAWIA pasek stanu po ``NVDA+End`` (i po ``F6``
    w widoku odtwarzacza). Skrypt pokazuje tylko, ze pasek ISTNIEJE jako
    natywna kontrolka z trescia -- odbioru czytnika nie mierzy.
  * ze wklejenie w Total Commanderze tworzy kopie. Skrypt pokazuje tylko,
    ze w schowku jest ``CF_HDROP`` ze wskazana sciezka.
Te dwa kroki zostaja dla czlowieka przy zywym NVDA.
"""

from __future__ import annotations

import ctypes
import json
import os
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

import wx  # noqa: E402

from amc_wx_lite import gui  # noqa: E402

CF_HDROP = 15


def native_class_name(hwnd: int) -> str:
    buffer = ctypes.create_unicode_buffer(256)
    ctypes.windll.user32.GetClassNameW(hwnd, buffer, 256)
    return buffer.value


def read_clipboard_file_drop() -> list[str]:
    """Czyta ``CF_HDROP`` wprost z Win32 -- nie przez wx, zeby nie badac wx soba."""
    user32 = ctypes.windll.user32
    shell32 = ctypes.windll.shell32
    if not user32.OpenClipboard(0):
        raise RuntimeError("nie moge otworzyc schowka")
    try:
        if not user32.IsClipboardFormatAvailable(CF_HDROP):
            return []
        handle = user32.GetClipboardData(CF_HDROP)
        if not handle:
            return []
        count = shell32.DragQueryFileW(handle, 0xFFFFFFFF, None, 0)
        files = []
        for index in range(count):
            length = shell32.DragQueryFileW(handle, index, None, 0)
            buffer = ctypes.create_unicode_buffer(length + 1)
            shell32.DragQueryFileW(handle, index, buffer, length + 1)
            files.append(buffer.value)
        return files
    finally:
        user32.CloseClipboard()


def main() -> int:
    verdict: dict = {"status_bar": {}, "file_drop": {}}
    app = wx.App()

    frame = wx.Frame(None, title="AMC proba -- Uspokojenie wieczorne")
    frame.CreateStatusBar(1, style=wx.STB_DEFAULT_STYLE)
    bar = gui.NativeStatusBar(frame.GetStatusBar())
    bar.show("Skopiowano plik i pełną ścieżkę")
    frame.Show()
    wx.Yield()

    # --- A. natywny pasek stanu ------------------------------------------
    native = frame.GetStatusBar()
    klass = native_class_name(int(native.GetHandle()))
    verdict["status_bar"] = {
        "window_class": klass,
        "is_native_statusbar": klass.lower() == "msctls_statusbar32",
        "text": native.GetStatusText(0),
        "frame_title": frame.GetTitle(),
        # Pasek stanu nie moze byc celem klawiatury (regula z
        # AccessiblePlaybackStatusStrip.cs).
        "accepts_focus": bool(native.AcceptsFocus()),
    }

    # --- B. CF_HDROP po Ctrl+Shift+C --------------------------------------
    folder = tempfile.mkdtemp(prefix="amc-proba-")
    path = os.path.join(folder, "proba.mp3")
    Path(path).write_bytes(b"ID3")

    from types import SimpleNamespace

    from amc_wx_lite.list_model import Row

    probe = gui.LiteFrame.__new__(gui.LiteFrame)
    spoken: list[str] = []
    probe.announcer = SimpleNamespace(say=spoken.append)
    probe.navigator = SimpleNamespace(
        session=SimpleNamespace(
            model=SimpleNamespace(
                selected_row=Row(item_id="f", title="proba.mp3", kind="track", path=path)
            )
        )
    )
    probe._copy_address()

    files = read_clipboard_file_drop()
    verdict["file_drop"] = {
        "expected_path": path,
        "clipboard_cf_hdrop": files,
        "matches": [os.path.normcase(f) for f in files] == [os.path.normcase(path)],
        "spoken": spoken,
    }

    frame.Destroy()
    app.Destroy()

    ok = (
        verdict["status_bar"]["is_native_statusbar"]
        and not verdict["status_bar"]["accepts_focus"]
        and verdict["file_drop"]["matches"]
        and spoken == ["Skopiowano plik i pełną ścieżkę"]
    )
    verdict["ok"] = ok
    verdict["pozostaje_recznie"] = [
        "NVDA+End w glownym widoku -- czy czyta tresc paska, nie ostatnie slowo tytulu",
        "F6 do odtwarzacza, potem NVDA+End -- to samo",
        "Ctrl+Shift+C na pliku, potem Ctrl+V w Total Commanderze -- czy powstaje kopia",
    ]
    print(json.dumps(verdict, ensure_ascii=False, indent=2))
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
