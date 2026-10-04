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

from .collation import HostCollation, HostCollationUnavailable, order_library_rows
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
    #: Czy kolejnosc wierszy jest zgodna z oryginalnym C#. ``False`` znaczy
    #: "host nie podal kluczy AMC_PL, kolejnosc jest zastepcza" -- i okno moze
    #: to powiedziec, zamiast milczeniem udawac zgodnosc.
    order_matches_amc: bool = False
    #: Czy odczyt sledzil zywy WAL. ``False`` = zamrozona migawka, wiec okno
    #: MUSI to powiedziec, a nie udawac swiezych danych.
    sees_live_writes: bool = True

    @property
    def is_empty(self) -> bool:
        return not self.rows


def degradation_notice(snapshot: LibrarySnapshot) -> str:
    """Krotki, PRAWDZIWY komunikat o degradacji odczytu. Puste = wszystko OK.

    Istnieje, bo ``order_matches_amc`` i ``sees_live_writes`` nie byly nigdzie
    czytane: uzytkownik dostawal zastepcza kolejnosc albo stary obraz profilu
    w calkowitej ciszy.
    """
    problems: list[str] = []
    if not snapshot.sees_live_writes:
        problems.append("zamrozona migawka profilu, bez zywych zmian AMC")
    if not snapshot.order_matches_amc:
        problems.append("kolejnosc zastepcza, niezgodna z AMC")
    if not problems:
        return ""
    return "Uwaga: " + "; ".join(problems) + "."


class LibrarySource:
    """Czyta Biblioteke AMC na zadanie. Nie trzyma uchwytu miedzy odczytami.

    Uchwyt otwieramy na KAZDY odczyt, zeby zobaczyc to, co host C# wlasnie
    zapisal. Polaczenie idzie w trybie ``mode=ro`` (bez ``immutable=1``), bo
    ``immutable`` kazal SQLite pominac ``-wal`` i oddawal stan sprzed commitu.

    Kolejnosc wierszy ustala ``HostCollation`` -- klucze kolacji AMC_PL
    policzone oryginalnym ``CompareInfo`` w hoscie. Bez hosta zostaje kolejnosc
    z SQL (niezgodna) i wtedy ``LibrarySnapshot.order_matches_amc`` jest
    ``False``.
    """

    def __init__(
        self,
        layout: ProfileLayout | None = None,
        *,
        collation: HostCollation | None = None,
    ) -> None:
        self.layout = layout or resolve_layout()
        self._collation = collation

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

    def use_collation(self, collation: "HostCollation | None") -> None:
        """Podepnij kolejnosc liczona przez host C#.

        Wolane po starcie silnika: wczesniej nie ma z kim rozmawiac.
        """
        self._collation = collation

    def load(self, folder: str | None) -> LibrarySnapshot:
        with self._open() as db:
            rows = folder_rows(db, folder)
            if folder:
                rows = breadcrumb_rows(db, folder) + rows
                heading = f"Biblioteka — Foldery — {_leaf(folder)}"
            else:
                heading = "Biblioteka — Foldery"

            # Kolejnosc zgodna z C# liczymy PO odczycie: SQL moze uzyc tylko
            # tej kolacji, ktora da sie zarejestrowac w SQLite, a zgodny
            # komparator pl-PL siedzi w hoscie.
            ordered, matches = self._apply_amc_order(rows)
            return LibrarySnapshot(
                rows=ordered,
                heading=heading,
                folder_path=folder,
                total_active=db.count_active_items(),
                order_matches_amc=matches,
                sees_live_writes=db.sees_live_writes,
            )

    def _apply_amc_order(self, rows: list[Row]) -> tuple[list[Row], bool]:
        """Kolejnosc C#, jesli host odpowie; inaczej wejscie i uczciwe ``False``.

        Awaria kolacji NIE moze przewrocic Biblioteki: lepiej pokazac liste
        w kolejnosci zastepczej i oznaczyc ja, niz nie pokazac nic.
        """
        if self._collation is None:
            return rows, False
        try:
            return order_library_rows(rows, self._collation), True
        except HostCollationUnavailable:
            return rows, False

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
