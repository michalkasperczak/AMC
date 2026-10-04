"""Testy warstwy danych kolejnych widokow Biblioteki.

Dwie warstwy pomiaru, celowo rozdzielone:

* rogi na SYNTETYCZNEJ bazie o PRAWDZIWYM schemacie (duplikaty nazw, brak
  pozycji, pusta lista, polskie tytuly, ID jako napis, swiezy odczyt po
  commicie WAL przy OTWARTYM writerze),
* przebieg na PELNEJ kopii profilu -- osobny plik kwitu, bez tytulow.

Czego te testy NIE dowodza: nie uruchamiaja oryginalnego C#. Sprawdzaja, ze
NASZ kod robi to, co PRZECZYTANA logika zrodlowa (cytaty w
``LIBRARY_VIEWS_CONTRACT.md``). Zgodnosc kolejnosci alfabetycznej z .NET jest
zmierzona osobno, sonda ``probe-csharp-order``.
"""

from __future__ import annotations

import os
import sqlite3
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite.library_db import LibraryDatabase  # noqa: E402
from amc_wx_lite.library_views import (  # noqa: E402
    LOCAL_SESSION,
    PLAYLISTS_VIEW,
    active_items,
    all_files_rows,
    favorite_rows,
    playlist_contents_rows,
    playlist_id_from_view,
    playlist_rows,
    view_name_for_playlist,
)

FIXTURE = Path(
    os.environ.get(
        "AMC_WX_FIXTURE",
        "/home/michal/projekty/amc_pomoc/wx-full-profile-after421/fixture",
    )
)
LIBRARY_DB = FIXTURE / "library.db"


def _ordinal(left: str, right: str) -> int:
    return (left > right) - (left < right)


class FakeCollation:
    """Atrapa ``HostCollation`` o TYM SAMYM interfejsie (``load``/``key_for``).

    Klucz: ``casefold`` + zlozenie polskich znakow. To NIE jest nowy kolator
    produkcyjny -- sluzy wylacznie do sprawdzenia, ze ``all_files_rows``
    UZYWA kluczy hosta i tie-breaka po sciezce. Zgodnosc z .NET mierzy sonda.
    """

    _FOLD = str.maketrans("ąćęłńóśźż", "acelnoszz")

    def __init__(self) -> None:
        self._keys: dict[str, bytes] = {}
        self.load_calls = 0

    def load(self, titles) -> None:
        self.load_calls += 1
        for title in titles:
            self._keys[title] = title.casefold().translate(self._FOLD).encode()

    def key_for(self, title: str) -> bytes | None:
        return self._keys.get(title)


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
CREATE TABLE favorite_order (
    session_id TEXT NOT NULL, ordinal INTEGER NOT NULL, item_id TEXT NOT NULL,
    PRIMARY KEY(session_id, ordinal)
);
CREATE TABLE favorite_added_order (
    session_id TEXT NOT NULL, ordinal INTEGER NOT NULL, item_id TEXT NOT NULL,
    PRIMARY KEY(session_id, ordinal)
);
CREATE TABLE playlists (
    id TEXT PRIMARY KEY, session_id TEXT NOT NULL, ordinal INTEGER NOT NULL,
    name TEXT NOT NULL COLLATE AMC_PL, created_utc_ticks INTEGER NOT NULL
);
CREATE TABLE playlist_items (
    playlist_id TEXT NOT NULL, ordinal INTEGER NOT NULL, item_id TEXT NOT NULL,
    PRIMARY KEY(playlist_id, ordinal), UNIQUE(playlist_id, item_id)
);
CREATE TABLE metadata (key TEXT PRIMARY KEY, value TEXT NOT NULL);
"""


class _Builder:
    """Pisze baze o PRAWDZIWYM schemacie. Writer zostaje OTWARTY (tryb WAL)."""

    def __init__(self, path: Path) -> None:
        self.connection = sqlite3.connect(path, isolation_level=None)
        self.connection.create_collation("AMC_PL", _ordinal)
        self.connection.execute("PRAGMA journal_mode=WAL").fetchone()
        self.connection.executescript(_SCHEMA)
        self._n = 0

    def item(
        self,
        item_id: str,
        title: str,
        path: str,
        *,
        favorite: bool = False,
        available: bool = True,
        in_library: bool = True,
        ticks: int = 0,
    ) -> str:
        self.connection.execute(
            "INSERT INTO local_items (id, title, has_custom_title, path, "
            "duration_ticks, bitrate_estimated, is_favorite, is_in_library, "
            "is_available, is_in_queue, is_play_next, resume_mode, "
            "resume_position_ticks, is_radio_recording) "
            "VALUES (?, ?, 0, ?, ?, 0, ?, ?, ?, 0, 0, 0, 0, 0)",
            (item_id, title, path, ticks, int(favorite), int(in_library), int(available)),
        )
        return item_id

    def favorites_added(self, *item_ids: str, session: str = LOCAL_SESSION) -> None:
        for ordinal, item_id in enumerate(item_ids):
            self.connection.execute(
                "INSERT INTO favorite_added_order(session_id, ordinal, item_id) "
                "VALUES (?, ?, ?)",
                (session, ordinal, item_id),
            )

    def favorites_custom(self, *item_ids: str, session: str = LOCAL_SESSION) -> None:
        for ordinal, item_id in enumerate(item_ids):
            self.connection.execute(
                "INSERT INTO favorite_order(session_id, ordinal, item_id) "
                "VALUES (?, ?, ?)",
                (session, ordinal, item_id),
            )

    def playlist(
        self, playlist_id: str, name: str, *item_ids: str, session: str = LOCAL_SESSION
    ) -> str:
        self.connection.execute(
            "INSERT INTO playlists(id, session_id, ordinal, name, created_utc_ticks) "
            "VALUES (?, ?, ?, ?, 0)",
            (playlist_id, session, self._n, name),
        )
        self._n += 1
        for ordinal, item_id in enumerate(item_ids):
            self.connection.execute(
                "INSERT INTO playlist_items(playlist_id, ordinal, item_id) "
                "VALUES (?, ?, ?)",
                (playlist_id, ordinal, item_id),
            )
        return playlist_id

    def close(self) -> None:
        self.connection.close()


class _SyntheticCase(unittest.TestCase):
    """Wspolna, mala baza o prawdziwym schemacie."""

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


class AllFilesView(_SyntheticCase):
    def test_sorts_by_host_keys_then_by_path(self):
        """Tytul przez klucze AMC_PL, remis po sciezce ``OrdinalIgnoreCase``."""
        self.build.item("b", "Żuraw", "C:/m/2.mp3")
        self.build.item("a", "Łąka", "C:/m/1.mp3")
        self.build.item("d", "Echo", "C:/m/b.mp3")
        self.build.item("c", "Echo", "C:/m/A.mp3")  # ten sam tytul, inna sciezka
        result = all_files_rows(self.open_db(), FakeCollation())
        self.assertEqual(
            [r.item_id for r in result.rows],
            ["c", "d", "a", "b"],
            "Echo(A) przed Echo(b) -- OrdinalIgnoreCase, potem Łąka, potem Żuraw",
        )
        self.assertTrue(result.order_matches_amc)
        self.assertEqual(result.heading, "Biblioteka — Wszystkie pliki")

    def test_duplicate_titles_keep_both_rows_with_distinct_ids(self):
        """Duplikat NAZWY to nie duplikat pozycji -- oba wiersze zostaja."""
        self.build.item("x1", "Ten sam tytul", "C:/m/a.mp3")
        self.build.item("x2", "Ten sam tytul", "C:/m/b.mp3")
        rows = all_files_rows(self.open_db(), FakeCollation()).rows
        self.assertEqual([r.item_id for r in rows], ["x1", "x2"])
        self.assertEqual(len({r.item_id for r in rows}), 2)

    def test_skips_unavailable_and_out_of_library(self):
        """``ActiveLocalItems`` = ``IsAvailable && IsInLibrary`` i nic wiecej."""
        self.build.item("ok", "Dostepny", "C:/m/1.mp3")
        self.build.item("gone", "Brak pliku", "C:/m/2.mp3", available=False)
        self.build.item("out", "Poza biblioteka", "C:/m/3.mp3", in_library=False)
        rows = all_files_rows(self.open_db(), FakeCollation()).rows
        self.assertEqual([r.item_id for r in rows], ["ok"])

    def test_without_host_keys_order_is_declared_substitute(self):
        """Bez hosta NIE udajemy zgodnosci -- sygnal degradacji jak w snapshot."""
        self.build.item("a", "Żuraw", "C:/m/1.mp3")
        self.build.item("b", "Łąka", "C:/m/2.mp3")
        result = all_files_rows(self.open_db(), None)
        self.assertFalse(result.order_matches_amc)
        self.assertEqual(len(result.rows), 2)

    def test_empty_library_is_empty_not_error(self):
        result = all_files_rows(self.open_db(), FakeCollation())
        self.assertTrue(result.is_empty)
        self.assertEqual(result.rows, [])

    def test_item_ids_are_strings_even_when_numeric(self):
        """ID trwale i jako NAPIS -- inaczej wybor po Id gubi sie po odswiezeniu."""
        self.build.item("12345", "Numeryczny", "C:/m/1.mp3")
        row = all_files_rows(self.open_db(), FakeCollation()).rows[0]
        self.assertIsInstance(row.item_id, str)
        self.assertEqual(row.item_id, "12345")

    def test_host_keys_are_fetched_in_one_batch(self):
        """Jedno ``load`` na widok, a nie porownywarka IPC na kazda pare."""
        for n in range(20):
            self.build.item(f"i{n}", f"Tytul {n}", f"C:/m/{n}.mp3")
        collation = FakeCollation()
        all_files_rows(self.open_db(), collation)
        self.assertEqual(collation.load_calls, 1)


class FavoritesView(_SyntheticCase):
    def test_default_order_is_added_order_reversed(self):
        """``AddedNewest`` = ``Order(favorite_added_order)`` + ``.Reverse()``."""
        for key in ("a", "b", "c"):
            self.build.item(key, f"Utwor {key}", f"C:/m/{key}.mp3", favorite=True)
        self.build.favorites_added("a", "b", "c")
        result = favorite_rows(self.open_db())
        self.assertEqual([r.item_id for r in result.rows], ["c", "b", "a"])
        self.assertEqual(result.heading, "Ulubione")

    def test_items_missing_from_stored_order_come_first_after_reverse(self):
        """``Order`` daje nieznanym ``int.MaxValue``; po ``Reverse`` sa na przodzie."""
        for key in ("a", "b", "nowy"):
            self.build.item(key, f"Utwor {key}", f"C:/m/{key}.mp3", favorite=True)
        self.build.favorites_added("a", "b")  # "nowy" nie ma wpisu
        rows = favorite_rows(self.open_db()).rows
        self.assertEqual([r.item_id for r in rows], ["nowy", "b", "a"])

    def test_only_active_favorites(self):
        """Ulubione czytaja katalog sesji, a ten jest juz przefiltrowany."""
        self.build.item("ok", "Ulubiony", "C:/m/1.mp3", favorite=True)
        self.build.item("gone", "Zniknal", "C:/m/2.mp3", favorite=True, available=False)
        self.build.item("zwykly", "Nieulubiony", "C:/m/3.mp3")
        self.build.favorites_added("ok", "gone")
        rows = favorite_rows(self.open_db()).rows
        self.assertEqual([r.item_id for r in rows], ["ok"])

    def test_custom_order_uses_favorite_order_without_reverse(self):
        """Alt+3: ``favorite_order``, BEZ odwracania."""
        for key in ("a", "b", "c"):
            self.build.item(key, f"Utwor {key}", f"C:/m/{key}.mp3", favorite=True)
        self.build.favorites_custom("c", "a", "b")
        rows = favorite_rows(self.open_db(), order="custom").rows
        self.assertEqual([r.item_id for r in rows], ["c", "a", "b"])

    def test_stale_ids_in_stored_order_do_not_create_rows(self):
        """Zapis moze pamietac Id, ktorego juz nie ma -- nie wymyslamy wiersza."""
        self.build.item("a", "Jest", "C:/m/1.mp3", favorite=True)
        self.build.favorites_added("duch", "a")
        rows = favorite_rows(self.open_db()).rows
        self.assertEqual([r.item_id for r in rows], ["a"])

    def test_empty_favorites(self):
        self.build.item("a", "Nieulubiony", "C:/m/1.mp3")
        self.assertTrue(favorite_rows(self.open_db()).is_empty)

    def test_favorites_do_not_depend_on_host_collation(self):
        """Ulubione NIE sortuja sie alfabetycznie, wiec host nie jest potrzebny."""
        self.build.item("a", "Zzz", "C:/m/1.mp3", favorite=True)
        self.build.item("b", "Aaa", "C:/m/2.mp3", favorite=True)
        self.build.favorites_added("a", "b")
        result = favorite_rows(self.open_db())
        self.assertEqual([r.item_id for r in result.rows], ["b", "a"])
        self.assertTrue(result.order_matches_amc, "brak sortu = brak degradacji")


class PlaylistsView(_SyntheticCase):
    def test_lists_local_playlists_in_stored_ordinal_order(self):
        """``ORDER BY session_id, ordinal`` -- NIE alfabetycznie po nazwie."""
        self.build.playlist("p1", "Zima")
        self.build.playlist("p2", "Alfabetycznie pierwsza")
        result = playlist_rows(self.open_db())
        self.assertEqual([p.playlist_id for p in result.playlists], ["p1", "p2"])
        self.assertEqual(result.heading, "Playlisty")

    def test_playlists_of_other_sessions_are_not_listed(self):
        self.build.playlist("local1", "Moja")
        self.build.playlist("t1", "TIDAL-owa", session="tidal")
        result = playlist_rows(self.open_db())
        self.assertEqual([p.playlist_id for p in result.playlists], ["local1"])

    def test_row_id_is_prefixed_string_and_stable(self):
        """``$"playlist:{id}"`` -- stabilne Id do powrotu i wyboru."""
        self.build.playlist("abc", "Nazwa")
        row = playlist_rows(self.open_db()).rows[0]
        self.assertEqual(row.item_id, "playlist:abc")
        self.assertEqual(row.kind, "playlist")
        self.assertIsInstance(row.item_id, str)

    def test_label_counts_available_vs_stored(self):
        """Pozycja niedostepna -> ``dostepne X z Y``, nie ciche skrocenie."""
        self.build.item("a", "Jest", "C:/m/1.mp3", ticks=60 * 10_000_000)
        self.build.item("b", "Znikl", "C:/m/2.mp3", available=False)
        self.build.playlist("p", "Mieszana", "a", "b")
        entry = playlist_rows(self.open_db()).playlists[0]
        self.assertEqual(entry.stored_count, 2)
        self.assertEqual(entry.available_count, 1)
        self.assertIn("dostepne 1 z 2", entry.row.detail.replace("ę", "e"))

    def test_empty_playlist_label_has_no_duration(self):
        """``storedItemCount == 0`` -> etykieta konczy sie na liczbie elementow."""
        self.build.playlist("p", "Pusta")
        entry = playlist_rows(self.open_db()).playlists[0]
        self.assertEqual(entry.stored_count, 0)
        self.assertNotIn("czas", entry.row.detail)

    def test_duplicate_playlist_names_stay_separate_rows(self):
        """Unikalnosc nazw jest wymuszana przy ZAPISIE; odczyt ma byc odporny."""
        self.build.playlist("p1", "Ta sama")
        self.build.playlist("p2", "Ta sama")
        rows = playlist_rows(self.open_db()).rows
        self.assertEqual([r.item_id for r in rows], ["playlist:p1", "playlist:p2"])

    def test_no_playlists_is_empty_not_error(self):
        self.assertTrue(playlist_rows(self.open_db()).is_empty)

    def test_duration_saturates_instead_of_overflowing(self):
        """``CreatePlaylistRows`` (12813-12819) sumuje czas z nasyceniem.

        C# uzywa ``Aggregate`` z jawnym warunkiem
        ``item.Duration.Ticks > long.MaxValue - total ? long.MaxValue : ...``.
        Python ma liczby dowolnej precyzji, wiec BEZ tego warunku cicho
        przekroczylby ``long.MaxValue`` i oddal liczbe, ktorej oryginal nigdy
        nie zwroci.
        """
        long_max = 2**63 - 1
        self.build.item("a", "A", "C:/m/1.mp3", ticks=long_max - 5)
        self.build.item("b", "B", "C:/m/2.mp3", ticks=1000)
        self.build.playlist("p1", "Dluga", "a", "b")
        entry = playlist_rows(self.open_db()).playlists[0]
        self.assertEqual(long_max, entry.duration_ticks)

    def test_duration_skips_unavailable_items(self):
        self.build.item("a", "Jest", "C:/m/1.mp3", ticks=30 * 10_000_000)
        self.build.item("b", "Znikl", "C:/m/2.mp3", available=False, ticks=999)
        self.build.playlist("p", "P", "a", "b")
        entry = playlist_rows(self.open_db()).playlists[0]
        self.assertEqual(entry.duration_ticks, 30 * 10_000_000)


class PlaylistContentsView(_SyntheticCase):
    def test_keeps_stored_ordinal_order_not_alphabetical(self):
        self.build.item("z", "Zzz", "C:/m/1.mp3")
        self.build.item("a", "Aaa", "C:/m/2.mp3")
        self.build.playlist("p", "Moja", "z", "a")
        result = playlist_contents_rows(self.open_db(), "p")
        self.assertEqual([r.item_id for r in result.rows], ["z", "a"])
        self.assertEqual(result.heading, "Playlista — Moja")

    def test_missing_items_are_skipped_silently(self):
        """``Where(item is not null)`` -- brak wiersza zastepczego."""
        self.build.item("a", "Jest", "C:/m/1.mp3")
        self.build.item("b", "Znikl", "C:/m/2.mp3", available=False)
        self.build.playlist("p", "P", "a", "b", "duch")
        rows = playlist_contents_rows(self.open_db(), "p").rows
        self.assertEqual([r.item_id for r in rows], ["a"])

    def test_unknown_playlist_falls_back_to_playlists_view(self):
        """AMC wraca do "Playlisty", wiec warstwa danych ma to zglosic."""
        result = playlist_contents_rows(self.open_db(), "nie-ma")
        self.assertEqual(result.fallback_view, PLAYLISTS_VIEW)
        self.assertTrue(result.is_empty)

    def test_playlist_of_another_session_falls_back(self):
        self.build.playlist("t1", "TIDAL", session="tidal")
        result = playlist_contents_rows(self.open_db(), "t1")
        self.assertEqual(result.fallback_view, PLAYLISTS_VIEW)

    def test_empty_playlist_is_empty_without_fallback(self):
        """Pusta playlista ISTNIEJE -- to nie to samo, co playlista znikla."""
        self.build.playlist("p", "Pusta")
        result = playlist_contents_rows(self.open_db(), "p")
        self.assertTrue(result.is_empty)
        self.assertIsNone(result.fallback_view)

    def test_polish_name_in_heading(self):
        self.build.playlist("p", "Zażółć gęślą jaźń")
        result = playlist_contents_rows(self.open_db(), "p")
        self.assertEqual(result.heading, "Playlista — Zażółć gęślą jaźń")


class ViewNameParsing(unittest.TestCase):
    def test_round_trip(self):
        self.assertEqual(playlist_id_from_view(view_name_for_playlist("abc")), "abc")

    def test_prefix_without_id_is_not_a_playlist_view(self):
        """``Length > prefix.Length`` -- sam prefiks to NIE widok playlisty."""
        self.assertIsNone(playlist_id_from_view("Playlista:"))

    def test_other_views_are_rejected(self):
        for name in ("Playlisty", "Wszystkie pliki", "Ulubione", "playlista:x"):
            self.assertIsNone(playlist_id_from_view(name), name)

    def test_id_with_colon_survives(self):
        self.assertEqual(playlist_id_from_view("Playlista:a:b"), "a:b")


class FreshReadAfterLiveCommit(_SyntheticCase):
    """Writer zostaje OTWARTY -- zmiana siedzi w ``-wal``, tak jak w AMC."""

    def test_new_favorite_is_visible_to_a_fresh_read(self):
        self.build.item("a", "Pierwszy", "C:/m/1.mp3", favorite=True)
        self.build.favorites_added("a")
        self.assertEqual(len(favorite_rows(self.open_db()).rows), 1)

        self.build.connection.execute("BEGIN")
        self.build.item("b", "Drugi", "C:/m/2.mp3", favorite=True)
        self.build.connection.execute(
            "INSERT INTO favorite_added_order(session_id, ordinal, item_id) "
            "VALUES (?, 1, 'b')",
            (LOCAL_SESSION,),
        )
        self.build.connection.execute("COMMIT")

        rows = favorite_rows(self.open_db()).rows
        self.assertEqual(
            [r.item_id for r in rows],
            ["b", "a"],
            "Swiezy odczyt musi widziec commit ZYWEGO writera (WAL)",
        )

    def test_playlist_item_added_by_live_writer_is_visible(self):
        self.build.item("a", "Pierwszy", "C:/m/1.mp3")
        self.build.item("b", "Drugi", "C:/m/2.mp3")
        self.build.playlist("p", "Moja", "a")
        self.assertEqual(len(playlist_contents_rows(self.open_db(), "p").rows), 1)

        self.build.connection.execute("BEGIN")
        self.build.connection.execute(
            "INSERT INTO playlist_items(playlist_id, ordinal, item_id) "
            "VALUES ('p', 1, 'b')"
        )
        self.build.connection.execute("COMMIT")

        rows = playlist_contents_rows(self.open_db(), "p").rows
        self.assertEqual([r.item_id for r in rows], ["a", "b"])

    def test_selection_survives_refresh_because_ids_are_stable(self):
        """Dowod na brak utraty zaznaczenia: Id tej samej pozycji sie nie zmienia."""
        self.build.item("keep", "Zostaje", "C:/m/1.mp3", favorite=True)
        self.build.favorites_added("keep")
        before = favorite_rows(self.open_db()).rows[0].item_id

        self.build.connection.execute("BEGIN")
        self.build.item("new", "Nowy", "C:/m/2.mp3", favorite=True)
        self.build.connection.execute(
            "INSERT INTO favorite_added_order(session_id, ordinal, item_id) "
            "VALUES (?, 1, 'new')",
            (LOCAL_SESSION,),
        )
        self.build.connection.execute("COMMIT")

        after = favorite_rows(self.open_db()).rows
        self.assertIn(before, [r.item_id for r in after])
        self.assertEqual(
            [r.item_id for r in after].index(before),
            1,
            "Pozycja sie przesuwa, ale Id jest to samo -- wybor da sie odtworzyc",
        )


@unittest.skipUnless(LIBRARY_DB.exists(), f"Brak kopii bazy: {LIBRARY_DB}")
class FullProfileSmoke(unittest.TestCase):
    """Przebieg na PELNEJ kopii profilu. Bez tytulow w asercjach."""

    @classmethod
    def setUpClass(cls) -> None:
        cls.db = LibraryDatabase(LIBRARY_DB)

    @classmethod
    def tearDownClass(cls) -> None:
        cls.db.close()

    def test_all_files_count_matches_active_predicate(self):
        expected = self.db.count_active_items()
        rows = all_files_rows(self.db, None).rows
        self.assertEqual(len(rows), expected)
        self.assertGreater(expected, 1000, "pelna baza, nie atrapa")

    def test_all_ids_are_unique_strings(self):
        rows = all_files_rows(self.db, None).rows
        ids = [r.item_id for r in rows]
        self.assertEqual(len(ids), len(set(ids)))
        self.assertTrue(all(isinstance(i, str) for i in ids))

    def test_favorites_are_subset_of_active_items(self):
        active = {i.id for i in active_items(self.db)}
        favorites = [r.item_id for r in favorite_rows(self.db).rows]
        self.assertTrue(set(favorites) <= active)
        self.assertEqual(len(favorites), len(set(favorites)))

    def test_playlist_contents_are_subset_of_active_items(self):
        active = {i.id for i in active_items(self.db)}
        for entry in playlist_rows(self.db).playlists:
            rows = playlist_contents_rows(self.db, entry.playlist_id).rows
            self.assertTrue(set(r.item_id for r in rows) <= active)
            self.assertEqual(len(rows), entry.available_count)


if __name__ == "__main__":
    unittest.main()
