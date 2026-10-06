"""Odczyt ZAPISANEJ kolejnosci Biblioteki i Ulubionych Radia.

Co tu jest mierzone
-------------------
``radio_views._station_view`` oddawalo wiersze w kolejnosci CACHE'U
``radio.stations``, a AMC pokazuje je w kolejnosci ZAPISANEJ w profilu.
Te testy pilnuja, zeby wxPython czytalo ten sam zapis co oryginal:

* tryb z ``sessionNavigation.sessions["radio"].collectionSortModes[widok]``
  (``AppSettings.cs:1190``, czytany przez ``MainWindow.xaml.cs:13191-13197``),
  domyslnie ``AddedNewest``,
* kolejnosc z ``library.db``: ``favorite_order`` / ``favorite_added_order``
  dla Ulubionych i ``library_custom_order`` / ``library_added_order`` dla
  Biblioteki (``LocalLibraryDatabase.cs:232-259``), klucz sesji ``radio``
  (``CollectionOrderStorageKey``, ``MainWindow.xaml.cs:13313-13316``).

Czego tu NIE ma
---------------
Zadnego skrotu zmieniajacego tryb i zadnego zapisu. Tryb jest CZYTANY,
nie wybierany tutaj -- wlascicielem wyboru zostaje AMC.
"""

from __future__ import annotations

import base64
import json
import sqlite3
import sys
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite.collation import (  # noqa: E402
    COLLATION_TITLE_IGNORE_CASE,
    HostCollation,
)
from amc_wx_lite.profile_layout import ProfileLayout, ProfileMode  # noqa: E402
from amc_wx_lite.radio_source import RadioSource  # noqa: E402
from amc_wx_lite.radio_views import (  # noqa: E402
    RADIO_SESSION,
    SORT_ADDED_NEWEST,
    SORT_ALPHABETICAL,
    SORT_CUSTOM,
    VIEW_FAVORITES,
    VIEW_LIBRARY,
    load_view,
)

#: Tylko tabele kolejnosci -- ten test nie dotyka ``local_items``.
SCHEMA = """
CREATE TABLE playback_history (session_id TEXT, ordinal INTEGER, item_id TEXT);
CREATE TABLE favorite_order (session_id TEXT, ordinal INTEGER, item_id TEXT);
CREATE TABLE favorite_added_order (session_id TEXT, ordinal INTEGER, item_id TEXT);
CREATE TABLE library_added_order (session_id TEXT, ordinal INTEGER, item_id TEXT);
CREATE TABLE library_custom_order (session_id TEXT, ordinal INTEGER, item_id TEXT);
"""

#: Cache radia CELOWO w innej kolejnosci niz kazdy zapis ponizej.
#: Biblioteka = {a, b, c}, Ulubione = {b, c, d} -- zakresy sie nie pokrywaja.
STATIONS = [
    {"id": "c", "name": "Czworka", "streamUrl": "http://c", "isInLibrary": True,
     "isFavorite": True},
    {"id": "a", "name": "Alfa", "streamUrl": "http://a", "isInLibrary": True},
    {"id": "b", "name": "Beta", "streamUrl": "http://b", "isInLibrary": True,
     "isFavorite": True},
    # Ulubiona POZA Biblioteka: zakresy musza zostac niezalezne.
    {"id": "d", "name": "Delta", "streamUrl": "http://d", "isFavorite": True},
]


def _state(sort_modes: dict | None = None) -> dict:
    raw = {
        "schemaVersion": 54,
        "radio": {"currentItemId": "b", "stations": STATIONS},
    }
    if sort_modes is not None:
        raw["sessionNavigation"] = {
            "sessions": {RADIO_SESSION: {"collectionSortModes": sort_modes}}
        }
    return raw


def _ids(result) -> list[str]:
    return [row.item_id for row in result.rows]


def _fake_title_collation(titles) -> HostCollation:
    """Kolacja-atrapa: klucz = ``casefold`` w UTF-8.

    NIE udaje zgodnosci z ``CompareInfo`` pl-PL -- sluzy tylko temu, zeby
    sprawdzic, ze widok UZYWA drogi kluczy hosta i wlasciwego trybu.
    """
    values = list(dict.fromkeys(titles))
    return HostCollation.from_payload(
        {
            "titles": values,
            "keys": [base64.b64encode(v.casefold().encode("utf-8")).decode("ascii")
                     for v in values],
            "mode": COLLATION_TITLE_IGNORE_CASE,
        }
    )


class RadioOrderBase(unittest.TestCase):
    """Wspolny mirror AMC, TYLKO do odczytu. Produkcji nie czytamy."""

    SORT_MODES: dict | None = None

    def setUp(self) -> None:
        self._tmp = TemporaryDirectory()
        self.dir = Path(self._tmp.name)
        self.state_json = self.dir / "state.json"
        self.state_json.write_text(
            json.dumps(_state(self.SORT_MODES), ensure_ascii=False), encoding="utf-8"
        )
        self.db_path = self.dir / "library.db"
        con = sqlite3.connect(self.db_path)
        con.executescript(SCHEMA)
        # Kazdy zapis INNY, zeby pomylka tabeli byla widoczna.
        # "z" nie istnieje w cache'u (pozycja usunieta z profilu);
        # "a" / "d" nie ma w zapisie (pozycja DODANA po ostatnim zapisie).
        self._seed(con, "favorite_order", ["c", "z", "b"])
        self._seed(con, "favorite_added_order", ["b", "c"])
        self._seed(con, "library_custom_order", ["b", "a", "c"])
        self._seed(con, "library_added_order", ["c", "b"])
        # Obca sesja nie moze wyciec do radia.
        self._seed(con, "favorite_order", ["d"], session="local")
        con.commit()
        con.close()

        self.layout = ProfileLayout(
            mode=ProfileMode.READ_ONLY_MIRROR,
            library_db=self.db_path,
            podcasts_db=self.dir / "podcasts.db",
            state_json=self.state_json,
            lite_settings_dir=self.dir / "lite",
        )
        self.source = RadioSource(self.layout)
        self._state_before = (
            self.state_json.read_bytes(), self.state_json.stat().st_mtime_ns)
        self._db_before = (self.db_path.read_bytes(), self.db_path.stat().st_mtime_ns)

    @staticmethod
    def _seed(con, table: str, ids: list[str], *, session: str = RADIO_SESSION) -> None:
        for ordinal, item_id in enumerate(ids):
            con.execute(
                f"INSERT INTO {table}(session_id, ordinal, item_id) VALUES (?,?,?)",
                (session, ordinal, item_id),
            )

    def tearDown(self) -> None:
        self._tmp.cleanup()

    def assert_nothing_written(self) -> None:
        self.assertEqual(
            (self.state_json.read_bytes(), self.state_json.stat().st_mtime_ns),
            self._state_before,
            "odczyt kolejnosci zmienil state.json",
        )
        self.assertEqual(
            (self.db_path.read_bytes(), self.db_path.stat().st_mtime_ns),
            self._db_before,
            "odczyt kolejnosci zmienil library.db",
        )


class TestDefaultModeIsAddedNewest(RadioOrderBase):
    """Brak zapisanego trybu = ``AddedNewest`` (``MainWindow.xaml.cs:13193-13196``)."""

    SORT_MODES = None

    def test_library_uses_library_added_order_reversed(self) -> None:
        # Zapis ["c","b"], "a" dopisana na koniec przez Normalize, potem Reverse.
        result = load_view(self.source, VIEW_LIBRARY)
        self.assertEqual(_ids(result), ["a", "b", "c"])
        self.assertTrue(result.order_matches_amc)
        self.assertIsNone(result.unavailable_reason)
        self.assert_nothing_written()

    def test_favorites_use_favorite_added_order_reversed(self) -> None:
        # Zapis ["b","c"], "d" dopisana na koniec, potem Reverse.
        result = load_view(self.source, VIEW_FAVORITES)
        self.assertEqual(_ids(result), ["d", "c", "b"])
        self.assertTrue(result.order_matches_amc)
        self.assert_nothing_written()

    def test_cache_order_is_not_the_answer(self) -> None:
        """Gdyby cache wygral, Biblioteka byla by ["c","a","b"]."""
        self.assertNotEqual(_ids(load_view(self.source, VIEW_LIBRARY)), ["c", "a", "b"])

    def test_views_read_different_tables(self) -> None:
        self.assertNotEqual(
            _ids(load_view(self.source, VIEW_LIBRARY)),
            _ids(load_view(self.source, VIEW_FAVORITES)),
        )


class TestSavedCustom(RadioOrderBase):
    """``Custom`` = zapis wlasny BEZ odwracania."""

    SORT_MODES = {"Biblioteka": "Custom", "Ulubione": "Custom"}

    def test_library_custom_order_is_not_reversed(self) -> None:
        result = load_view(self.source, VIEW_LIBRARY)
        self.assertEqual(_ids(result), ["b", "a", "c"])
        self.assertTrue(result.order_matches_amc)
        self.assert_nothing_written()

    def test_favorites_custom_order_skips_unknown_and_appends_new(self) -> None:
        # Zapis ["c","z","b"]: "z" nie ma w cache'u -> NIE tworzy wiersza,
        # "d" nie ma w zapisie -> laduje na koncu (Normalize), bez Reverse.
        result = load_view(self.source, VIEW_FAVORITES)
        self.assertEqual(_ids(result), ["c", "b", "d"])
        self.assert_nothing_written()

    def test_unknown_id_is_not_deleted_from_profile(self) -> None:
        load_view(self.source, VIEW_FAVORITES)
        con = sqlite3.connect(self.db_path)
        try:
            kept = con.execute(
                "SELECT COUNT(*) FROM favorite_order WHERE item_id = 'z'"
            ).fetchone()[0]
        finally:
            con.close()
        self.assertEqual(kept, 1, "odczyt skasowal zapisane Id")

    def test_mode_name_is_case_insensitive(self) -> None:
        """C# trzyma ``collectionSortModes`` w slowniku OrdinalIgnoreCase."""
        self.state_json.write_text(
            json.dumps(_state({"biblioteka": "custom"}), ensure_ascii=False),
            encoding="utf-8",
        )
        self.assertEqual(_ids(load_view(self.source, VIEW_LIBRARY)), ["b", "a", "c"])

    def test_other_session_order_does_not_leak(self) -> None:
        self.assertNotEqual(_ids(load_view(self.source, VIEW_FAVORITES))[0], "d")


class TestSavedAlphabetical(RadioOrderBase):
    """``Alphabetical`` idzie droga kluczy hosta, nie ``str.casefold`` w widoku."""

    SORT_MODES = {"Biblioteka": "Alphabetical", "Ulubione": "Alphabetical"}

    def test_alphabetical_uses_host_keys(self) -> None:
        collation = _fake_title_collation([s["name"] for s in STATIONS])
        result = load_view(self.source, VIEW_LIBRARY, collation=collation)
        # Alfa, Beta, Czworka
        self.assertEqual(_ids(result), ["a", "b", "c"])
        self.assertTrue(result.order_matches_amc)
        self.assert_nothing_written()

    def test_without_host_keys_order_is_named_as_substitute(self) -> None:
        """Brak hosta nie moze ani sklamac zgodnosci, ani zabrac listy."""
        result = load_view(self.source, VIEW_LIBRARY)
        self.assertEqual(sorted(_ids(result)), ["a", "b", "c"])
        self.assertFalse(result.order_matches_amc)
        self.assert_nothing_written()


class TestOrderStoreUnavailable(RadioOrderBase):
    """Brak opcjonalnego sortu NIE moze zabrac calej listy."""

    SORT_MODES = None

    def test_missing_database_keeps_rows_and_tells_truth(self) -> None:
        self.db_path.unlink()
        result = load_view(self.source, VIEW_LIBRARY)
        self.assertEqual(sorted(_ids(result)), ["a", "b", "c"])
        self.assertFalse(result.order_matches_amc)

    def test_missing_order_table_keeps_rows_and_tells_truth(self) -> None:
        con = sqlite3.connect(self.db_path)
        con.execute("DROP TABLE library_added_order")
        con.commit()
        con.close()
        result = load_view(self.source, VIEW_LIBRARY)
        self.assertEqual(sorted(_ids(result)), ["a", "b", "c"])
        self.assertFalse(result.order_matches_amc)

    def test_caller_db_is_not_closed(self) -> None:
        from amc_wx_lite.library_db import LibraryDatabase

        db = LibraryDatabase(self.db_path)
        try:
            load_view(self.source, VIEW_LIBRARY, db=db)
            db.connection.execute("SELECT COUNT(*) FROM library_added_order").fetchone()
        finally:
            db.close()

    def test_one_json_read_per_view(self) -> None:
        """Duzy ``state.json`` czytamy RAZ na widok, nie raz na kolejnosc."""
        reads = 0
        original = Path.read_text

        def counting(self_path, *args, **kwargs):
            nonlocal reads
            if self_path == self.state_json:
                reads += 1
            return original(self_path, *args, **kwargs)

        Path.read_text = counting
        try:
            load_view(self.source, VIEW_LIBRARY)
        finally:
            Path.read_text = original
        self.assertEqual(reads, 1)


class TestGuiCallerPassesSavedOrder(RadioOrderBase):
    """Bramka kontraktu: GUI MUSI podac kolacje i oddac werdykt kolejnosci.

    Sam ``load_view`` z poprawna kolejnoscia jest bezuzyteczny, jesli okno go
    tak nie wola. Czytamy wiec ZRODLO ``gui.py`` i ``navigation.py`` --
    ``wx`` nie da sie zaimportowac w tym headless WSL.
    """

    SORT_MODES = None

    def _source(self, name: str) -> str:
        return (
            Path(__file__).resolve().parents[1] / "amc_wx_lite" / name
        ).read_text(encoding="utf-8")

    def test_gui_passes_collation_to_radio_loader(self) -> None:
        self.assertIn("load_view(source, scope, collation=collation)",
                      self._source("gui.py"))

    def test_gui_keeps_collation_after_engine_start(self) -> None:
        self.assertIn("self._collation = collation", self._source("gui.py"))

    def test_gui_forwards_order_verdict_to_navigator(self) -> None:
        self.assertIn("order_matches_amc=result.order_matches_amc",
                      self._source("gui.py"))

    def test_navigator_names_substitute_order(self) -> None:
        self.assertIn("kolejność zastępcza", self._source("navigation.py"))

    def test_navigator_accepts_the_verdict(self) -> None:
        from amc_wx_lite.navigation import Navigator

        import inspect

        signature = inspect.signature(Navigator.apply_radio_view)
        self.assertIn("order_matches_amc", signature.parameters)


class TestSortModeNames(unittest.TestCase):
    def test_mode_names_match_csharp_enum(self) -> None:
        """``CollectionSortMode`` (``AppSettings.cs:27-32``) serializuje sie nazwa."""
        self.assertEqual(
            (SORT_ADDED_NEWEST, SORT_ALPHABETICAL, SORT_CUSTOM),
            ("AddedNewest", "Alphabetical", "Custom"),
        )


if __name__ == "__main__":
    unittest.main()
