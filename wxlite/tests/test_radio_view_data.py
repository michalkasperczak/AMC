"""Warstwa DANYCH trzech widokow Radia -- bez GUI, bez ``wx``.

Czego te testy pilnuja (a nie samego ,,nie wybucha'')
----------------------------------------------------
1. **Rozbiezne flagi.** Stacja ``isFavorite=true`` z ``isInLibrary=false`` MUSI
   byc w Ulubionych i NIE moze byc w Bibliotece. To jedyny test, ktory lapie
   podstawienie jednego zakresu pod drugi -- przy zgodnych flagach oba widoki
   wygladaja identycznie i blad przechodzi.
2. **Napis ``"false"``.** W Pythonie jest PRAWDZIWY, wiec filtr przez
   prawdziwosc cicho przepuscilby caly cache. Czytanie ``is True`` jest
   sprawdzane wprost.
3. **Historia jest szersza od Biblioteki.** Wpis odtworzony i nigdy nie dodany
   do Biblioteki zostaje w historii.
4. **Kolejnosc i duplikaty historii.** ``ORDER BY ordinal`` (0 = ostatnio
   odtworzone), ``Distinct`` po pierwszym wystapieniu.
5. **Nieznane Id** liczy sie w ``missing_item_count`` i NIE jest usuwane z bazy.
6. **Blad nie udaje pustki.** Uszkodzony JSON, brak profilu i brak tabeli
   historii daja ``unavailable_reason``, nie udana pusta liste.
7. **PRIVATE_SANDBOX.** Biblioteka prywatnych stacji dziala; Ulubione i
   Historia maja jasny powod niedostepnosci.
8. **Ochrona zapisu.** Po wszystkich odczytach ``state.json`` i ``library.db``
   maja NIEZMIENIONA tresc i mtime, a ``save`` we wspolnym profilu odmawia.
"""

from __future__ import annotations

import json
import sqlite3
import sys
import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from amc_wx_lite.library_db import LibraryDatabase  # noqa: E402
from amc_wx_lite.profile_layout import (  # noqa: E402
    ProfileLayout,
    ProfileMode,
    ProfileWriteDenied,
)
from amc_wx_lite.radio_source import RadioSnapshot, RadioSource  # noqa: E402
from amc_wx_lite.radio_views import (  # noqa: E402
    RADIO_SESSION,
    VIEW_FAVORITES,
    VIEW_HISTORY,
    VIEW_LIBRARY,
    load_view,
)
from amc_wx_lite.state_store import Station, StateStore  # noqa: E402

SCHEMA = """
CREATE TABLE playback_history (session_id TEXT, ordinal INTEGER, item_id TEXT);
"""

#: Cache radia z ROZBIEZNYMI flagami -- to on rozdziela widoki.
STATIONS = [
    # Biblioteka + ulubiona.
    {"id": "1", "name": "Trojka", "streamUrl": "http://t3", "isInLibrary": True,
     "isFavorite": True},
    # Biblioteka, NIE ulubiona.
    {"id": "2", "name": "Dwojka", "streamUrl": "http://t2", "isInLibrary": True},
    # ULUBIONA, ale POZA Biblioteka -- rozbiezne flagi.
    {"id": "3", "name": "Nowy Swiat", "streamUrl": "http://ns", "isInLibrary": False,
     "isFavorite": True},
    # Osad cache'u: ani Biblioteka, ani ulubione. Odtworzona raz z katalogu.
    {"id": "4", "name": "Katalog Radio Browser", "streamUrl": "http://rb"},
    # Napis "false" -- w Pythonie PRAWDZIWY. Nie moze przejsc nigdzie.
    {"id": "5", "name": "Pulapka", "streamUrl": "http://x", "isInLibrary": "false",
     "isFavorite": "false"},
    # Glowny adres pusty -> zapasowy, jak w AMC.
    {"id": "6", "name": "Zapas", "streamUrl": "", "backupStreamUrl": "http://bak",
     "isInLibrary": True},
    # Bez Id: nie da sie ani wybrac, ani odtworzyc.
    {"name": "Bez tozsamosci", "streamUrl": "http://no-id", "isInLibrary": True},
]

STATE = {
    "schemaVersion": 54,
    "radio": {"currentItemId": "2", "stations": STATIONS},
}


def _ids(result) -> list[str]:
    return [row.item_id for row in result.rows]


class RadioViewDataBase(unittest.TestCase):
    """Wspolny mirror AMC: ``state.json`` + ``library.db`` tylko do ODCZYTU."""

    def setUp(self) -> None:
        self._tmp = TemporaryDirectory()
        self.dir = Path(self._tmp.name)
        self.state_json = self.dir / "state.json"
        self.state_json.write_text(
            json.dumps(STATE, ensure_ascii=False), encoding="utf-8"
        )
        self.db_path = self.dir / "library.db"
        con = sqlite3.connect(self.db_path)
        con.executescript(SCHEMA)
        # ordinal 0 = ostatnio odtworzone. Wpis "4" jest POZA Biblioteka,
        # "9" nie istnieje w cache'u, "1" powtarza sie dwa razy.
        for ordinal, item_id in enumerate(["4", "1", "9", "1", "2"]):
            con.execute(
                "INSERT INTO playback_history(session_id, ordinal, item_id) "
                "VALUES (?,?,?)",
                (RADIO_SESSION, ordinal, item_id),
            )
        # Obca sesja: nie moze wyciec do historii radia.
        con.execute(
            "INSERT INTO playback_history(session_id, ordinal, item_id) "
            "VALUES ('local', 0, '1')"
        )
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
            self.state_json.read_bytes(),
            self.state_json.stat().st_mtime_ns,
        )
        self._db_before = (self.db_path.read_bytes(), self.db_path.stat().st_mtime_ns)

    def tearDown(self) -> None:
        self._tmp.cleanup()

    def assert_nothing_written(self) -> None:
        self.assertEqual(
            (self.state_json.read_bytes(), self.state_json.stat().st_mtime_ns),
            self._state_before,
            "odczyt widoku zmienil state.json",
        )
        self.assertEqual(
            (self.db_path.read_bytes(), self.db_path.stat().st_mtime_ns),
            self._db_before,
            "odczyt widoku zmienil library.db",
        )


class TestLibraryAndFavorites(RadioViewDataBase):
    def test_library_keeps_only_is_in_library_true(self) -> None:
        result = load_view(self.source, VIEW_LIBRARY)
        self.assertEqual(_ids(result), ["1", "2", "6"])
        self.assertIsNone(result.unavailable_reason)
        self.assertEqual(result.heading, "Radio — Biblioteka")
        self.assertEqual(result.current_id, "2")
        self.assert_nothing_written()

    def test_favorites_are_independent_of_library(self) -> None:
        """Rozbiezne flagi: "3" jest ulubiona i POZA Biblioteka."""
        result = load_view(self.source, VIEW_FAVORITES)
        self.assertEqual(_ids(result), ["1", "3"])
        self.assertNotIn("3", _ids(load_view(self.source, VIEW_LIBRARY)))
        self.assertEqual(result.heading, "Radio — Ulubione")
        self.assertIsNone(result.unavailable_reason)

    def test_views_are_not_the_same_list(self) -> None:
        """Gdyby ktos podstawil jeden zakres pod drugi, to by sie zrownalo."""
        self.assertNotEqual(
            _ids(load_view(self.source, VIEW_LIBRARY)),
            _ids(load_view(self.source, VIEW_FAVORITES)),
        )

    def test_string_false_is_not_truthy_membership(self) -> None:
        for view in (VIEW_LIBRARY, VIEW_FAVORITES):
            self.assertNotIn("5", _ids(load_view(self.source, view)), view)

    def test_entry_without_id_has_no_row(self) -> None:
        titles = [row.title for row in load_view(self.source, VIEW_LIBRARY).rows]
        self.assertNotIn("Bez tozsamosci", titles)

    def test_backup_url_used_when_primary_empty(self) -> None:
        row = next(r for r in load_view(self.source, VIEW_LIBRARY).rows
                   if r.item_id == "6")
        self.assertEqual(row.url, "http://bak")

    def test_rows_use_short_name_without_kind_word(self) -> None:
        """``rows_from_stations``: adres tylko w ``url``, bez slowa rodzaju."""
        row = load_view(self.source, VIEW_LIBRARY).rows[0]
        self.assertEqual(row.title, "Trojka")
        self.assertEqual(row.kind, "station")
        self.assertFalse(row.show_kind)
        self.assertEqual(row.detail, "")
        self.assertEqual(row.url, "http://t3")

    def test_unknown_view_is_refused(self) -> None:
        with self.assertRaises(ValueError):
            load_view(self.source, "podcasts")


class TestHistory(RadioViewDataBase):
    def test_order_duplicates_and_non_library_entry(self) -> None:
        result = load_view(self.source, VIEW_HISTORY)
        # "4" jest poza Biblioteka i ZOSTAJE: historia to "czego sluchalem".
        # "1" powtorzone -> jedno wystapienie, pierwsze. "9" nieznane -> brak.
        self.assertEqual(_ids(result), ["4", "1", "2"])
        self.assertEqual(result.missing_item_count, 1)
        self.assertIsNone(result.unavailable_reason)
        self.assertEqual(result.heading, "Radio — Historia odtwarzania")
        self.assertNotIn("4", _ids(load_view(self.source, VIEW_LIBRARY)))
        self.assert_nothing_written()

    def test_unknown_id_is_counted_not_deleted(self) -> None:
        load_view(self.source, VIEW_HISTORY)
        con = sqlite3.connect(self.db_path)
        try:
            left = con.execute(
                "SELECT COUNT(*) FROM playback_history WHERE item_id = '9'"
            ).fetchone()[0]
        finally:
            con.close()
        self.assertEqual(left, 1, "nieznane Id zostalo usuniete z bazy")

    def test_other_session_does_not_leak(self) -> None:
        # Sesja 'local' ma wpis "1" na ordinal 0; gdyby wyciekla, "1" byloby
        # pierwsze zamiast "4".
        self.assertEqual(_ids(load_view(self.source, VIEW_HISTORY))[0], "4")

    def test_caller_db_is_not_closed(self) -> None:
        with LibraryDatabase(self.db_path) as db:
            result = load_view(self.source, VIEW_HISTORY, db=db)
            self.assertEqual(_ids(result), ["4", "1", "2"])
            # Gdyby loader zamknal cudzy uchwyt, to by rzucilo.
            db.connection.execute("SELECT COUNT(*) FROM playback_history").fetchone()

    def test_missing_database_is_not_empty_history(self) -> None:
        self.db_path.unlink()
        result = load_view(self.source, VIEW_HISTORY)
        self.assertEqual(result.rows, [])
        self.assertIsNotNone(result.unavailable_reason)
        self.assertIn("library.db", result.unavailable_reason)

    def test_missing_history_table_is_not_empty_history(self) -> None:
        con = sqlite3.connect(self.db_path)
        con.execute("DROP TABLE playback_history")
        con.commit()
        con.close()
        result = load_view(self.source, VIEW_HISTORY)
        self.assertEqual(result.rows, [])
        self.assertIsNotNone(result.unavailable_reason)

    def test_no_profile_is_not_empty_history(self) -> None:
        self.state_json.unlink()
        result = load_view(self.source, VIEW_HISTORY)
        self.assertEqual(result.rows, [])
        self.assertIsNotNone(result.unavailable_reason)


class TestReadErrors(RadioViewDataBase):
    def test_broken_json_is_not_empty_collection(self) -> None:
        self.state_json.write_text("{nie-json", encoding="utf-8")
        for view in (VIEW_LIBRARY, VIEW_FAVORITES, VIEW_HISTORY):
            result = load_view(self.source, view)
            self.assertEqual(result.rows, [], view)
            self.assertIsNotNone(result.unavailable_reason, view)
            self.assertIn("uszkodzony", result.unavailable_reason, view)

    def test_missing_profile_is_not_empty_collection(self) -> None:
        self.state_json.unlink()
        for view in (VIEW_LIBRARY, VIEW_FAVORITES):
            result = load_view(self.source, view)
            self.assertEqual(result.rows, [], view)
            self.assertIn("Nie znalazłem profilu AMC", result.unavailable_reason, view)

    def test_empty_profile_without_radio_is_honest_empty(self) -> None:
        """Pusty profil to UDANY odczyt: pusto, ale bez komunikatu bledu."""
        self.state_json.write_text("{}", encoding="utf-8")
        result = load_view(self.source, VIEW_LIBRARY)
        self.assertEqual(result.rows, [])
        self.assertIsNone(result.unavailable_reason)

    def test_profile_with_unexpected_shape(self) -> None:
        self.state_json.write_text("[1,2,3]", encoding="utf-8")
        result = load_view(self.source, VIEW_LIBRARY)
        self.assertEqual(result.rows, [])
        self.assertIsNotNone(result.unavailable_reason)


class TestPrivateSandbox(unittest.TestCase):
    """Prywatna lista stacji dziala; zakresy, ktorych nie ma, mowia dlaczego."""

    def setUp(self) -> None:
        self._tmp = TemporaryDirectory()
        self.dir = Path(self._tmp.name)
        self.layout = ProfileLayout(
            mode=ProfileMode.PRIVATE_SANDBOX,
            library_db=self.dir / "library.db",
            podcasts_db=self.dir / "podcasts.db",
            state_json=self.dir / "state.json",
            lite_settings_dir=self.dir / "lite",
        )
        store = StateStore(self.layout.lite_settings_dir)
        state = store.load()
        state.stations = [
            Station(id="a", name="Moja pierwsza", url="http://a"),
            Station(id="b", name="Moja druga", url="http://b"),
        ]
        store.save(state)
        self.source = RadioSource(self.layout)

    def tearDown(self) -> None:
        self._tmp.cleanup()

    def test_private_library_still_works(self) -> None:
        result = load_view(self.source, VIEW_LIBRARY)
        self.assertEqual(_ids(result), ["a", "b"])
        self.assertIsNone(result.unavailable_reason)

    def test_favorites_and_history_say_why_they_are_unavailable(self) -> None:
        for view in (VIEW_FAVORITES, VIEW_HISTORY):
            result = load_view(self.source, view)
            self.assertEqual(result.rows, [], view)
            self.assertIsNotNone(result.unavailable_reason, view)
            self.assertIn("profilu AMC", result.unavailable_reason, view)

    def test_reading_views_does_not_create_amc_profile(self) -> None:
        for view in (VIEW_LIBRARY, VIEW_FAVORITES, VIEW_HISTORY):
            load_view(self.source, view)
        self.assertFalse(self.layout.state_json.exists())
        self.assertFalse(self.layout.library_db.exists())


class TestWriteProtection(RadioViewDataBase):
    def test_shared_profile_refuses_save(self) -> None:
        self.assertFalse(self.source.may_edit)
        with self.assertRaises(ProfileWriteDenied):
            self.source.save(RadioSnapshot(list=self.source.load().list))
        self.assert_nothing_written()


if __name__ == "__main__":
    unittest.main()
