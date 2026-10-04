"""Warstwa DANYCH aktywnosci lokalnej Biblioteki (tylko odczyt).

Trzy odczyty, ktorych brakowalo nad widokami biblioteki:

1. **historia odtwarzania** plikow lokalnych,
2. **ZAPISANA kolejka** (to, co AMC utrwalilo w profilu),
3. **zakladki wybranego elementu** -- wejsciem jest RZECZYWISTE Id elementu.

Reguly filtrow, kolejnosci, duplikatow i pozycji nieznanych sa PRZEPISANE
z aktualnego kodu AMC (C#), nie zgadniete z nazw tabel. Cytaty i numery
wierszy: ``wxlite/LIBRARY_ACTIVITY_CONTRACT.md``.

Czego ten modul NIE robi
------------------------
Nie jest kolejka dzialajacego odtwarzacza Pythona. ``saved_queue_rows`` czyta
UTRWALONY stan profilu; kolejka zywego silnika to inna rzecz i nie ma tu
zadnego API odtwarzania. Modul nie zapisuje do bazy ani do profilu i nie
dotyka ``library_db.py``, ``library_views.py`` ani ``library_source.py`` --
pracuje na gotowym, read-only ``LibraryDatabase``.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Sequence

from .library_db import LibraryDatabase, LibraryItem, _ACTIVE, _format_detail
from .list_model import Row

#: Sesja plikow lokalnych.
LOCAL_SESSION = "local"

#: ``PlaybackHistory.MaxEntriesPerSession`` (``PlaybackHistory.cs:5``).
MAX_HISTORY_ENTRIES_PER_SESSION = 500

_ITEM_COLUMNS = (
    "id, title, path, duration_ticks, is_favorite, is_available, "
    "is_in_library, is_in_queue, is_play_next, is_radio_recording"
)


@dataclass(frozen=True, slots=True)
class ActivityResult:
    """Wynik jednego odczytu: wiersze + uczciwy opis tego, czego brakuje.

    ``missing_item_count`` to liczba ZAPISANYCH wpisow, ktorych oryginal NIE
    pokazuje (brak w katalogu sesji). Nie tworzymy dla nich wierszy, bo WPF
    tego nie robi -- ale liczba jest jawna, zeby puste pole nie wygladalo jak
    utrata danych.
    """

    rows: list[Row]
    heading: str
    order_matches_amc: bool = True
    sees_live_writes: bool = True
    missing_item_count: int = 0
    #: Metadane kolejki/zakladek OBOK ``Row`` (bez zmiany konstruktora ``Row``).
    queue: tuple["QueueRow", ...] = ()
    bookmarks: tuple["BookmarkRow", ...] = ()

    @property
    def is_empty(self) -> bool:
        return not self.rows


@dataclass(frozen=True, slots=True)
class QueueRow:
    """Jedna pozycja ZAPISANEJ kolejki, metadane obok ``Row``."""

    row: Row
    item_id: str
    is_in_queue: bool
    is_play_next: bool


@dataclass(frozen=True, slots=True)
class BookmarkRow:
    """Jedna zakladka, metadane obok ``Row``."""

    row: Row
    bookmark_id: str
    item_id: str
    item_title: str
    name: str
    session_id: str
    session_name: str
    position_ticks: int
    created_utc_ticks: int


def _item(row) -> LibraryItem:
    return LibraryItem(
        id=str(row["id"]),  # ID zostaje NAPISEM, nawet gdy wyglada na liczbe
        title=row["title"],
        path=row["path"],
        duration_ticks=int(row["duration_ticks"]),
        is_favorite=bool(row["is_favorite"]),
        is_available=bool(row["is_available"]),
        is_in_library=bool(row["is_in_library"]),
        is_radio_recording=bool(row["is_radio_recording"]),
    )


def _track_row(item: LibraryItem) -> Row:
    return Row(
        item_id=item.id,
        title=item.title,
        kind="track",
        path=item.path,
        detail=_format_detail(item),
    )


def _active_catalog(db: LibraryDatabase) -> dict[str, LibraryItem]:
    """Katalog sesji ``local`` po Id, ``StringComparer.Ordinal``.

    Odpowiednik ``_sessions.Current.Items.ToDictionary(item => item.Id,
    StringComparer.Ordinal)`` (``MainWindow.xaml.cs:12855``). Sesja lokalna
    powstaje z ``ActiveLocalItems()`` (``MainWindow.xaml.cs:9972``,
    ``10116-10117``: ``IsAvailable && IsInLibrary``), a ``ReplaceItems`` robi
    ``DistinctBy(Id, Ordinal)`` -- pierwsze wystapienie wygrywa.
    """
    catalog: dict[str, LibraryItem] = {}
    rows = db.connection.execute(
        f"SELECT {_ITEM_COLUMNS} FROM local_items WHERE {_ACTIVE} ORDER BY rowid"
    )
    for row in rows:
        item = _item(row)
        catalog.setdefault(item.id, item)
    return catalog


def _stored_ids(db: LibraryDatabase, table: str, session: str) -> list[str]:
    """Zapisana lista Id jednej sesji, ``ORDER BY ordinal``.

    Nazwa tabeli pochodzi z ZAMKNIETEGO zbioru, nie z wejscia uzytkownika.
    Klucz sesji w C# jest ``OrdinalIgnoreCase``
    (``PlaybackHistory.cs:47-49``, ``CollectionOrderSettings``
    ``AppSettings.cs:1199-1212``), wiec dopasowanie robimy bez wzgledu na
    wielkosc liter -- ale WYLACZNIE dla klucza sesji. Id zostaja ``Ordinal``.
    """
    if table not in (
        "playback_history",
        "queue_order",
        "queue_regular_order",
        "queue_play_next_order",
    ):
        raise ValueError(f"Nieznana tabela aktywnosci: {table}")
    rows = db.connection.execute(
        f"SELECT item_id FROM {table} WHERE session_id = ? COLLATE NOCASE "
        "ORDER BY ordinal",
        (session,),
    )
    return [str(row[0]) for row in rows]


def _distinct_ordinal(ids: Sequence[str]) -> list[str]:
    """``Distinct(StringComparer.Ordinal)`` z zachowaniem kolejnosci."""
    seen: set[str] = set()
    result: list[str] = []
    for item_id in ids:
        if item_id in seen:
            continue
        seen.add(item_id)
        result.append(item_id)
    return result


# ---------------------------------------------------------------- 1. historia


def history_rows(
    db: LibraryDatabase, *, session: str = LOCAL_SESSION
) -> ActivityResult:
    """Widok "Historia odtwarzania" (``MainWindow.xaml.cs:12853-12860``).

    Kolejnosc jest ZAPISANA, nie liczona: ``ordinal`` 0 to ostatnio odtworzone,
    bo ``Record`` wstawia na przod (``PlaybackHistory.cs:29``), a widok czyta
    ``GetItemIds`` bez zadnego ``OrderBy``. Zadnej alfabetyki, wiec klucze
    kolatora nie sa tu potrzebne i ``order_matches_amc`` zostaje ``True``.

    Reguly przeniesione 1:1:

    * puste/biale ``sessionId`` -> pusta lista (``PlaybackHistory.cs:9-13``),
    * ``Distinct(Ordinal)`` i ``Take(500)`` z ``Normalize``
      (``PlaybackHistory.cs:52-56``) -- obcinany jest OGON,
    * pozycja nieobecna w katalogu sesji NIE daje wiersza
      (``MainWindow.xaml.cs:12857-12859``), bez placeholdera,
    * katalog sesji lokalnej to ``ActiveLocalItems()``, wiec wpis niedostepny
      albo poza biblioteka nie jest widoczny -- to regula ORYGINALU
      (``MainWindow.xaml.cs:9972``, ``10116-10117``), nie nasz skrot.
    """
    heading = "Biblioteka — Historia odtwarzania"
    if not session or not session.strip():
        return ActivityResult(
            rows=[],
            heading=heading,
            sees_live_writes=db.sees_live_writes,
        )

    stored = _distinct_ordinal(_stored_ids(db, "playback_history", session))[
        :MAX_HISTORY_ENTRIES_PER_SESSION
    ]
    catalog = _active_catalog(db)
    rows: list[Row] = []
    missing = 0
    for item_id in stored:
        item = catalog.get(item_id)
        if item is None:
            missing += 1
            continue
        rows.append(_track_row(item))
    return ActivityResult(
        rows=rows,
        heading=heading,
        sees_live_writes=db.sees_live_writes,
        missing_item_count=missing,
    )
