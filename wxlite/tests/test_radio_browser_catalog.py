"""Radio Browser navigation must keep provider IDs out of spoken labels."""

from amc_wx_lite.radio_browser_catalog import (
    Country,
    OTHER_COUNTRIES,
    countries_by_continent,
    countries_from_payload,
    station_page_from_payload,
)


def test_countries_group_by_continent_and_keep_unknown_codes_reachable() -> None:
    countries = countries_from_payload({"items": [
        {"code": "PL", "name": "Polska", "stationCount": 1},
        {"code": "US", "name": "Stany Zjednoczone", "stationCount": 234},
        {"code": "ZZ", "name": "Inny kraj", "stationCount": 2},
        {"code": "PL", "name": "Duplikat", "stationCount": 90},
        {"code": "bad", "name": "Błędny", "stationCount": 1},
    ]})
    groups = countries_by_continent(countries)
    assert groups["Europa"] == (Country("PL", "Polska", 1),)
    assert groups["Ameryka Północna"] == (
        Country("US", "Stany Zjednoczone", 234),
    )
    assert groups[OTHER_COUNTRIES][0].code == "ZZ"
    assert groups["Europa"][0].label == "Polska, 1 stacja"
    assert "PL" not in groups["Europa"][0].label


def test_station_page_uses_existing_radio_result_contract() -> None:
    page = station_page_from_payload({
        "items": [{
            "id": "radio:private-id",
            "title": "Radio Testowe",
            "kind": "station",
            "remoteKind": "radioStation",
            "address": "https://example.test/live",
            "pageAddress": "https://example.test/",
            "detail": "Polska, AAC, 192 kb/s",
        }],
        "nextOffset": 100,
    })
    assert len(page.entries) == 1
    assert page.entries[0].row.title == "Radio Testowe"
    assert page.entries[0].row.address == "https://example.test/live"
    assert page.entries[0].remote_kind == "radioStation"
    assert "private-id" not in page.entries[0].accessible_label
    assert page.page_addresses[page.entries[0].row.item_id] == "https://example.test/"
    assert page.next_offset == 100
