"""Navigation data for the public Radio Browser directory, independent of wx."""

from __future__ import annotations

from dataclasses import dataclass

from .remote_search import radio_entries_from_payload
from .search_catalog import SearchEntry


@dataclass(frozen=True, slots=True)
class Country:
    code: str
    name: str
    station_count: int

    @property
    def label(self) -> str:
        count = self.station_count
        ending = "stacja" if count == 1 else "stacje" if count % 10 in (2, 3, 4) and count % 100 not in (12, 13, 14) else "stacji"
        return f"{self.name}, {count} {ending}"


@dataclass(frozen=True, slots=True)
class StationPage:
    entries: tuple[SearchEntry, ...]
    page_addresses: dict[str, str]
    next_offset: int | None


# Radio Browser provides country codes, not continents. This stable UI grouping
# never changes a provider ID. Unlisted codes remain reachable in Other countries.
_CONTINENT_CODES = {
    "Europa": "AD AL AT AX BA BE BG BY CH CY CZ DE DK EE ES FI FO FR GB GG GI GR HR HU IE IM IS IT JE LI LT LU LV MC MD ME MK MT NL NO PL PT RO RS RU SE SI SJ SK SM TR UA VA XK",
    "Azja": "AE AF AM AZ BD BH BN BT CN GE HK ID IL IN IQ IR JO JP KG KH KP KR KW KZ LA LB LK MM MN MO MV MY NP OM PH PK PS QA SA SG SY TH TJ TL TM TW UZ VN YE",
    "Afryka": "AO BF BI BJ BW CD CF CG CI CM CV DJ DZ EG EH ER ET GA GH GM GN GQ GW KE KM LR LS LY MA MG ML MR MU MW MZ NA NE NG RE RW SC SD SH SL SN SO SS ST SZ TD TG TN TZ UG YT ZA ZM ZW",
    "Ameryka Północna": "AG AI AW BB BL BM BQ BS BZ CA CR CU CW DM DO GD GL GP GT HN HT JM KN KY LC MF MQ MS MX NI PA PM PR SV SX TC TT US VC VG VI",
    "Ameryka Południowa": "AR BO BR CL CO EC FK GF GY PE PY SR UY VE",
    "Australia i Oceania": "AS AU CK FJ FM GU KI MH MP NC NF NR NU NZ PF PG PN PW SB TK TO TV VU WF WS",
    "Antarktyda": "AQ BV GS HM TF",
}
CONTINENT_CODES = {
    name: frozenset(codes.split())
    for name, codes in _CONTINENT_CODES.items()
}
OTHER_COUNTRIES = "Pozostałe kraje"
STATION_ORDERS = (
    ("Alfabetycznie A–Z", "name"),
    ("Najpopularniejsze", "votes"),
    ("Ostatnio zmienione", "changetimestamp"),
)


def countries_from_payload(payload: object) -> tuple[Country, ...]:
    if not isinstance(payload, dict) or not isinstance(payload.get("items"), list):
        raise ValueError("Nieprawidłowa lista krajów Radio Browser")
    countries: list[Country] = []
    seen: set[str] = set()
    for item in payload["items"]:
        if not isinstance(item, dict):
            continue
        code = item.get("code")
        name = item.get("name")
        count = item.get("stationCount")
        if (
            not isinstance(code, str)
            or len(code) != 2
            or not code.isascii()
            or not code.isalpha()
            or not isinstance(name, str)
            or not name.strip()
            or not isinstance(count, int)
            or count < 1
        ):
            continue
        code = code.upper()
        if code in seen:
            continue
        seen.add(code)
        countries.append(Country(code, name.strip(), count))
    return tuple(sorted(countries, key=lambda item: item.name.casefold()))


def countries_by_continent(countries: tuple[Country, ...]) -> dict[str, tuple[Country, ...]]:
    grouped: dict[str, tuple[Country, ...]] = {}
    known = set().union(*CONTINENT_CODES.values())
    for name, codes in CONTINENT_CODES.items():
        members = tuple(country for country in countries if country.code in codes)
        if members:
            grouped[name] = members
    other = tuple(country for country in countries if country.code not in known)
    if other:
        grouped[OTHER_COUNTRIES] = other
    return grouped


def station_page_from_payload(payload: object) -> StationPage:
    entries, _warnings = radio_entries_from_payload(payload)
    assert isinstance(payload, dict)
    raw_next = payload.get("nextOffset")
    if raw_next is not None and (not isinstance(raw_next, int) or raw_next < 0):
        raise ValueError("Nieprawidłowa strona katalogu stacji")
    page_addresses = {
        str(item.get("id")): str(item.get("pageAddress"))
        for item in payload["items"]
        if isinstance(item, dict)
        and isinstance(item.get("id"), str)
        and isinstance(item.get("pageAddress"), str)
        and item["pageAddress"].startswith(("https://", "http://"))
    }
    return StationPage(tuple(entries), page_addresses, raw_next)
