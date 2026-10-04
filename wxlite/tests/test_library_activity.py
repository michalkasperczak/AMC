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
    bookmark_rows,
    history_rows,
    saved_queue_rows,
)
from amc_wx_lite.library_db import LibraryDatabase  # noqa: E402

FIXTURE = Path(
    os.environ.get(
        "AMC_WX_FIXTURE",
        "/home/michal/projekty/amc_pomoc/wx-full-profile-after421/fixture",
    )
)
LIBRARY_DB = FIXTURE / "library.db"


def _sha256(path: Path) -> str:
    """Skrot pliku bazy: dowod, ze odczyt NICZEGO nie zmienil."""
    import hashlib

    return hashlib.sha256(path.read_bytes()).hexdigest()

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


# --------------------------------------------------------- 2. zapisana kolejka


class SavedQueueView(_SyntheticCase):
    def test_play_next_block_comes_first_then_stored_order(self):
        """``OrderedQueueItems`` (``MainWindow.xaml.cs:13518-13525``).

        ``LocalLibraryManualOrder.Order(items, stored)``, a potem
        ``OrderByDescending(IsPlayNext)``. ``OrderByDescending`` w LINQ jest
        STABILNE, wiec "odtworz nastepne" wychodzi na gore BEZ mieszania
        kolejnosci wewnatrz obu blokow. To jest wlasnie rozroznienie
        kolejnosci wlasciwe dla AMC -- nie dwie osobne listy.
        """
        for name in ("a", "b", "c", "d"):
            self.build.item(name, name.upper(), f"C:/m/{name}.mp3", in_queue=True)
        self.build.queue("a", "b", "c", "d")
        self.build.queue_regular("a", "c")
        self.build.queue_play_next("b", "d")

        result = saved_queue_rows(self.open_db())

        self.assertEqual([row.item_id for row in result.rows], ["b", "d", "a", "c"])
        self.assertEqual(
            [(q.item_id, q.is_play_next) for q in result.queue],
            [("b", True), ("d", True), ("a", False), ("c", False)],
        )
        self.assertTrue(result.order_matches_amc)

    def test_membership_comes_from_saved_lists_not_from_item_flags(self):
        """``Restore`` czysci flagi i nadaje je z ZAPISU.

        ``TransientQueuePersistence.cs:109-127``: najpierw
        ``IsInQueue = IsPlayNext = false`` dla WSZYSTKICH, potem flagi tylko
        dla Id opisanych zapisana kolejnoscia. Kolumna ``is_in_queue`` w bazie
        to migawka POPRZEDNIEJ sesji i nie moze wygrac z zapisem.
        """
        self.build.item("a", "A", "C:/m/a.mp3", in_queue=True)
        self.build.item("b", "B", "C:/m/b.mp3", in_queue=True, play_next=True)
        self.build.item("c", "C", "C:/m/c.mp3", in_queue=False)
        self.build.queue("c")
        self.build.queue_regular("c")

        result = saved_queue_rows(self.open_db())

        self.assertEqual([row.item_id for row in result.rows], ["c"])
        self.assertEqual(result.queue[0].is_in_queue, True)
        self.assertEqual(result.queue[0].is_play_next, False)

    def test_legacy_queue_without_membership_lists_is_regular(self):
        """``legacyRegularQueue`` (``TransientQueuePersistence.cs:101``).

        Gdy OBIE listy czlonkostwa sa puste, zapis pochodzi ze starszej
        wersji i cala ``queue_order`` jest zwykla kolejka. Usuniecie tych
        wpisow "dla wygody" skasowaloby uzytkownikowi kolejke.
        """
        self.build.item("a", "A", "C:/m/a.mp3")
        self.build.item("b", "B", "C:/m/b.mp3")
        self.build.queue("a", "b")

        result = saved_queue_rows(self.open_db())

        self.assertEqual([row.item_id for row in result.rows], ["a", "b"])
        self.assertTrue(all(q.is_in_queue for q in result.queue))
        self.assertFalse(any(q.is_play_next for q in result.queue))

    def test_item_outside_catalog_is_skipped_without_placeholder(self):
        """``Restore`` dopasowuje Id do katalogu i pomija nieznane.

        ``TransientQueuePersistence.cs:116-119`` (``continue``). Wpisu nie
        usuwamy z bazy -- jest tylko niewidoczny, a liczba jest jawna.
        """
        self.build.item("a", "A", "C:/m/a.mp3")
        self.build.item("znikniety", "Z", "C:/m/z.mp3", available=False)
        self.build.queue("znikniety", "a")
        self.build.queue_regular("znikniety", "a")

        result = saved_queue_rows(self.open_db())

        self.assertEqual([row.item_id for row in result.rows], ["a"])
        self.assertEqual(result.missing_item_count, 1)

    def test_repeated_id_in_stored_order_appears_once(self):
        """``storedOrder.Distinct(Ordinal)`` (``TransientQueuePersistence.cs:114``).

        Pierwsze wystapienie wyznacza pozycje -- tak jak ``Order`` bierze
        indeks PIERWSZEGO wystapienia (``LocalLibraryManualOrder.cs:95-98``).
        """
        self.build.item("a", "A", "C:/m/a.mp3")
        self.build.item("b", "B", "C:/m/b.mp3")
        self.build.queue("a", "b", "a")
        self.build.queue_regular("a", "b")

        result = saved_queue_rows(self.open_db())

        self.assertEqual([row.item_id for row in result.rows], ["a", "b"])

    def test_catalog_item_missing_from_stored_order_is_not_in_queue(self):
        """Kolejka to pozycje Z ZAPISU, nie caly katalog.

        ``Restore`` zeruje flagi wszystkim i nadaje je tylko Id z zapisu
        (``TransientQueuePersistence.cs:109-127``), a ``OrderedQueueItems``
        filtruje ``IsInQueue || IsPlayNext`` (``MainWindow.xaml.cs:13520``).
        """
        self.build.item("a", "A", "C:/m/a.mp3", in_queue=True)
        self.build.item("b", "B", "C:/m/b.mp3")
        self.build.queue("b")
        self.build.queue_regular("b")

        result = saved_queue_rows(self.open_db())

        self.assertEqual([row.item_id for row in result.rows], ["b"])

    def test_id_present_in_order_but_in_no_membership_list_is_dropped(self):
        """Zapisany, ale ani zwykly, ani priorytetowy -> poza kolejka.

        ``Restore`` ustawia obie flagi na ``false``
        (``TransientQueuePersistence.cs:123-126``), a filtr widoku przepuszcza
        tylko ``IsInQueue || IsPlayNext``. Dzieje sie tak tylko, gdy
        czlonkostwo ISTNIEJE (inaczej dziala ``legacyRegularQueue``).
        """
        self.build.item("a", "A", "C:/m/a.mp3")
        self.build.item("b", "B", "C:/m/b.mp3")
        self.build.queue("a", "b")
        self.build.queue_regular("b")

        result = saved_queue_rows(self.open_db())

        self.assertEqual([row.item_id for row in result.rows], ["b"])

    def test_other_sessions_never_mix_into_local_queue(self):
        """Kolejka podcastow/TIDAL-a to inna sesja (``GetValueOrDefault(sessionId)``).

        ``MainWindow.xaml.cs:13513-13515``.
        """
        self.build.item("a", "A", "C:/m/a.mp3")
        self.build.queue("a")
        self.build.queue_regular("a")
        self.build.queue("pod-1", session="podcasts")
        self.build.queue_regular("pod-1", session="podcasts")

        result = saved_queue_rows(self.open_db(), session="local")

        self.assertEqual([row.item_id for row in result.rows], ["a"])

    def test_empty_saved_queue_is_not_an_error(self):
        self.build.item("a", "A", "C:/m/a.mp3", in_queue=True)

        result = saved_queue_rows(self.open_db())

        self.assertTrue(result.is_empty)

    def test_sees_commit_from_open_writer(self):
        """Swiezosc WAL przy OTWARTYM writerze -- ponowny odczyt widzi commit."""
        self.build.item("a", "A", "C:/m/a.mp3")
        self.build.item("b", "B", "C:/m/b.mp3")
        self.build.queue("a")
        self.build.queue_regular("a")
        db = self.open_db()
        self.assertEqual([row.item_id for row in saved_queue_rows(db).rows], ["a"])

        self.build.connection.execute("BEGIN")
        self.build.connection.execute(
            "INSERT INTO queue_order(session_id, ordinal, item_id) "
            "VALUES ('local', 1, 'b')"
        )
        self.build.connection.execute(
            "INSERT INTO queue_play_next_order(session_id, ordinal, item_id) "
            "VALUES ('local', 0, 'b')"
        )
        self.build.connection.execute("COMMIT")

        result = saved_queue_rows(db)
        self.assertEqual([row.item_id for row in result.rows], ["b", "a"])
        self.assertTrue(result.sees_live_writes)


# ------------------------------------------- 3. zakladki wybranego elementu


#: Ticks C#: 1 sekunda = 10 000 000. Ta sama jednostka co ``TimeSpan.Ticks``.
_SECOND = 10_000_000
#: 2026-01-15 12:00:00 UTC w tickach .NET (``DateTime.Ticks``, epoka 0001-01-01).
#: Poludnie, zeby ``ToLocalTime`` w zadnej strefie nie przesunelo doby.
_SOME_DATE = 639_040_752_000_000_000


class BookmarkView(_SyntheticCase):
    def test_selects_by_real_item_id_not_by_list_index(self):
        """Wejsciem jest RZECZYWISTE Id elementu, nie pozycja na liscie.

        ``GetForItem(sessionId, itemId)`` (``BookmarkIndex.cs:30-36``) --
        indeksu listy nie ma tam w ogole.
        """
        self.build.item("local-a", "Alfa", "C:/m/a.mp3")
        self.build.item("local-b", "Beta", "C:/m/b.mp3")
        self.build.bookmark("bm1", "local-a", 10 * _SECOND, _SOME_DATE)
        self.build.bookmark("bm2", "local-b", 20 * _SECOND, _SOME_DATE)

        result = bookmark_rows(self.open_db(), item_id="local-b")

        self.assertEqual([b.bookmark_id for b in result.bookmarks], ["bm2"])

    def test_orders_by_position_then_created(self):
        """``OrderBy(PositionTicks).ThenBy(CreatedUtcTicks)``.

        ``BookmarkIndex.cs:34-35``. Zadnej alfabetyki -- porzadek jest
        liczbowy, wiec kolator nie jest tu potrzebny.
        """
        self.build.item("local-a", "Alfa", "C:/m/a.mp3")
        self.build.bookmark("pozno", "local-a", 90 * _SECOND, _SOME_DATE)
        self.build.bookmark("nowszy", "local-a", 30 * _SECOND, _SOME_DATE + 5)
        self.build.bookmark("starszy", "local-a", 30 * _SECOND, _SOME_DATE)

        result = bookmark_rows(self.open_db(), item_id="local-a")

        self.assertEqual(
            [b.bookmark_id for b in result.bookmarks], ["starszy", "nowszy", "pozno"]
        )

    def test_item_id_is_ordinal_session_id_is_case_insensitive(self):
        """``ItemId`` ``Ordinal``, ``SessionId`` ``OrdinalIgnoreCase``.

        ``BookmarkIndex.cs:32-33``. Dwa rozne porownania w jednym warunku --
        nie wolno ich ujednolicic.
        """
        self.build.item("local-A", "Alfa", "C:/m/a.mp3")
        self.build.bookmark("duze", "local-A", 10 * _SECOND, _SOME_DATE)
        self.build.bookmark("male", "local-a", 10 * _SECOND, _SOME_DATE)
        self.build.bookmark("inna-wielkosc", "local-A", 20 * _SECOND, _SOME_DATE,
                            session="LOCAL")

        result = bookmark_rows(self.open_db(), item_id="local-A")

        self.assertEqual(
            [b.bookmark_id for b in result.bookmarks], ["duze", "inna-wielkosc"]
        )

    def test_chapter_only_entry_is_not_a_bookmark(self):
        """``IsBookmark`` to bit ``Bookmark`` w ``Purpose``.

        ``BookmarkIndex.cs:189-190``: ``(entry.Purpose & Bookmark) != 0``.
        Czysty rozdzial (``Purpose = 2``) NIE jest zakladka, a wpis o obu
        bitach (3) jest. Rozdzialow nie usuwamy -- po prostu ich nie
        pokazujemy w zakladkach.
        """
        self.build.item("local-a", "Alfa", "C:/m/a.mp3")
        self.build.bookmark("zakladka", "local-a", 10 * _SECOND, _SOME_DATE, purpose=1)
        self.build.bookmark("rozdzial", "local-a", 20 * _SECOND, _SOME_DATE, purpose=2)
        self.build.bookmark("oba", "local-a", 30 * _SECOND, _SOME_DATE, purpose=3)

        result = bookmark_rows(self.open_db(), item_id="local-a")

        self.assertEqual([b.bookmark_id for b in result.bookmarks], ["zakladka", "oba"])

    def test_two_bookmarks_at_same_position_both_survive(self):
        """Powtorzony element i ta sama pozycja: oba wpisy ZOSTAJA.

        Tolerancja 1 s z ``Add`` (``BookmarkIndex.cs:10``, ``53-56``) dziala
        tylko przy DODAWANIU nowej zakladki. Odczyt ``GetForItem`` nie scala
        niczego, wiec wpisy, ktore juz sa w bazie, nie moga znikac przy
        powtorzeniu Id.
        """
        self.build.item("local-a", "Alfa", "C:/m/a.mp3")
        self.build.bookmark("pierwsza", "local-a", 10 * _SECOND, _SOME_DATE)
        self.build.bookmark("druga", "local-a", 10 * _SECOND, _SOME_DATE + 1)

        result = bookmark_rows(self.open_db(), item_id="local-a")

        self.assertEqual([b.bookmark_id for b in result.bookmarks], ["pierwsza", "druga"])

    def test_label_has_date_time_name_and_session(self):
        """Etykieta 1:1 z ``CreateBookmarkRow`` (``MainWindow.xaml.cs:13763``).

        ``{ItemTitle}, {data}, {czas}, {nazwa}, {sesja}, zakładka`` -- czas
        przez ``FormatDuration`` (``MediaItemFormatter.cs:69-73``), data po
        polsku z ``pl-PL`` (``MainWindow.xaml.cs:13767-13779``).
        """
        self.build.item("local-a", "Alfa", "C:/m/a.mp3")
        self.build.bookmark(
            "bm",
            "local-a",
            125 * _SECOND,
            _SOME_DATE,
            item_title="Alfa",
            name="Refren",
            session_name="Pliki lokalne",
        )

        result = bookmark_rows(self.open_db(), item_id="local-a")
        label = result.rows[0].title

        self.assertIn("2:05", label)
        self.assertIn("Refren", label)
        self.assertIn("Pliki lokalne", label)
        self.assertTrue(label.endswith("zakładka"))
        self.assertIn("stycznia 2026", label)

    def test_blank_name_leaves_no_empty_comma_section(self):
        """``namePart`` jest PUSTY przy pustej nazwie (``MainWindow.xaml.cs:13762``)."""
        self.build.item("local-a", "Alfa", "C:/m/a.mp3")
        self.build.bookmark(
            "bm", "local-a", 60 * _SECOND, _SOME_DATE, item_title="Alfa", name="   "
        )

        label = bookmark_rows(self.open_db(), item_id="local-a").rows[0].title

        self.assertNotIn(", , ", label)

    def test_unknown_created_date_is_not_a_fake_date(self):
        """``CreatedUtcTicks <= 0`` -> "data utworzenia nieznana".

        ``MainWindow.xaml.cs:13769``. Nieznany czas to NIE jest zero ani
        1 stycznia roku 1 -- wzorzec C# mowi wprost, ze nie wie.
        """
        self.build.item("local-a", "Alfa", "C:/m/a.mp3")
        self.build.bookmark("bm", "local-a", 60 * _SECOND, 0, item_title="Alfa")

        label = bookmark_rows(self.open_db(), item_id="local-a").rows[0].title

        self.assertIn("data utworzenia nieznana", label)

    def test_row_id_is_prefixed_and_stable(self):
        """``Id = $"bookmark:{bookmark.Id}"`` (``MainWindow.xaml.cs:13756``).

        Wiersz zakladki ma WLASNY, trwaly identyfikator -- inaczej dwie
        zakladki tego samego utworu mialyby to samo Id i wybor by skakal.
        """
        self.build.item("local-a", "Alfa", "C:/m/a.mp3")
        self.build.bookmark("bm1", "local-a", 10 * _SECOND, _SOME_DATE)
        self.build.bookmark("bm2", "local-a", 20 * _SECOND, _SOME_DATE)

        rows = bookmark_rows(self.open_db(), item_id="local-a").rows

        self.assertEqual([row.item_id for row in rows], ["bookmark:bm1", "bookmark:bm2"])

    def test_bookmark_of_missing_item_still_shows_with_stored_title(self):
        """Zakladka NIE znika, gdy pozycji nie ma juz w katalogu.

        ``CreateBookmarkRow`` tworzy zastepczy ``MediaItem`` z
        ``bookmark.ItemTitle`` (``MainWindow.xaml.cs:13749-13753``), a sam
        wiersz bierze tytul z ZAKLADKI, nie z katalogu (``13757``). Tu
        placeholder JEST w oryginale -- inaczej niz w historii.
        """
        self.build.bookmark(
            "bm", "local-nieobecny", 30 * _SECOND, _SOME_DATE, item_title="Stary tytul"
        )

        result = bookmark_rows(self.open_db(), item_id="local-nieobecny")

        self.assertEqual(len(result.rows), 1)
        self.assertEqual(result.bookmarks[0].item_title, "Stary tytul")
        self.assertTrue(result.rows[0].title.startswith("Stary tytul"))

    def test_other_sessions_never_mix_into_local_item(self):
        """Zakladka podcastu nie wchodzi do zakladek pliku lokalnego."""
        self.build.item("local-a", "Alfa", "C:/m/a.mp3")
        self.build.bookmark("lok", "local-a", 10 * _SECOND, _SOME_DATE)
        self.build.bookmark(
            "pod", "local-a", 20 * _SECOND, _SOME_DATE, session="podcasts"
        )

        result = bookmark_rows(self.open_db(), item_id="local-a", session="local")

        self.assertEqual([b.bookmark_id for b in result.bookmarks], ["lok"])

    def test_blank_item_id_is_empty(self):
        self.build.item("local-a", "Alfa", "C:/m/a.mp3")
        self.build.bookmark("bm", "local-a", 10 * _SECOND, _SOME_DATE)

        self.assertTrue(bookmark_rows(self.open_db(), item_id="  ").is_empty)

    def test_sees_commit_from_open_writer(self):
        """Swiezosc WAL przy OTWARTYM writerze."""
        self.build.item("local-a", "Alfa", "C:/m/a.mp3")
        self.build.bookmark("bm1", "local-a", 10 * _SECOND, _SOME_DATE)
        db = self.open_db()
        self.assertEqual(len(bookmark_rows(db, item_id="local-a").rows), 1)

        self.build.connection.execute("BEGIN")
        self.build.connection.execute(
            "INSERT INTO bookmarks(id, ordinal, session_id, session_name, item_id, "
            "item_title, name, position_ticks, created_utc_ticks, purpose, "
            "chapter_origin, chapter_source_id) "
            "VALUES ('bm2', 9, 'local', 'Pliki lokalne', 'local-a', 'Alfa', '', "
            f"{5 * _SECOND}, {_SOME_DATE}, 1, 0, NULL)"
        )
        self.build.connection.execute("COMMIT")

        result = bookmark_rows(db, item_id="local-a")
        self.assertEqual([b.bookmark_id for b in result.bookmarks], ["bm2", "bm1"])
        self.assertTrue(result.sees_live_writes)


# ------------------------- rzeczywisty odczyt CALEJ wlasciwej czesci fixture


@unittest.skipUnless(LIBRARY_DB.exists(), f"brak chronionego fixture: {LIBRARY_DB}")
class RealFixture(unittest.TestCase):
    """Pomiar na PELNEJ chronionej bazie, nie na probce.

    Fixture jest otwierany WYLACZNIE do odczytu (``mode=ro``) i nigdy do
    zapisu; liczby sa zliczane programowo, a nie przepisywane z raportu.
    """

    @classmethod
    def setUpClass(cls):
        cls.db = LibraryDatabase(LIBRARY_DB)
        cls.db.__enter__()
        cls.sha_before = _sha256(LIBRARY_DB)

    @classmethod
    def tearDownClass(cls):
        cls.db.__exit__(None, None, None)

    def test_fixture_is_the_known_protected_database(self):
        self.assertEqual(
            self.sha_before,
            "211d8ecf8cf2031d955c6b7e9e6676ddccedd3a04cb64a0306baffbcfa476572",
        )

    def test_read_does_not_modify_the_fixture(self):
        history_rows(self.db)
        saved_queue_rows(self.db)
        for item_id in self._bookmarked_item_ids():
            bookmark_rows(self.db, item_id=item_id)
        self.assertEqual(_sha256(LIBRARY_DB), self.sha_before)

    # --- historia ----------------------------------------------------------

    def test_history_hides_exactly_the_inactive_stored_entries(self):
        """119 z 275 zapisanych wpisow daje wiersz -- i wiadomo DLACZEGO.

        Zmierzone na fixture: 135 pozycji ``is_available = 0`` i 21
        ``is_in_library = 0``. Suma 156 to dokladnie liczba wpisow bez wiersza,
        czyli regula ``ActiveLocalItems`` (``MainWindow.xaml.cs:10116-10117``),
        a nie przypadkowa utrata danych.
        """
        stored = [
            str(r["item_id"])
            for r in self.db.connection.execute(
                "SELECT item_id FROM playback_history "
                "WHERE session_id = 'local' COLLATE NOCASE ORDER BY ordinal"
            )
        ]
        flags = {
            str(r["id"]): (bool(r["is_available"]), bool(r["is_in_library"]))
            for r in self.db.connection.execute(
                "SELECT id, is_available, is_in_library FROM local_items"
            )
        }
        inactive = sum(1 for i in stored if not all(flags.get(i, (False, False))))

        result = history_rows(self.db)

        self.assertEqual(len(stored), 275)
        self.assertEqual(len(result.rows), 275 - inactive)
        self.assertEqual(result.missing_item_count, inactive)
        self.assertEqual(inactive, 156)

    def test_history_has_no_duplicate_ids_and_respects_the_cap(self):
        result = history_rows(self.db)
        ids = [row.item_id for row in result.rows]

        self.assertEqual(len(ids), len(set(ids)))
        self.assertLessEqual(len(ids), MAX_HISTORY_ENTRIES_PER_SESSION)

    def test_history_keeps_the_stored_order(self):
        """Wynik jest PODCIAGIEM zapisu: tabela nie ma kolumny czasu."""
        stored = [
            str(r["item_id"])
            for r in self.db.connection.execute(
                "SELECT item_id FROM playback_history "
                "WHERE session_id = 'local' COLLATE NOCASE ORDER BY ordinal"
            )
        ]
        returned = [row.item_id for row in history_rows(self.db).rows]
        kept = set(returned)

        self.assertEqual(returned, [i for i in dict.fromkeys(stored) if i in kept])

    def test_history_never_mixes_other_sessions(self):
        """811 wierszy w tabeli, ale sesja lokalna ma tylko 275.

        Podcasty/stacje/tidal nie moga wejsc do historii plikow lokalnych.
        """
        total = self.db.connection.execute(
            "SELECT COUNT(*) FROM playback_history"
        ).fetchone()[0]
        local = self.db.connection.execute(
            "SELECT COUNT(*) FROM playback_history "
            "WHERE session_id = 'local' COLLATE NOCASE"
        ).fetchone()[0]

        self.assertGreater(total, local)
        self.assertLessEqual(len(history_rows(self.db).rows), local)

    # --- zapisana kolejka --------------------------------------------------

    def test_saved_queue_matches_stored_membership_not_the_columns(self):
        """Czlonkostwo bierze sie z ZAPISU, nie z kolumn migawkowych.

        Fixture ma 8 wierszy ``is_in_queue = 1``, ale zapisana kolejka lokalna
        ma 6 pozycji -- gdyby kolumny wygraly, widok pokazalby 8.
        """
        stored = [
            str(r["item_id"])
            for r in self.db.connection.execute(
                "SELECT item_id FROM queue_order "
                "WHERE session_id = 'local' COLLATE NOCASE ORDER BY ordinal"
            )
        ]
        column_flagged = self.db.connection.execute(
            "SELECT COUNT(*) FROM local_items WHERE is_in_queue = 1"
        ).fetchone()[0]

        result = saved_queue_rows(self.db)

        self.assertEqual(len(stored), 6)
        self.assertEqual(len(result.rows), 6)
        self.assertEqual(column_flagged, 8)
        self.assertNotEqual(len(result.rows), column_flagged)

    def test_saved_queue_keeps_stored_order_and_has_no_duplicates(self):
        stored = [
            str(r["item_id"])
            for r in self.db.connection.execute(
                "SELECT item_id FROM queue_order "
                "WHERE session_id = 'local' COLLATE NOCASE ORDER BY ordinal"
            )
        ]
        result = saved_queue_rows(self.db)
        ids = [q.item_id for q in result.queue]
        flags = [q.is_play_next for q in result.queue]
        positions = [
            (0 if q.is_play_next else 1, stored.index(q.item_id)) for q in result.queue
        ]

        self.assertEqual(len(ids), len(set(ids)))
        self.assertEqual(flags, sorted(flags, reverse=True))
        self.assertEqual(positions, sorted(positions))

    def test_saved_queue_never_mixes_other_sessions(self):
        total = self.db.connection.execute(
            "SELECT COUNT(*) FROM queue_order"
        ).fetchone()[0]
        self.assertGreater(total, len(saved_queue_rows(self.db).rows))

    # --- zakladki ----------------------------------------------------------

    def _bookmarked_item_ids(self):
        return [
            str(r["item_id"])
            for r in self.db.connection.execute(
                "SELECT DISTINCT item_id FROM bookmarks "
                "WHERE session_id = 'local' COLLATE NOCASE ORDER BY item_id"
            )
        ]

    def test_every_bookmarked_item_reads_in_stored_order_without_loss(self):
        """CALA wlasciwa czesc: kazdy lokalny element majacy zakladki.

        Zmierzone: 12 elementow, 20 wierszy, zero bledow porzadku, zero
        kolizji Id wiersza. Zadna zakladka nie ginie i zadna nie przychodzi
        z podcastow (4975 wierszy innych sesji w tej samej tabeli).
        """
        item_ids = self._bookmarked_item_ids()
        expected_total = self.db.connection.execute(
            "SELECT COUNT(*) FROM bookmarks WHERE session_id = 'local' "
            "COLLATE NOCASE AND (purpose & 1) != 0"
        ).fetchone()[0]

        returned = 0
        row_ids = []
        for item_id in item_ids:
            result = bookmark_rows(self.db, item_id=item_id)
            keys = [(b.position_ticks, b.created_utc_ticks) for b in result.bookmarks]
            self.assertEqual(keys, sorted(keys), f"zly porzadek dla {item_id!r}")
            self.assertTrue(all(b.item_id == item_id for b in result.bookmarks))
            returned += len(result.rows)
            row_ids.extend(row.item_id for row in result.rows)

        self.assertEqual(len(item_ids), 12)
        self.assertEqual(returned, expected_total)
        self.assertEqual(returned, 20)
        self.assertEqual(len(row_ids), len(set(row_ids)))

    def test_bookmarks_of_other_sessions_stay_out(self):
        total = self.db.connection.execute(
            "SELECT COUNT(*) FROM bookmarks"
        ).fetchone()[0]
        local = self.db.connection.execute(
            "SELECT COUNT(*) FROM bookmarks WHERE session_id = 'local' COLLATE NOCASE"
        ).fetchone()[0]

        self.assertEqual(total, 5000)
        self.assertEqual(local, 20)

    def test_unknown_item_id_is_empty_not_an_error(self):
        result = bookmark_rows(self.db, item_id="local-nie-ma-takiego-id")
        self.assertTrue(result.is_empty)


if __name__ == "__main__":
    unittest.main()
