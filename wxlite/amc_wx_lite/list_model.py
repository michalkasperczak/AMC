"""Model listy dla widoku wirtualnego (wx.ListCtrl LC_VIRTUAL).

Zasada: lista NIE przechowuje widgetow. Trzyma wiersze i wybor po STABILNYM
identyfikatorze, zeby po odswiezeniu folderu fokus nie skakal na pozycje 0.
Tekst oddajemy na zadanie (OnGetItemText w wx), wiec 10 tysiecy plikow nie
tworzy 10 tysiecy obiektow.

Kod celowo bez importu wx: dzieki temu da sie go przetestowac w WSL bez pulpitu.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Iterable, Sequence


@dataclass(frozen=True, slots=True)
class Row:
    """Jeden wiersz listy. ``item_id`` musi byc trwaly miedzy odswiezeniami."""

    item_id: str
    title: str
    kind: str  # "folder" | "track" | "station" | "parent"
    path: str | None = None
    url: str | None = None
    detail: str = ""

    @property
    def is_openable(self) -> bool:
        """Czy Enter ma WEJSC w element, zamiast go odtworzyc."""
        return self.kind in ("folder", "parent")


@dataclass(slots=True)
class ListModel:
    """Wiersze + wybor. Jedno zrodlo prawdy dla widoku wirtualnego."""

    rows: list[Row] = field(default_factory=list)
    _selected_id: str | None = None

    def __len__(self) -> int:
        return len(self.rows)

    @property
    def selected_id(self) -> str | None:
        return self._selected_id

    @property
    def selected_index(self) -> int:
        """Pozycja wyboru albo -1. Liczona z ID, nie pamietana osobno."""
        if self._selected_id is None:
            return -1
        for index, row in enumerate(self.rows):
            if row.item_id == self._selected_id:
                return index
        return -1

    @property
    def selected_row(self) -> Row | None:
        index = self.selected_index
        return self.rows[index] if index >= 0 else None

    def row_at(self, index: int) -> Row | None:
        if 0 <= index < len(self.rows):
            return self.rows[index]
        return None

    def text_for(self, index: int, column: int = 0) -> str:
        """Tekst komorki NA ZADANIE. Poza zakresem zwraca puste, nie wyjatek:
        wx potrafi zapytac o wiersz w trakcie zmiany dlugosci listy."""
        row = self.row_at(index)
        if row is None:
            return ""
        return row.title if column == 0 else row.detail

    def select_index(self, index: int) -> bool:
        row = self.row_at(index)
        if row is None:
            return False
        self._selected_id = row.item_id
        return True

    def select_id(self, item_id: str | None) -> bool:
        if item_id is None:
            self._selected_id = None
            return True
        if any(row.item_id == item_id for row in self.rows):
            self._selected_id = item_id
            return True
        return False

    def replace(
        self,
        rows: Iterable[Row],
        *,
        keep_selection: bool = True,
        preferred_id: str | None = None,
    ) -> None:
        """Podmien zawartosc listy.

        ``preferred_id`` ma pierwszenstwo (np. wracamy do folderu, z ktorego
        wyszlismy przez Backspace i chcemy stanac na nim). Gdy nie ma ani jego,
        ani poprzedniego wyboru, stajemy na pierwszym wierszu, ktory NIE jest
        wejsciem do rodzica - inaczej kazde odswiezenie konczy sie na "..".
        """
        previous = self._selected_id
        self.rows = list(rows)

        for candidate in (preferred_id, previous if keep_selection else None):
            if candidate is not None and self.select_id(candidate):
                return

        self._selected_id = None
        for row in self.rows:
            if row.kind != "parent":
                self._selected_id = row.item_id
                return
        if self.rows:
            self._selected_id = self.rows[0].item_id

    def find_prefix(self, prefix: str, start_index: int = 0) -> int:
        """Szukanie po pierwszej literze, zawijane. Natywny ListCtrl robi to sam
        dla zwyklych list, ale w trybie wirtualnym musimy podac wynik sami."""
        if not prefix or not self.rows:
            return -1
        needle = prefix.casefold()
        count = len(self.rows)
        for offset in range(1, count + 1):
            index = (start_index + offset) % count
            if self.rows[index].title.casefold().startswith(needle):
                return index
        return -1


def rows_from_folder_payload(payload: dict, *, include_parent: bool = True) -> list[Row]:
    """Zamien odpowiedz hosta ``files.listFolder`` na wiersze listy."""
    rows: list[Row] = []
    parent = payload.get("parent")
    if include_parent and parent:
        rows.append(Row(item_id=f"parent:{parent}", title="..", kind="parent", path=parent))
    for entry in payload.get("items", ()):
        kind = entry.get("kind", "track")
        rows.append(
            Row(
                item_id=str(entry.get("id") or entry.get("path") or entry.get("title", "")),
                title=str(entry.get("title", "")),
                kind=kind,
                path=entry.get("path"),
                detail="folder" if kind == "folder" else "",
            )
        )
    return rows


def rows_from_stations(stations: Sequence[dict]) -> list[Row]:
    """Wiersze dla sesji radiowej z WLASNEJ listy stacji uzytkownika."""
    return [
        Row(
            item_id=str(station["id"]),
            title=str(station.get("name", "")),
            kind="station",
            url=str(station.get("url", "")),
            detail=str(station.get("url", "")),
        )
        for station in stations
    ]
