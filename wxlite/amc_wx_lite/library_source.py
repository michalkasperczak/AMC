"""Zrodlo wierszy Biblioteki dla okna wxPython.

Rozdziela dwie rzeczy, ktore wczesniej byly zlepione w jedna:

* **Biblioteka AMC** -- rekordy z ``library.db``. Dziala tez wtedy, gdy
  sciezek nie ma na tej maszynie (``D:\\``, ``C:\\Users\\micha``). To ona
  stoi pod Ctrl+1.
* **Przegladanie dysku** -- ``files.listFolder`` w hoscie. Przydatne przy
  Ctrl+O, ale NIE jest Biblioteka.

Caly odczyt jest synchroniczny i tani (SQLite), ale okno i tak wola go przez
``runner.submit``, zeby wczytanie 11 tysiecy rekordow nie zatrzymalo GUI.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

from .library_db import LibraryDatabase, breadcrumb_rows, folder_rows
from .list_model import Row
from .profile_layout import ProfileLayout, resolve_layout


@dataclass(slots=True)
class LibrarySnapshot:
    """Wiersze + opis naglowka dla jednego poziomu Biblioteki."""

    rows: list[Row]
    heading: str
    folder_path: str | None
    total_active: int

    @property
    def is_empty(self) -> bool:
        return not self.rows


class LibrarySource:
    """Czyta Biblioteke AMC na zadanie. Nie trzyma uchwytu miedzy odczytami.

    Uchwyt otwieramy na KAZDY odczyt, bo ``immutable=1`` zamraza obraz bazy --
    gdybysmy trzymali jedno polaczenie, nie zobaczylibysmy zmian zapisanych
    przez hosta C#.
    """

    def __init__(self, layout: ProfileLayout | None = None) -> None:
        self.layout = layout or resolve_layout()

    @property
    def database_path(self) -> Path:
        return self.layout.library_db

    @property
    def is_available(self) -> bool:
        return self.database_path.exists()

    def _open(self) -> LibraryDatabase:
        return LibraryDatabase(self.database_path)

    def saved_folder(self) -> str | None:
        """Folder zapamietany przez AMC (``local_state.current_folder_path``)."""
        with self._open() as db:
            return db.local_state().current_folder_path

    def load(self, folder: str | None) -> LibrarySnapshot:
        with self._open() as db:
            rows = folder_rows(db, folder)
            if folder:
                rows = breadcrumb_rows(db, folder) + rows
                heading = f"Biblioteka — Foldery — {_leaf(folder)}"
            else:
                heading = "Biblioteka — Foldery"
            return LibrarySnapshot(
                rows=rows,
                heading=heading,
                folder_path=folder,
                total_active=db.count_active_items(),
            )

    def describe(self) -> str:
        """Komunikat dla czytnika ekranu, gdy Biblioteki nie ma."""
        if self.is_available:
            return ""
        return (
            f"Nie znalazlem Biblioteki AMC ({self.database_path}). "
            "Uruchom AMC albo wskaz folder: Ctrl+O."
        )


def _leaf(path: str) -> str:
    import ntpath

    norm = ntpath.normpath(path).rstrip("\\/")
    return ntpath.basename(norm) or norm
