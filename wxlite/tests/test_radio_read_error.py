"""Blad odczytu profilu to NIE "zero stacji" -- i nie wolno go przemilczec.

Zglaszany objaw: ``RadioSource._load_from_amc`` lapalo ``FileNotFoundError``,
``json.JSONDecodeError``, ``OSError`` i ``UnicodeDecodeError``, po czym
zwracalo pusta liste. Dla uzytkownika niewidomego oba przypadki brzmialy
IDENTYCZNIE -- cisza i pusta lista:

  * profil faktycznie bez stacji (prawda, nic nie trzeba robic),
  * AMC trzyma plik na zapisie / plik uszkodzony (blad, ktory trzeba zglosic).

Wymagania sprawdzane tutaj:
  1. Udany odczyt pustej listy => ``load_error is None``.
  2. Blad odczytu => ``load_error`` z krotkim zdaniem dla czytnika.
  3. Nieudane ODSWIEZENIE zachowuje poprzednia liste (165 stacji nie znika
     z ekranu z powodu jednego chwilowego bledu odczytu).
  4. Poprawny ponowny odczyt pokazuje NOWE dane i gasi komunikat bledu.
  5. Nic sie nie zapisuje do profilu AMC -- ochrona zapisu zostaje.
"""

from __future__ import annotations

import json
import os
import tempfile
import unittest
from pathlib import Path

from amc_wx_lite.profile_layout import ProfileLayout, ProfileMode
from amc_wx_lite.radio_source import RadioSource


def _state(stations: list[dict], current: str | None = None) -> dict:
    """Zbuduj ``state.json`` dla testow BLEDU ODCZYTU.

    Ten plik bada obsluge bledow, nie czlonkostwo w Bibliotece, dlatego
    kazda stacja bez jawnej flagi dostaje ``isInLibrary: True`` -- inaczej
    poprawny filtr Biblioteki (``radio_source.stations_from_amc_state``)
    zwracalby pusta liste i testy bledow mierzylyby nie to, co powinny.
    Zakres Biblioteki rozstrzyga ``tests/test_radio_library_membership.py``.
    """
    stamped = [
        {**s, "isInLibrary": s.get("isInLibrary", True)}
        if isinstance(s, dict)
        else s
        for s in stations
    ]
    radio: dict = {"stations": stamped}
    if current:
        radio["currentItemId"] = current
    return {"schemaVersion": 54, "radio": radio}


class AmcReadErrorTests(unittest.TestCase):
    def setUp(self) -> None:
        self.dir = Path(tempfile.mkdtemp(prefix="amc-radio-err-"))
        self.state = self.dir / "state.json"
        self.layout = ProfileLayout(
            mode=ProfileMode.READ_ONLY_MIRROR,
            library_db=self.dir / "library.db",
            podcasts_db=self.dir / "podcasts.db",
            state_json=self.state,
            lite_settings_dir=self.dir / "lite",
        )

    def _source(self) -> RadioSource:
        return RadioSource(self.layout)

    # --------------------------------------------------- prawdziwie zero stacji

    def test_real_empty_profile_is_not_an_error(self) -> None:
        self.state.write_text(json.dumps(_state([])), encoding="utf-8")
        snap = self._source().load()
        self.assertEqual(snap.stations, [])
        self.assertIsNone(snap.load_error, "pusta lista to nie blad odczytu")

    def test_station_count_is_reported_for_real_data(self) -> None:
        self.state.write_text(
            json.dumps(_state([{"id": "a", "name": "Jeden", "streamUrl": "http://a/"}])),
            encoding="utf-8",
        )
        snap = self._source().load()
        self.assertEqual(len(snap.stations), 1)
        self.assertIsNone(snap.load_error)

    # ------------------------------------------------------------ bledy odczytu

    def test_missing_file_reports_error(self) -> None:
        snap = self._source().load()
        self.assertEqual(snap.stations, [])
        self.assertIsNotNone(snap.load_error)
        self.assertIn("nie", (snap.load_error or "").lower())

    def test_broken_json_reports_error(self) -> None:
        self.state.write_text('{"radio": {"stations": [', encoding="utf-8")
        snap = self._source().load()
        self.assertEqual(snap.stations, [])
        self.assertIsNotNone(snap.load_error)

    def test_non_dict_json_reports_error(self) -> None:
        self.state.write_text("[1, 2, 3]", encoding="utf-8")
        snap = self._source().load()
        self.assertIsNotNone(snap.load_error)

    def test_error_message_is_short_enough_to_hear(self) -> None:
        snap = self._source().load()
        message = snap.load_error or ""
        self.assertLessEqual(len(message), 160, "komunikat dla czytnika musi byc krotki")
        self.assertNotIn("Traceback", message)

    # ------------------------------------- nieudane odswiezenie zachowuje liste

    def test_failed_refresh_keeps_previous_stations(self) -> None:
        self.state.write_text(
            json.dumps(_state([{"id": f"s{i}", "name": f"Stacja {i}",
                                "streamUrl": f"http://h/{i}"} for i in range(165)], "s80")),
            encoding="utf-8",
        )
        source = self._source()
        first = source.load()
        self.assertEqual(len(first.stations), 165)
        self.assertEqual(first.index_of_current, 80)

        self.state.write_text("{{{ zepsute", encoding="utf-8")
        second = source.load(previous=first)
        self.assertEqual(len(second.stations), 165, "lista nie moze zniknac przy bledzie")
        self.assertEqual(second.index_of_current, 80, "zaznaczenie tez zostaje")
        self.assertIsNotNone(second.load_error)
        self.assertTrue(second.kept_previous)

    def test_successful_reread_shows_new_data_and_clears_error(self) -> None:
        self.state.write_text("zepsute", encoding="utf-8")
        source = self._source()
        broken = source.load()
        self.assertIsNotNone(broken.load_error)

        self.state.write_text(
            json.dumps(_state([{"id": "n1", "name": "Nowa", "streamUrl": "http://n/"}])),
            encoding="utf-8",
        )
        fixed = source.load(previous=broken)
        self.assertEqual([s.name for s in fixed.stations], ["Nowa"])
        self.assertIsNone(fixed.load_error)
        self.assertFalse(fixed.kept_previous)

    def test_real_empty_profile_does_not_resurrect_old_list(self) -> None:
        """Uzytkownik usunal wszystkie stacje w AMC -- lista MA sie oproznic."""
        self.state.write_text(
            json.dumps(_state([{"id": "a", "name": "Jeden", "streamUrl": "http://a/"}])),
            encoding="utf-8",
        )
        source = self._source()
        first = source.load()
        self.state.write_text(json.dumps(_state([])), encoding="utf-8")
        second = source.load(previous=first)
        self.assertEqual(second.stations, [])
        self.assertIsNone(second.load_error)
        self.assertFalse(second.kept_previous)

    # ------------------------------------------------------- ochrona zapisu AMC

    def test_read_error_does_not_write_the_amc_profile(self) -> None:
        before = self.state.exists()
        snap = self._source().load()
        self.assertIsNotNone(snap.load_error)
        self.assertEqual(self.state.exists(), before, "nie wolno tworzyc state.json")

    def test_permission_error_is_reported_not_swallowed(self) -> None:
        if os.name == "nt":
            self.skipTest("chmod nie blokuje odczytu na Windows")
        self.state.write_text(json.dumps(_state([])), encoding="utf-8")
        self.state.chmod(0o000)
        try:
            snap = self._source().load()
        finally:
            self.state.chmod(0o644)
        self.assertIsNotNone(snap.load_error)


if __name__ == "__main__":
    unittest.main()
