"""Adres na ZADANIE: Ctrl+C nazwa, Ctrl+Shift+C adres -- jak w zwyklym AMC.

Skroty NIE sa wymyslone. Odczytane ze zrodel pelnego AMC:

  src/AccessibleMediaController.Windows/MainWindow.xaml.cs:20717-20724
      modifiers == ModifierKeys.Control && e.Key == Key.C
          -> CopyActionItemName()
  src/AccessibleMediaController.Windows/MainWindow.xaml.cs:20725-20732
      modifiers == (Control | Shift) && e.Key == Key.C
          -> CopyActionItemLocation()

Komunikaty tez pochodza z AMC, nie z wlasnej inwencji:
  MainWindow.xaml.cs:24372  "Skopiowano nazwę"
  MainWindow.xaml.cs:24400  "Skopiowano bezpośredni adres"
  MainWindow.xaml.cs:24428  "Skopiowano plik i pełną ścieżkę"

WAZNE ograniczenie zakresu: te skroty dzialaja na LISCIE. Pole tekstowe
dialogu stacji ma zachowac zwykle, tekstowe Ctrl+C -- odebranie go byloby
regresja dostepnosci, wiec ``resolve`` nie wolno pytac w dialogu.
"""

from __future__ import annotations

from amc_wx_lite.list_model import ListModel, Row, rows_from_stations
from amc_wx_lite.shortcuts import Action, Chord, resolve

URL = "http://stream.example.com:8000/live/nadajnik.mp3"


def test_ctrl_c_on_list_is_copy_name() -> None:
    action = resolve(Chord("C", ctrl=True), player_view=False, radio_session=True)
    assert action is Action.COPY_NAME


def test_ctrl_shift_c_on_list_is_copy_address() -> None:
    action = resolve(Chord("C", ctrl=True, shift=True), player_view=False, radio_session=True)
    assert action is Action.COPY_ADDRESS


def test_copy_shortcuts_work_in_files_session_too() -> None:
    # W AMC Ctrl+C / Ctrl+Shift+C nie sa skrotem tylko radia.
    assert resolve(Chord("C", ctrl=True), player_view=False, radio_session=False) is Action.COPY_NAME
    assert resolve(
        Chord("C", ctrl=True, shift=True), player_view=False, radio_session=False
    ) is Action.COPY_ADDRESS


def test_plain_c_is_not_ours_so_letter_navigation_still_works() -> None:
    # Pisanie litery skacze po liscie -- to nalezy do natywnego ListCtrl.
    assert resolve(Chord("C"), player_view=False, radio_session=True) is None


def test_station_address_is_the_stream_url() -> None:
    model = ListModel()
    model.replace(rows_from_stations([{"id": "s1", "name": "Radio", "url": URL}]))
    row = model.selected_row
    assert row is not None
    assert row.address == URL


def test_track_address_is_the_file_path() -> None:
    row = Row(item_id="f", title="a.mp3", kind="track", path="D:\\muzyka\\a.mp3")
    assert row.address == "D:\\muzyka\\a.mp3"


def test_parent_row_has_no_address_to_copy() -> None:
    row = Row(item_id="p", title="..", kind="parent", path=None)
    assert row.address == ""


def test_copy_actions_are_described_in_help() -> None:
    from amc_wx_lite.shortcuts import describe

    entries = dict((chord, label) for chord, label in describe())
    assert entries.get("Ctrl+C") == "Skopiuj nazwe"
    assert entries.get("Ctrl+Shift+C") == "Skopiuj adres"
