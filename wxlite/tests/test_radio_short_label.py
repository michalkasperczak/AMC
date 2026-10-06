"""Na liście stacji rodzaj jest zbędny; dane stacji nadal są pełne."""

from amc_wx_lite.list_model import ListModel, rows_from_stations


def test_radio_list_omits_kind_but_keeps_station_metadata() -> None:
    rows = rows_from_stations([
        {"id": "radio-a", "name": "Stacja Nadzieja", "url": "https://example.invalid/a"},
        {"id": "radio-b", "name": "Drugie Radio", "url": "https://example.invalid/b"},
    ])
    model = ListModel()
    model.replace(rows)
    assert model.select_id("radio-b")
    selected = model.selected_row
    assert selected is not None
    assert selected.kind == "station"
    assert selected.kind_label == "stacja"
    assert selected.address == "https://example.invalid/b"
    assert model.text_for(0, 0) == "Stacja Nadzieja", "Nie obcinaj słowa z prawdziwej nazwy"
    assert model.text_for(1, 1) == "", "Jednorodna lista radia nie powtarza rodzaju"
    assert model.text_for(1, 2) == ""
    assert model.selected_id == "radio-b"
