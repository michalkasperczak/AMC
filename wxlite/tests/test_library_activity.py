"""Testy warstwy danych aktywnosci: historia, zapisana kolejka, zakladki.

Dwie rozdzielone warstwy pomiaru:

* rogi na SYNTETYCZNEJ bazie o PRAWDZIWYM schemacie (odczytanym z bazy AMC,
  nie z pamieci): brak pozycji, pozycja niedostepna, duplikat Id, limit 500,
  sesje obce, rozdzial bez zakladki, swiezy odczyt przy OTWARTYM writerze,
* przebieg na PELNEJ kopii profilu -- osobny plik, bez tytulow i sciezek
  (``tools/measure_library_activity.py``).

Czego te testy NIE dowodza: nie uruchamiaja oryginalnego C#. Sprawdzaja, ze
NASZ kod robi to, co PRZECZYTANA logika zrodlowa (cytaty i numery wierszy w
``LIBRARY_ACTIVITY_CONTRACT.md``). Zgodnosc z FAKTYCZNIE URUCHOMIONYM C#
mierzy osobna sonda ``probe-csharp-activity`` (wynik w kwicie).
"""

from __future__ import annotations

import os
import sqlite3
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite.library_activity import (  # noqa: E402
    MAX_HISTORY_ENTRIES_PER_SESSION,
    history_rows,
)
from amc_wx_lite.library_db import LibraryDatabase  # noqa: E402

FIXTURE = Path(
    os.environ.get(
        "AMC_WX_FIXTURE",
        "/home/michal/projekty/amc_pomoc/wx-full-profile-after421/fixture",
    )
)
LIBRARY_DB = FIXTURE / "library.db"

#: Schemat PRZEPISANY z chronionego fixture (``sqlite_schema``), nie z glowy.
#: Kolumny ``purpose``/``chapter_origin``/``chapter_source_id`` doszly migracja
#: i w bazie maja wartosci domyslne -- tutaj tak samo, inaczej test mierzylby
#: inny schemat niz produkcja.
_SCHEMA = """
CREATE TABLE local_items (
    id TEXT PRIMARY KEY,
    title TEXT NOT NULL COLLATE AMC_PL,
    has_custom_title INTEGER NOT NULL,
    path TEXT NOT NULL,
    duration_ticks INTEGER NOT NULL,
    bitrate_estimated INTEGER NOT NULL,
    is_favorite INTEGER NOT NULL,
    is_in_library INTEGER NOT NULL,
    is_available INTEGER NOT NULL,
    is_in_queue INTEGER NOT NULL,
    is_play_next INTEGER NOT NULL,
    resume_mode INTEGER NOT NULL,
    resume_position_ticks INTEGER NOT NULL,
    is_radio_recording INTEGER NOT NULL
);
CREATE TABLE playback_history (
    session_id TEXT NOT NULL, ordinal INTEGER NOT NULL, item_id TEXT NOT NULL,
    PRIMARY KEY(session_id, ordinal)
);
CREATE TABLE queue_order (
    session_id TEXT NOT NULL, ordinal INTEGER NOT NULL, item_id TEXT NOT NULL,
    PRIMARY KEY(session_id, ordinal)
);
CREATE TABLE queue_regular_order (
    session_id TEXT NOT NULL, ordinal INTEGER NOT NULL, item_id TEXT NOT NULL,
    PRIMARY KEY(session_id, ordinal)
);
CREATE TABLE queue_play_next_order (
    session_id TEXT NOT NULL, ordinal INTEGER NOT NULL, item_id TEXT NOT NULL,
    PRIMARY KEY(session_id, ordinal)
);
CREATE TABLE bookmarks (
    id TEXT PRIMARY KEY,
    ordinal INTEGER NOT NULL,
    session_id TEXT NOT NULL,
    session_name TEXT NOT NULL,
    item_id TEXT NOT NULL,
    item_title TEXT NOT NULL COLLATE AMC_PL,
    name TEXT NOT NULL COLLATE AMC_PL,
    position_ticks INTEGER NOT NULL,
    created_utc_ticks INTEGER NOT NULL,
    purpose INTEGER NOT NULL DEFAULT 1,
    chapter_origin INTEGER NOT NULL DEFAULT 0,
    chapter_source_id TEXT NULL
);
CREATE TABLE metadata (key TEXT PRIMARY KEY, value TEXT NOT NULL);
"""


def _ordinal(left: str, right: str) -> int:
    return (left > right) - (left < right)


class _Builder:
    """Pisze baze o PRAWDZIWYM schemacie. Writer zostaje OTWARTY (tryb WAL)."""

    def __init__(self, path: Path) -> None:
        self.connection = sqlite3.connect(path, isolation_level=None)
        self.connection.create_collation("AMC_PL", _ordinal)
        self.connection.execute("PRAGMA journal_mode=WAL").fetchone()
        self.connection.executescript(_SCHEMA)
        self._bookmarks = 0

    def item(
        self,
        item_id: str,
        title: str,
        path: str,
        *,
        available: bool = True,
        in_library: bool = True,
        in_queue: bool = False,
        play_next: bool = False,
        ticks: int = 0,
    ) -> str:
        self.connection.execute(
            "INSERT INTO local_items (id, title, has_custom_title, path, "
            "duration_ticks, bitrate_estimated, is_favorite, is_in_library, "
            "is_available, is_in_queue, is_play_next, resume_mode, "
            "resume_position_ticks, is_radio_recording) "
            "VALUES (?, ?, 0, ?, ?, 0, 0, ?, ?, ?, ?, 0, 0, 0)",
            (
                item_id,
                title,
                path,
                ticks,
                int(in_library),
                int(available),
                int(in_queue),
                int(play_next),
            ),
        )
        return item_id

    def history(self, *item_ids: str, session: str = "local", start: int = 0) -> None:
        for offset, item_id in enumerate(item_ids):
            self.connection.execute(
                "INSERT INTO playback_history(session_id, ordinal, item_id) "
                "VALUES (?, ?, ?)",
                (session, start + offset, item_id),
            )

    def queue(self, *item_ids: str, session: str = "local") -> None:
        for ordinal, item_id in enumerate(item_ids):
            self.connection.execute(
                "INSERT INTO queue_order(session_id, ordinal, item_id) VALUES (?, ?, ?)",
                (session, ordinal, item_id),
            )

    def queue_regular(self, *item_ids: str, session: str = "local") -> None:
        for ordinal, item_id in enumerate(item_ids):
            self.connection.execute(
                "INSERT INTO queue_regular_order(session_id, ordinal, item_id) "
                "VALUES (?, ?, ?)",
                (session, ordinal, item_id),
            )

    def queue_play_next(self, *item_ids: str, session: str = "local") -> None:
        for ordinal, item_id in enumerate(item_ids):
            self.connection.execute(
                "INSERT INTO queue_play_next_order(session_id, ordinal, item_id) "
                "VALUES (?, ?, ?)",
                (session, ordinal, item_id),
            )

    def bookmark(
        self,
        bookmark_id: str,
        item_id: str,
        position_ticks: int,
        created_utc_ticks: int,
        *,
        session: str = "local",
        session_name: str = "Pliki lokalne",
        item_title: str = "",
        name: str = "",
        purpose: int = 1,
    ) -> str:
        self.connection.execute(
            "INSERT INTO bookmarks(id, ordinal, session_id, session_name, item_id, "
            "item_title, name, position_ticks, created_utc_ticks, purpose, "
            "chapter_origin, chapter_source_id) "
            "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 0, NULL)",
            (
                bookmark_id,
                self._bookmarks,
                session,
                session_name,
                item_id,
                item_title,
                name,
                position_ticks,
                created_utc_ticks,
                purpose,
            ),
        )
        self._bookmarks += 1
        return bookmark_id

    def close(self) -> None:
        self.connection.close()


class _SyntheticCase(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.path = Path(self.tmp.name) / "library.db"
        self.build = _Builder(self.path)
        self.addCleanup(self.build.close)

    def open_db(self) -> LibraryDatabase:
        db = LibraryDatabase(self.path)
        self.addCleanup(db.close)
        return db


# ---------------------------------------------------------------- 1. historia


class HistoryView(_SyntheticCase):
    def test_newest_first_from_ordinal(self):
        """``ordinal`` 0 to OSTATNIO odtworzone.

        ``PlaybackHistory.Record`` robi ``entries.Insert(0, itemId)``
        (``PlaybackHistory.cs:29``), a widok czyta ``GetItemIds`` BEZ sortu
        (``MainWindow.xaml.cs:12856``). Kolejnosc zapisu jest wiec gotowa i
        nie wolno jej sortowac alfabetycznie.
        """
        self.build.item("a", "Alfa", "C:/m/a.mp3")
        self.build.item("b", "Beta", "C:/m/b.mp3")
        self.build.item("c", "Gamma", "C:/m/c.mp3")
        self.build.history("c", "a", "b")

        result = history_rows(self.open_db())

        self.assertEqual([row.item_id for row in result.rows], ["c", "a", "b"])
        self.assertTrue(result.order_matches_amc)

    def test_drops_items_missing_from_catalog_without_placeholder(self):
        """Brak pozycji w katalogu = BRAK wiersza, nie wiersz zastepczy.

        ``MainWindow.xaml.cs:12857-12859``: ``itemsById.GetValueOrDefault`` i
        ``Where(item => item is not null)``. WPF nie tworzy tu zadnego
        placeholdera, wiec my tez nie.
        """
        self.build.item("a", "Alfa", "C:/m/a.mp3")
        self.build.history("znikl", "a")

        result = history_rows(self.open_db())

        self.assertEqual([row.item_id for row in result.rows], ["a"])
        self.assertEqual(result.missing_item_count, 1)

    def test_unavailable_item_is_not_listed(self):
        """Katalog sesji lokalnej to ``ActiveLocalItems()``.

        ``MainWindow.xaml.cs:12855`` buduje slownik z ``session.Items``, a dla
        ``local`` sesja powstaje z ``ActiveLocalItems()``
        (``MainWindow.xaml.cs:9972``, ``10116-10117``): ``IsAvailable &&
        IsInLibrary``. Pozycja niedostepna nie jest wiec w historii widoczna --
        to przeczytana regula ORYGINALU, nie nasze uproszczenie.
        """
        self.build.item("a", "Alfa", "C:/m/a.mp3")
        self.build.item("b", "Beta", "C:/m/b.mp3", available=False)
        self.build.item("c", "Gamma", "C:/m/c.mp3", in_library=False)
        self.build.history("b", "c", "a")

        result = history_rows(self.open_db())

        self.assertEqual([row.item_id for row in result.rows], ["a"])
        self.assertEqual(result.missing_item_count, 2)

    def test_repeated_item_id_appears_once_at_first_position(self):
        """``Distinct(Ordinal)`` z ``Normalize`` (``PlaybackHistory.cs:54``).

        ``Record`` usuwa starsze wystapienie przed wstawieniem na przod
        (``PlaybackHistory.cs:26-29``), wiec powtorzenie tego samego elementu
        zostaje JEDNYM wpisem -- i to tym NOWSZYM (mniejszy ``ordinal``).
        """
        self.build.item("a", "Alfa", "C:/m/a.mp3")
        self.build.item("b", "Beta", "C:/m/b.mp3")
        self.build.history("a", "b", "a")

        result = history_rows(self.open_db())

        self.assertEqual([row.item_id for row in result.rows], ["a", "b"])

    def test_item_id_comparison_is_case_sensitive(self):
        """Id porownuje sie ``StringComparison.Ordinal``.

        ``PlaybackHistory.cs:26`` i ``54``, slownik katalogu
        ``MainWindow.xaml.cs:12855`` tez ``StringComparer.Ordinal``.
        """
        self.build.item("local-A", "Alfa", "C:/m/a.mp3")
        self.build.item("local-a", "Alfa male", "C:/m/b.mp3")
        self.build.history("local-a", "local-A")

        result = history_rows(self.open_db())

        self.assertEqual([row.item_id for row in result.rows], ["local-a", "local-A"])

    def test_caps_at_five_hundred_entries(self):
        """``MaxEntriesPerSession = 500`` (``PlaybackHistory.cs:5``).

        ``Normalize`` bierze ``Take(500)`` PO ``Distinct``
        (``PlaybackHistory.cs:52-56``), czyli obcina OGON, nie poczatek.
        """
        for index in range(MAX_HISTORY_ENTRIES_PER_SESSION + 7):
            self.build.item(f"i{index:04d}", f"Utwor {index}", f"C:/m/{index}.mp3")
        self.build.history(*[f"i{index:04d}" for index in range(507)])

        result = history_rows(self.open_db())

        self.assertEqual(len(result.rows), MAX_HISTORY_ENTRIES_PER_SESSION)
        self.assertEqual(result.rows[0].item_id, "i0000")
        self.assertEqual(result.rows[-1].item_id, "i0499")

    def test_session_key_is_case_insensitive_but_other_sessions_never_mix(self):
        """``ItemIdsBySession`` to slownik ``OrdinalIgnoreCase``.

        ``PlaybackHistory.cs:47-49``. Jednoczesnie historia podcastow, radia
        czy TIDAL-a NIE wchodzi do historii plikow lokalnych: widok czyta
        ``GetItemIds(_sessions.Current.Id)`` dla JEDNEJ sesji
        (``MainWindow.xaml.cs:12856``).
        """
        self.build.item("a", "Alfa", "C:/m/a.mp3")
        self.build.item("b", "Beta", "C:/m/b.mp3")
        self.build.history("a", session="LOCAL")
        self.build.history("b", session="podcasts")

        result = history_rows(self.open_db(), session="local")

        self.assertEqual([row.item_id for row in result.rows], ["a"])

    def test_blank_session_is_empty(self):
        """``string.IsNullOrWhiteSpace(sessionId)`` -> ``[]``.

        ``PlaybackHistory.cs:9-13``.
        """
        self.build.item("a", "Alfa", "C:/m/a.mp3")
        self.build.history("a")

        self.assertEqual(history_rows(self.open_db(), session="   ").rows, [])

    def test_empty_history_is_not_an_error(self):
        self.build.item("a", "Alfa", "C:/m/a.mp3")

        result = history_rows(self.open_db())

        self.assertTrue(result.is_empty)
        self.assertEqual(result.missing_item_count, 0)

    def test_sees_commit_from_open_writer(self):
        """Swiezosc WAL: writer zostaje OTWARTY, a ponowny odczyt widzi commit.

        Bez tego kazdy pomiar historii mogl pokazywac stan sprzed zapisu AMC.
        Zachowania WAL po ZAMKNIECIU writera celowo nie zakladamy.
        """
        self.build.item("a", "Alfa", "C:/m/a.mp3")
        self.build.item("b", "Beta", "C:/m/b.mp3")
        self.build.history("a")
        db = self.open_db()
        self.assertEqual([row.item_id for row in history_rows(db).rows], ["a"])

        self.build.connection.execute("BEGIN")
        self.build.connection.execute(
            "INSERT INTO playback_history(session_id, ordinal, item_id) "
            "VALUES ('local', -1, 'b')"
        )
        self.build.connection.execute("COMMIT")

        self.assertEqual([row.item_id for row in history_rows(db).rows], ["b", "a"])
        self.assertTrue(history_rows(db).sees_live_writes)
