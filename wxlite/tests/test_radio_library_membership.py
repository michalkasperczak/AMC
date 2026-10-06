"""Biblioteka radia to CZLONKOSTWO (``isInLibrary``), nie caly cache profilu.

Zmierzony objaw (``amc_pomoc/wx-library-compare-20261006/verified-comparison.json``):
na jednym odczycie glownego komputera Python oddawal **180** stacji, a widok
WPF 422 ,,Wszystkie stacje'' **60**. Nadwyzka 120 wpisow to osad cache'u
``radio.stations``: wyniki katalogu Radio Browser, jednorazowe strumienie
i stacje kiedykolwiek zdjete z Biblioteki.

Filtr oryginalu (``MainWindow.xaml.cs:12833-12835``, commit ``1f5dccb6``):

```
if (_currentView == "Biblioteka" && !IsSonosSession(_sessions.Current.Id))
{
    items = items.Where(item => item.IsInLibrary
        && (!UsesTidalStyleCollections(_sessions.Current.Id)
            || TidalCollectionSemantics.UsesLibrary(item.Kind)));
```

Dla sesji ``radio`` ``UsesTidalStyleCollections`` jest falszywe, wiec efektywny
warunek to DOKLADNIE ``item.IsInLibrary == true`` -- bez ``IsAvailable``, bez
``Kind``, bez ``DirectoryId``.

Czego ten plik NIE sprawdza
---------------------------
* Nie ma tu zadnego widoku Ulubionych radia -- w wx taki OSOBNY widok nie
  istnieje (``Ctrl+U`` to lokalne Ulubione PLIKOW, w Radiu wyszarzone), wiec
  nie wymyslamy nowej uslugi i nie wciskamy ``isFavorite`` do Biblioteki.
* Nie mierzy zywego GUI ani NVDA. To testy bezokienne nad producentem wierszy.
* Nic nie zapisuje do profilu: zadne z 180 zapamietanych wejsc nie jest
  usuwane -- sa tylko NIEPOKAZYWANE, dokladnie jak w WPF.
"""

from __future__ import annotations

import json
import shutil
import tempfile
import unittest
from pathlib import Path

from amc_wx_lite.profile_layout import read_only_mirror
from amc_wx_lite.radio_source import RadioSource, stations_from_amc_state

FIXTURE_STATE = Path(
    "/home/michal/projekty/amc_pomoc/wx-full-profile-after421/fixture/state.json"
)


def _station(entry_id: str, name: str, **flags) -> dict:
    """Wpis o kształcie ZMIERZONEGO ``state.json`` (``schemaVersion`` 54).

    Flagi podaje sie JAWNIE, zeby nie udawac, ze brak pola znaczy cokolwiek
    innego niz brak pola.
    """
    station = {
        "id": entry_id,
        "name": name,
        "streamUrl": f"https://example.invalid/{entry_id}",
    }
    station.update(flags)
    return station


def _state(stations: list[dict], current: str | None = None) -> dict:
    radio: dict = {"stations": stations}
    if current:
        radio["currentItemId"] = current
    return {"schemaVersion": 54, "radio": radio}


class MembershipFlagDecidesTheRow(unittest.TestCase):
    """Trzy ROZNE stany flagi: jawne ``true``, jawne ``false``, BRAK pola."""

    def test_explicit_true_is_in_the_library(self) -> None:
        stations, _ = stations_from_amc_state(
            _state([_station("tak", "W Bibliotece", isInLibrary=True)])
        )
        self.assertEqual([s.id for s in stations], ["tak"])

    def test_explicit_false_is_not_in_the_library(self) -> None:
        """Jednorazowy odsluch / slad katalogu ZOSTAJE w pliku, ale nie w widoku.

        ``OpenStream`` (``MainWindow.xaml.cs:16062``) ustawia
        ``IsInLibrary = false`` z komentarzem ,,Nie trafia do Biblioteki: to
        odsluch jednorazowy'', a potem i tak robi ``CaptureRadioState()``.
        """
        stations, _ = stations_from_amc_state(
            _state([_station("nie", "Jednorazowy", isInLibrary=False)])
        )
        self.assertEqual(stations, [])

    def test_missing_flag_is_outside_the_library_like_csharp_default(self) -> None:
        """``RadioStationSettings.IsInLibrary`` to ``bool`` BEZ inicjalizatora.

        ``Core/Configuration/AppSettings.cs`` (``1f5dccb6``) deklaruje
        ``public bool IsInLibrary { get; set; }`` -- domyslnie ``false``.
        To ODWROTNIE niz pliki lokalne, gdzie ``LocalMediaItemSettings``
        ma ``= true``. Wpis radia bez tego pola laduje wiec POZA Biblioteka,
        i tak samo musi go widziec Python.
        """
        stations, _ = stations_from_amc_state(_state([_station("brak", "Bez flagi")]))
        self.assertEqual(stations, [])

    def test_all_three_states_in_one_profile(self) -> None:
        stations, _ = stations_from_amc_state(
            _state(
                [
                    _station("a", "Jawne true", isInLibrary=True),
                    _station("b", "Jawne false", isInLibrary=False),
                    _station("c", "Brak pola"),
                    _station("d", "Drugie true", isInLibrary=True),
                ]
            )
        )
        self.assertEqual([s.id for s in stations], ["a", "d"])

    def test_source_order_of_the_members_is_untouched(self) -> None:
        """Filtr wybiera wiersze; NIE sortuje tego, co zostalo."""
        stations, _ = stations_from_amc_state(
            _state(
                [
                    _station("z", "Zeta", isInLibrary=True),
                    _station("odpad", "Odpad", isInLibrary=False),
                    _station("a", "Alfa", isInLibrary=True),
                ]
            )
        )
        self.assertEqual([s.id for s in stations], ["z", "a"])


class FlagsComeFromRealBooleans(unittest.TestCase):
    """Parser musi dostawac BOOLEANY, a nie napisy ,,true''/,,false''.

    Gdyby wartosci przychodzily jako napisy, ``"false"`` bylo by prawdziwe
    w Pythonie i filtr nie odsiewalby niczego -- zielony test nad zepsutym
    wejsciem. Dlatego sprawdzamy typ w ZRODLE.
    """

    @classmethod
    def setUpClass(cls) -> None:
        if not FIXTURE_STATE.exists():
            raise unittest.SkipTest(f"brak kopii profilu: {FIXTURE_STATE}")
        cls.source = json.loads(FIXTURE_STATE.read_text(encoding="utf-8"))

    def test_fixture_stores_isinlibrary_as_json_boolean(self) -> None:
        values = {
            type(entry.get("isInLibrary")).__name__
            for entry in self.source["radio"]["stations"]
        }
        self.assertEqual(values, {"bool"}, "isInLibrary ma byc booleanem JSON")

    def test_fixture_has_both_states_so_the_filter_can_discriminate(self) -> None:
        flags = [bool(e["isInLibrary"]) for e in self.source["radio"]["stations"]]
        self.assertIn(True, flags)
        self.assertIn(False, flags, "bez wpisow false test nie rozroznialby regul")

    def test_string_false_is_not_silently_treated_as_membership(self) -> None:
        """Gdyby ktos kiedys wstawil napis, nie wolno go brac za ``true``."""
        stations, _ = stations_from_amc_state(
            _state([_station("napis", "Napis false", isInLibrary="false")])
        )
        self.assertEqual(
            [s.id for s in stations],
            [],
            "napis 'false' nie jest czlonkostwem -- czytamy boolean, nie prawdziwosc",
        )


class MeasuredOnACopyOfTheFullProfile(unittest.TestCase):
    """Przeliczenie na READ-ONLY kopii pelnego profilu.

    Zrodlo: ``/home/michal/projekty/amc_pomoc/wx-full-profile-after421/fixture/state.json``.
    Liczby NIE sa zahardkodowane jako oczekiwanie -- test liczy je w swoim
    zrodle i porownuje z tym, co oddal czytnik.
    """

    @classmethod
    def setUpClass(cls) -> None:
        if not FIXTURE_STATE.exists():
            raise unittest.SkipTest(f"brak kopii profilu: {FIXTURE_STATE}")
        cls.source = json.loads(FIXTURE_STATE.read_text(encoding="utf-8"))
        cls.raw_bytes = FIXTURE_STATE.read_bytes()

    def setUp(self) -> None:
        self.dir = Path(tempfile.mkdtemp(prefix="amc-radio-membership-"))
        self.addCleanup(shutil.rmtree, self.dir, ignore_errors=True)
        self.state_path = self.dir / "state.json"
        shutil.copy2(FIXTURE_STATE, self.state_path)
        # Fixture jest read-only i ``copy2`` przenosi tryb. Prawa nadajemy
        # WLASNEJ kopii w ``/tmp``; zrodlo pozostaje nietkniete.
        self.state_path.chmod(0o644)
        self.layout = read_only_mirror(
            local_dir=self.dir, profile_dir=self.dir, lite_settings_dir=self.dir / "lite"
        )

    def _members(self) -> list[dict]:
        return [e for e in self.source["radio"]["stations"] if e.get("isInLibrary")]

    def test_shows_exactly_the_members_not_the_whole_cache(self) -> None:
        stations = RadioSource(self.layout).load().stations
        members = self._members()
        self.assertEqual([s.id for s in stations], [e["id"] for e in members])
        self.assertLess(
            len(stations),
            len(self.source["radio"]["stations"]),
            "kopia ma wpisy poza Biblioteka -- inaczej test nie rozroznialby regul",
        )

    def test_difference_is_exactly_the_non_members(self) -> None:
        stations = RadioSource(self.layout).load().stations
        shown = {s.id for s in stations}
        outside = [
            e["id"] for e in self.source["radio"]["stations"] if not e.get("isInLibrary")
        ]
        self.assertEqual(len(shown) + len(outside), len(self.source["radio"]["stations"]))
        self.assertTrue(all(i not in shown for i in outside))

    def test_nothing_is_deleted_from_the_profile(self) -> None:
        """180 zapisow zostaje 180 zapisow. Filtr UKRYWA, nie kasuje."""
        before = self.state_path.read_bytes()
        RadioSource(self.layout).load()
        after = self.state_path.read_bytes()
        self.assertEqual(after, before, "odczyt nie zmienia ani jednego bajtu profilu")
        reread = json.loads(self.state_path.read_text(encoding="utf-8"))
        self.assertEqual(
            len(reread["radio"]["stations"]),
            len(self.source["radio"]["stations"]),
            "liczba ZAPISANYCH wpisow w profilu zostaje bez zmian",
        )

    def test_the_protected_fixture_is_not_modified(self) -> None:
        RadioSource(self.layout).load()
        self.assertEqual(FIXTURE_STATE.read_bytes(), self.raw_bytes)

    def test_selection_follows_current_item_id_within_the_library(self) -> None:
        """``currentItemId`` tej kopii jest CZLONKIEM, wiec ma pozycje w widoku."""
        snapshot = RadioSource(self.layout).load()
        current = self.source["radio"]["currentItemId"]
        members = [e["id"] for e in self._members()]
        self.assertIn(current, members, "ta kopia ma biezaca stacje w Bibliotece")
        self.assertEqual(snapshot.current_id, current)
        self.assertEqual(snapshot.index_of_current, members.index(current))

    def test_current_station_outside_the_library_has_no_row_to_select(self) -> None:
        """Bieząca stacja spoza Biblioteki nie ma wiersza -- i nie udajemy, ze ma.

        ``index_of_current`` oddaje ``None``, a nie wiersz 0: podstawienie
        pierwszego wiersza byloby cichym klamstwem o zaznaczeniu.
        """
        payload = json.loads(self.state_path.read_text(encoding="utf-8"))
        outside = next(
            e["id"] for e in payload["radio"]["stations"] if not e.get("isInLibrary")
        )
        payload["radio"]["currentItemId"] = outside
        self.state_path.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")

        snapshot = RadioSource(self.layout).load()
        self.assertEqual(snapshot.current_id, outside)
        self.assertIsNone(snapshot.index_of_current)


class FavoritesStayASeparateScope(unittest.TestCase):
    """Ulubione to INNY zakres niz Biblioteka -- i w wx nie ma ich widoku radia.

    ``ConfigurationStore.NormalizeRadio`` (``:873``) podnosi ``IsInLibrary``
    dla ulubionych PRZY WCZYTANIU PROFILU PRZEZ C#, wiec plik zapisany przez
    AMC nie powinien zawierac ``isFavorite:true`` bez ``isInLibrary:true``.
    Mierzymy to na kopii, zamiast powielac te normalizacje w Pythonie:
    Python nie jest wlascicielem zapisu i nie ma tu podnosic zadnych flag.
    """

    @classmethod
    def setUpClass(cls) -> None:
        if not FIXTURE_STATE.exists():
            raise unittest.SkipTest(f"brak kopii profilu: {FIXTURE_STATE}")
        cls.stations = json.loads(FIXTURE_STATE.read_text(encoding="utf-8"))["radio"][
            "stations"
        ]

    def test_measured_profile_has_no_favorite_outside_the_library(self) -> None:
        orphans = [
            e["id"]
            for e in self.stations
            if e.get("isFavorite") and not e.get("isInLibrary")
        ]
        self.assertEqual(
            orphans,
            [],
            "gdyby takie wpisy istnialy, Python pokazalby MNIEJ niz WPF -- "
            "wtedy trzeba dolozyc regule NormalizeRadio, nie zgadywac",
        )

    def test_favorite_flag_alone_does_not_create_a_library_row(self) -> None:
        """Python czyta SAME ``isInLibrary``. Zadnego cichego OR z ulubionymi.

        Widok Ulubionych radia w tym porcie NIE ISTNIEJE, wiec wpychanie go
        do Biblioteki byloby podstawieniem jednego zakresu pod drugi.
        """
        stations, _ = stations_from_amc_state(
            _state([_station("fav", "Tylko ulubiona", isFavorite=True, isInLibrary=False)])
        )
        self.assertEqual([s.id for s in stations], [])


if __name__ == "__main__":
    unittest.main()
