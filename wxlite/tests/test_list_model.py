"""Testy modelu listy: STABILNY wybor przy odswiezaniu i tekst na zadanie."""

from __future__ import annotations

from amc_wx_lite.list_model import ListModel, Row, rows_from_folder_payload, rows_from_stations


def make_rows(*names: str) -> list[Row]:
    return [Row(item_id=f"file:/m/{n}", title=n, kind="track", path=f"/m/{n}") for n in names]


def test_selection_survives_refresh_that_inserts_rows_above() -> None:
    # To jest sedno zglaszanego problemu: skan folderu dosypuje pliki, a fokus
    # ma ZOSTAC na tym samym utworze, nie wrocic na pozycje 0.
    model = ListModel()
    model.replace(make_rows("b.mp3", "c.mp3"))
    model.select_id("file:/m/c.mp3")

    model.replace(make_rows("a.mp3", "b.mp3", "c.mp3"))

    assert model.selected_id == "file:/m/c.mp3"
    assert model.selected_index == 2, "wybor idzie za ID, nie za numerem wiersza"


def test_selection_falls_back_when_selected_row_disappears() -> None:
    model = ListModel()
    model.replace(make_rows("a.mp3", "b.mp3"))
    model.select_id("file:/m/b.mp3")

    model.replace(make_rows("a.mp3"))

    assert model.selected_id == "file:/m/a.mp3"
    assert model.selected_index == 0


def test_refresh_does_not_land_on_parent_entry() -> None:
    rows = [Row(item_id="parent:/m", title="..", kind="parent", path="/m")] + make_rows("a.mp3")
    model = ListModel()
    model.replace(rows)
    assert model.selected_row is not None
    assert model.selected_row.kind == "track", "domyslny wybor pomija wejscie do rodzica"


def test_preferred_id_wins_over_previous_selection() -> None:
    # Powrot z podfolderu: chcemy stanac na folderze, z ktorego wyszlismy.
    model = ListModel()
    model.replace(make_rows("a.mp3"))
    model.select_id("file:/m/a.mp3")

    rows = [Row(item_id="dir:/m/album", title="album", kind="folder", path="/m/album")] + make_rows("a.mp3")
    model.replace(rows, preferred_id="dir:/m/album")

    assert model.selected_id == "dir:/m/album"


def test_text_requested_out_of_range_is_empty_not_error() -> None:
    model = ListModel()
    model.replace(make_rows("a.mp3"))
    assert model.text_for(0) == "a.mp3"
    assert model.text_for(99) == "", "wx pyta o wiersze w trakcie zmiany dlugosci listy"
    assert model.text_for(-1) == ""


def test_empty_model_has_no_selection() -> None:
    model = ListModel()
    model.replace([])
    assert model.selected_id is None
    assert model.selected_index == -1
    assert model.selected_row is None


def test_prefix_search_wraps_around() -> None:
    model = ListModel()
    model.replace(make_rows("ala.mp3", "basia.mp3", "celina.mp3"))
    assert model.find_prefix("b", start_index=0) == 1
    assert model.find_prefix("a", start_index=2) == 0, "szukanie zawija sie na koniec listy"
    assert model.find_prefix("z") == -1


def test_folders_are_openable_tracks_are_not() -> None:
    folder = Row(item_id="dir:/m/x", title="x", kind="folder", path="/m/x")
    track = Row(item_id="file:/m/a.mp3", title="a.mp3", kind="track", path="/m/a.mp3")
    assert folder.is_openable
    assert not track.is_openable


def test_folder_payload_produces_parent_then_folders_then_tracks() -> None:
    payload = {
        "path": "/m/muzyka",
        "parent": "/m",
        "items": [
            {"kind": "folder", "id": "dir:/m/muzyka/album", "title": "album", "path": "/m/muzyka/album"},
            {"kind": "track", "id": "file:/m/muzyka/a.mp3", "title": "a", "path": "/m/muzyka/a.mp3"},
        ],
    }
    rows = rows_from_folder_payload(payload)
    assert [row.kind for row in rows] == ["parent", "folder", "track"]
    assert rows[0].title == ".."


def test_folder_payload_at_drive_root_has_no_parent_row() -> None:
    rows = rows_from_folder_payload({"path": "C:\\", "parent": None, "items": []})
    assert rows == []


def test_station_rows_keep_user_identifiers() -> None:
    rows = rows_from_stations([{"id": "st-1", "name": "Radio", "url": "http://x/s"}])
    assert rows[0].item_id == "st-1"
    assert rows[0].kind == "station"
    assert not rows[0].is_openable, "Enter na stacji odtwarza, nie wchodzi"
