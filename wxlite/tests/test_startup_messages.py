"""Komunikaty startowe: blad odczytu stacji i blad saved_folder MA sie uslyszec.

Dwa zjawiska, oba mierzone na ``_load_initial_content``:

1. ``RadioSource.load`` potrafi juz oddzielic prawdziwe zero stacji od bledu
   odczytu (``RadioSnapshot.load_error``). GUI musi ten komunikat WYPOWIEDZIEC,
   inaczej poprawka konczy sie w strukturze danych i nie dociera do czlowieka.

2. ``LibrarySource.saved_folder()`` otwiera SQLite. Jest wolane wewnatrz
   ``done()`` zadania startowego, a to leci przez ``wx.CallAfter``. Nieobsluzony
   wyjatek w CallAfter nie trafia nigdzie poza log: okno zostaje puste i CICHE.
   Blad odczytu zapamietanego folderu nie moze tak znikac -- Biblioteka ma sie
   wczytac od korzenia i powiedziec, co sie stalo.
"""

from __future__ import annotations

import sqlite3
import sys
import unittest
from pathlib import Path
from types import SimpleNamespace

sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from test_gui_logic import install_wx_stub  # noqa: E402

install_wx_stub()

import amc_wx_lite.gui as gui  # noqa: E402


def _frame(*, library, radio_snapshot, queued: list, said: list):
    """Atrapa okna -- tylko pola, ktorych dotyka wczytywanie zawartosci.

    ``_open_library`` NIE jest podmieniane: to prawdziwa metoda produktu,
    bo wlasnie ona zglasza zadanie odczytu do runnera. Podmiana ukrylaby to,
    co test ma zmierzyc.
    """
    frame = SimpleNamespace(
        library=library,
        options=SimpleNamespace(last_folder=None),
        announcer=SimpleNamespace(say=said.append),
        runner=SimpleNamespace(
            submit=lambda tag, work, done, failed: queued.append((tag, work, done, failed))
        ),
        navigator=SimpleNamespace(
            apply_stations=lambda rows, preferred_id=None: [],
            apply_folder=lambda path, rows, preferred_id=None: [],
            sessions={gui.SessionId.FILES: SimpleNamespace(folder_path=None)},
        ),
        stations=SimpleNamespace(as_payload=lambda: []),
        _radio_snapshot=radio_snapshot,
        _run=lambda ops: None,
        _open_folder=lambda path, preferred_id=None: None,
        _window_alive=lambda: True,
    )
    frame._open_library = lambda folder, preferred_id=None: gui.LiteFrame._open_library(
        frame, folder, preferred_id
    )
    return frame


class _Library:
    """Biblioteka-atrapa: dostepna, a ``saved_folder`` moze rzucac."""

    def __init__(self, *, error: Exception | None = None, folder: str | None = None) -> None:
        self._error = error
        self._folder = folder
        self.is_available = True

    def saved_folder(self) -> str | None:
        if self._error is not None:
            raise self._error
        return self._folder

    def describe(self) -> str:
        return ""


def _snapshot(stations: list, load_error: str | None = None, kept: bool = False):
    from amc_wx_lite.radio_source import RadioSnapshot
    from amc_wx_lite.state_store import StationList

    return RadioSnapshot(
        list=StationList(stations),
        from_amc_profile=True,
        load_error=load_error,
        kept_previous=kept,
    )


class RadioLoadErrorIsSpokenTests(unittest.TestCase):
    def _run_initial(self, snapshot, library=None):
        said: list[str] = []
        queued: list = []
        frame = _frame(
            library=library or _Library(folder=None),
            radio_snapshot=snapshot,
            queued=queued,
            said=said,
        )
        gui.LiteFrame._load_initial_content(frame)
        return said, queued

    def test_read_error_is_announced(self) -> None:
        said, _ = self._run_initial(_snapshot([], "Profil AMC jest uszkodzony"))
        self.assertTrue(
            any("uszkodzony" in m for m in said),
            f"blad odczytu stacji nie zostal wypowiedziany: {said}",
        )

    def test_successful_empty_profile_says_nothing_about_errors(self) -> None:
        said, _ = self._run_initial(_snapshot([]))
        self.assertFalse(
            any("uszkodzony" in m or "Nie mogę odczytać" in m for m in said),
            f"pusty profil nie jest bledem, a powiedziano: {said}",
        )


class SavedFolderErrorTests(unittest.TestCase):
    def _run_initial(self, library):
        said: list[str] = []
        queued: list = []
        frame = _frame(
            library=library,
            radio_snapshot=_snapshot([]),
            queued=queued,
            said=said,
        )
        # Nieobsluzony wyjatek = brak komunikatu i puste okno. Test ma to zlapac
        # jako PORAZKE, nie jako blad samego testu.
        try:
            gui.LiteFrame._load_initial_content(frame)
        except Exception as error:  # pragma: no cover - to jest badany objaw
            self.fail(f"nieobsluzony wyjatek w callbacku startowym: {error!r}")
        return said, queued

    def test_sqlite_error_in_saved_folder_is_announced(self) -> None:
        said, _ = self._run_initial(
            _Library(error=sqlite3.OperationalError("database is locked"))
        )
        self.assertTrue(said, "blad odczytu zapamietanego folderu przeszedl w ciszy")
        self.assertTrue(
            any("folder" in m.lower() for m in said),
            f"komunikat nie mowi o folderze: {said}",
        )

    def test_library_still_loads_from_root_after_saved_folder_error(self) -> None:
        said, queued = self._run_initial(
            _Library(error=sqlite3.DatabaseError("malformed"))
        )
        tags = [tag for tag, *_ in queued]
        self.assertIn("folder", tags, "Biblioteka ma sie wczytac od korzenia")

    def test_os_error_in_saved_folder_is_announced(self) -> None:
        said, _ = self._run_initial(_Library(error=OSError("brak dostepu")))
        self.assertTrue(said)

    def test_healthy_saved_folder_is_used_without_extra_noise(self) -> None:
        said, queued = self._run_initial(_Library(folder="D:\\muzyka"))
        self.assertIn("folder", [tag for tag, *_ in queued])
        self.assertFalse(
            any("nie mog" in m.lower() for m in said),
            f"zdrowy odczyt nie moze zglaszac bledu: {said}",
        )


if __name__ == "__main__":
    unittest.main()
