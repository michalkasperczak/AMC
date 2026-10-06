"""Radio na PELNYM profilu AMC: stacje z ``radio.stations``, nie z kopii.

Co bylo zle
-----------
``StateStore.load`` czytalo PRYWATNY klucz ``stations`` (plaskie wpisy
``{id,name,url}``). Pelny profil AMC trzyma stacje gdzie indziej -- zmierzony
schemat zrodlowego ``state.json`` (``schemaVersion`` 54):

* ``radio.stations`` -- lista 165 wpisow,
* wpis ma ``id`` (hex, 42 znaki), ``name``, ``streamUrl`` (+ 18 innych pol:
  ``backupStreamUrl``, ``codec``, ``country``, ``isCustom``, ...),
* ``radio.currentItemId`` wskazuje stacje biezaca; w zmierzonym profilu jest
  to pozycja o indeksie **80**, a nie pierwsza. Zaznaczenie nie moze wiec
  "domyslnie wracac na gore".

Przy prywatnym kluczu ``stations`` Radio na pelnym profilu bylo PUSTE.
Migracja ("przepisz raz 165 stacji do wlasnego pliku") jest tu zla odpowiedzia:
dawalaby druga, starzejaca sie kopie, ktora nie widzi zmian zrobionych w AMC.

Kontrakt sprawdzany ponizej
---------------------------
* ``READ_ONLY_MIRROR``: stacje pochodza z AKTUALNEGO ``state.json`` AMC,
  w ORYGINALNEJ kolejnosci, z zachowanym ``id`` / nazwa / adres.
* Zmiana w zrodle jest widoczna po odswiezeniu (zadna kopia nie zastyga).
* Zaznaczenie idzie za ``radio.currentItemId``.
* Edycja/dodanie/usuniecie w tym trybie jest ODMOWIONE jawnie -- bez
  udawania, ze zapis do wspolnego profilu sie udal.
* ``PRIVATE_SANDBOX`` nadal dziala po staremu (prywatny klucz ``stations``),
  bo Ctrl+O i wlasna lista maja zostac sprawne.

Liczba 165 NIE jest tu zahardkodowana jako oczekiwanie: test liczy wpisy
w swoim zrodle i porownuje z tym, co zwrocil czytnik.
"""

from __future__ import annotations

import json
import shutil
import tempfile
import unittest
from pathlib import Path

from amc_wx_lite.list_model import rows_from_stations
from amc_wx_lite.profile_layout import ProfileWriteDenied, private_sandbox, read_only_mirror
from amc_wx_lite.radio_source import RadioSource
from amc_wx_lite.state_store import StateStore

FIXTURE_STATE = Path(
    "/home/michal/projekty/amc_pomoc/wx-full-profile-after421/fixture/state.json"
)


def _write_atomic(path: Path, payload: dict) -> None:
    temporary = path.with_suffix(".tmp")
    temporary.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")
    temporary.replace(path)


class RadioReadsTheRealAmcProfile(unittest.TestCase):
    """Zrodlo = kopia PRAWDZIWEGO state.json, nie wymyslony kształt."""

    @classmethod
    def setUpClass(cls):
        if not FIXTURE_STATE.exists():
            raise unittest.SkipTest(f"brak kopii profilu: {FIXTURE_STATE}")
        cls.source = json.loads(FIXTURE_STATE.read_text(encoding="utf-8"))

    def setUp(self):
        self.dir = Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, self.dir, ignore_errors=True)
        self.state_path = self.dir / "state.json"
        shutil.copy2(FIXTURE_STATE, self.state_path)
        self.state_path.chmod(0o644)  # fixture jest read-only, kopia nie musi byc
        self.layout = read_only_mirror(
            local_dir=self.dir, profile_dir=self.dir, lite_settings_dir=self.dir / "lite"
        )

    @property
    def expected_stations(self) -> list[dict]:
        """Stacje, ktore MAJA sie pokazac: wylacznie ``isInLibrary``.

        Biblioteka radia to czlonkostwo, a ``radio.stations`` to trwaly cache
        calej sesji (wyniki katalogu, jednorazowe strumienie, stacje zdjete
        z Biblioteki). Wczesniej te testy porownywaly sie do CALEJ listy
        i utrwalaly regule ,,kazdy zapis w profilu = Biblioteka'', ktora na
        zmierzonym profilu dawala 180 wierszy zamiast 60.

        ``RadioStationSettings.IsInLibrary`` to ``bool`` bez inicjalizatora,
        wiec brak pola = ``false`` = poza Biblioteka; dlatego ``is True``.
        """
        return [
            e
            for e in self.source["radio"]["stations"]
            if e.get("isInLibrary") is True
        ]

    # ------------------------------------------------------------- odczyt

    def test_reads_every_station_in_the_library(self):
        expected = self.expected_stations
        stations = RadioSource(self.layout).load().stations
        self.assertEqual(
            len(stations), len(expected),
            "Biblioteka radia oddaje stacje z isInLibrary, nie caly cache",
        )
        self.assertGreater(len(stations), 10, "kopia profilu ma byc realna, nie atrapa")
        self.assertLess(
            len(stations), len(self.source["radio"]["stations"]),
            "zmierzony profil MA stacje poza Biblioteka -- inaczej test nic nie rozstrzyga",
        )

    def test_keeps_id_name_address_and_source_order(self):
        expected = self.expected_stations
        stations = RadioSource(self.layout).load().stations
        self.assertEqual(
            [s.id for s in stations], [e["id"] for e in expected],
            "ID i KOLEJNOSC zrodla sa nienaruszalne -- zadnego sortowania 'dla wygody'",
        )
        self.assertEqual([s.name for s in stations], [e["name"] for e in expected])
        self.assertEqual([s.url for s in stations], [e["streamUrl"] for e in expected])

    def test_selection_follows_current_item_id_not_the_first_row(self):
        """Zaznaczenie idzie za ``currentItemId``, ale tylko w obrebie Biblioteki.

        ``current_id`` czytamy zawsze -- to stan sesji radia, nie czlonkostwo.
        Indeks wiersza istnieje tylko wtedy, gdy biezaca stacja jest TEZ
        w Bibliotece; jesli uzytkownik sluchal stacji z katalogu bez dodania
        jej do Biblioteki, nie ma czego zaznaczyc i to poprawny wynik.
        """
        expected = self.source["radio"]
        snapshot = RadioSource(self.layout).load()
        library_ids = [e["id"] for e in self.expected_stations]
        self.assertEqual(snapshot.current_id, expected["currentItemId"])
        if expected["currentItemId"] in library_ids:
            self.assertEqual(
                snapshot.index_of_current, library_ids.index(expected["currentItemId"]),
                "Zaznaczenie idzie za currentItemId profilu",
            )
        else:
            self.assertIsNone(
                snapshot.index_of_current,
                "biezaca stacja spoza Biblioteki nie ma wiersza do zaznaczenia",
            )

    def test_rows_for_the_list_widget_carry_name_and_address(self):
        snapshot = RadioSource(self.layout).load()
        rows = rows_from_stations(snapshot.as_payload())
        self.assertEqual(len(rows), len(snapshot.stations))
        self.assertTrue(all(row.kind == "station" for row in rows))
        self.assertTrue(all(row.title for row in rows), "kazdy wiersz ma nazwe do odczytania")
        self.assertTrue(all(row.url for row in rows), "kazdy wiersz ma adres strumienia")

    def test_sees_a_refresh_made_in_amc_instead_of_freezing_a_copy(self):
        source = RadioSource(self.layout)
        before = source.load()
        payload = json.loads(self.state_path.read_text(encoding="utf-8"))
        template = dict(payload["radio"]["stations"][0])
        template["id"] = "dopisana-przez-amc"
        template["name"] = "Stacja dopisana w AMC"
        template["streamUrl"] = "https://example.invalid/nowa"
        template["isInLibrary"] = True  # dodana do Biblioteki, nie tylko zapisana
        payload["radio"]["stations"].append(template)
        payload["radio"]["currentItemId"] = template["id"]
        _write_atomic(self.state_path, payload)

        after = source.load()
        self.assertEqual(
            len(after.stations), len(before.stations) + 1,
            "Zmiana zrobiona w AMC MUSI byc widoczna po odswiezeniu",
        )
        self.assertEqual(after.stations[-1].id, "dopisana-przez-amc")
        self.assertEqual(after.current_id, "dopisana-przez-amc")

    def test_a_station_only_saved_in_amc_is_not_a_library_member(self):
        """Dopisanie wpisu BEZ ``isInLibrary`` nie powieksza Biblioteki.

        Uzgodnienie uzytkownika: samo zapisanie w profilu nie oznacza
        czlonkostwa. Wpis zostaje w profilu nienaruszony -- jest tylko
        niepokazywany.
        """
        source = RadioSource(self.layout)
        before = source.load()
        payload = json.loads(self.state_path.read_text(encoding="utf-8"))
        template = dict(payload["radio"]["stations"][0])
        template["id"] = "tylko-zapisana"
        template["name"] = "Z katalogu, bez dodania"
        template["streamUrl"] = "https://example.invalid/katalog"
        template["isInLibrary"] = False
        payload["radio"]["stations"].append(template)
        _write_atomic(self.state_path, payload)

        after = source.load()
        self.assertEqual(len(after.stations), len(before.stations))
        self.assertNotIn("tylko-zapisana", [s.id for s in after.stations])
        # Wpis NADAL jest w profilu -- nic nie kasujemy.
        saved = json.loads(self.state_path.read_text(encoding="utf-8"))
        self.assertIn("tylko-zapisana", [e["id"] for e in saved["radio"]["stations"]])

    def test_does_not_touch_the_source_file(self):
        before = self.state_path.read_bytes()
        RadioSource(self.layout).load()
        self.assertEqual(
            self.state_path.read_bytes(), before,
            "Odczyt Radia nie moze zmienic ani jednego bajtu profilu",
        )

    # -------------------------------------------------------------- zapis

    def test_editing_the_shared_profile_is_refused_not_faked(self):
        source = RadioSource(self.layout)
        snapshot = source.load()
        before = self.state_path.read_bytes()
        with self.assertRaises(ProfileWriteDenied):
            source.save(snapshot)
        self.assertEqual(self.state_path.read_bytes(), before)

    def test_it_says_up_front_that_editing_is_unavailable(self):
        source = RadioSource(self.layout)
        self.assertFalse(
            source.may_edit,
            "GUI musi wiedziec ZAWCZASU, ze w trybie wspolnego profilu nie edytuje",
        )
        self.assertIn("tylko", source.edit_refusal_reason().lower())


class PrivateModeStillWorks(unittest.TestCase):
    """Ctrl+O i wlasna lista stacji nie moga sie zepsuc przy tej zmianie."""

    def setUp(self):
        self.dir = Path(tempfile.mkdtemp())
        self.addCleanup(shutil.rmtree, self.dir, ignore_errors=True)
        self.layout = private_sandbox(self.dir)

    def test_sandbox_reads_and_writes_its_own_station_list(self):
        source = RadioSource(self.layout)
        self.assertTrue(source.may_edit)
        snapshot = source.load()
        self.assertEqual(snapshot.stations, [])

        added = snapshot.list.add("Moja stacja", "https://example.invalid/moja")
        source.save(snapshot)

        again = RadioSource(self.layout).load()
        self.assertEqual([s.id for s in again.stations], [added.id])
        self.assertEqual(again.stations[0].name, "Moja stacja")

    def test_sandbox_writes_go_to_the_private_store_only(self):
        source = RadioSource(self.layout)
        snapshot = source.load()
        snapshot.list.add("Moja", "https://example.invalid/a")
        source.save(snapshot)
        self.assertFalse(
            (self.dir / "state.json").exists(),
            "prywatny tryb nie tworzy ani nie rusza profilowego state.json",
        )
        self.assertTrue((StateStore(self.layout.lite_settings_dir)).path.exists())


if __name__ == "__main__":
    unittest.main()
