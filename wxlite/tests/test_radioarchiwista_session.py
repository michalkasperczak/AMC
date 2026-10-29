"""Nawigacja sesji Radio Archiwisty: poziomy katalogu, odtwarzanie, powroty.

Testy są w pełni offline: Navigator dostaje gotowe wiersze, jakie buduje
moduł źródła z shardów publicznego katalogu. Żaden test nie sięga do sieci
ani do prawdziwych stacji.
"""

from __future__ import annotations

from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import (
    Announce,
    LibraryView,
    Navigator,
    OpenRadioarchiwistaLevel,
    OpenRadioarchiwistaStations,
    PlayMedia,
    SessionId,
)


def make_navigator() -> Navigator:
    navigator = Navigator()
    navigator.active = SessionId.RADIOARCHIWISTA
    return navigator


def station_row(slug: str = "pr1", name: str = "Polskie Radio Jedynka") -> Row:
    return Row(
        item_id=f"rarch:station:{slug}",
        title=name,
        kind="folder",
        service_id=slug,
        service_kind="rarchStation",
        show_kind=False,
    )


def year_row(slug: str = "pr1", year: int = 2025) -> Row:
    return Row(
        item_id=f"rarch:years:{slug}:{year}",
        title=str(year),
        kind="folder",
        service_id=slug,
        service_kind="rarchYear",
        show_kind=False,
    )


def month_row(slug: str = "pr1", year: int = 2025, month: int = 10) -> Row:
    return Row(
        item_id=f"rarch:months:{slug}:{year}:{month}",
        title="październik",
        kind="folder",
        service_id=slug,
        service_kind="rarchMonth",
        show_kind=False,
    )


def day_row(slug: str = "pr1", year: int = 2025, month: int = 10, day: int = 28) -> Row:
    return Row(
        item_id=f"rarch:days:{slug}:{year}:{month}:{day}",
        title=f"{day:02d}.{month:02d}.{year}",
        kind="folder",
        service_id=slug,
        service_kind="rarchDay",
        show_kind=False,
    )


def recording_row(item_id: str = "rarch:rec:a", position: float = 0.0) -> Row:
    return Row(
        item_id=item_id,
        title="08:00",
        kind="recording",
        detail="",
        url="https://archive.org/download/pr1/2025-10-28-08.mp3",
        service_id="pr1",
        position_seconds=position,
        show_kind=False,
    )


def test_enter_na_najnowszych_otwiera_ostatnia_godzine() -> None:
    from amc_wx_lite.navigation import OpenRadioarchiwistaLatest

    navigator = make_navigator()
    latest = Row(
        item_id="rarch:latest",
        title="Najnowsze nagrania",
        kind="folder",
        service_kind="rarchLatest",
        show_kind=False,
    )
    navigator.apply_radioarchiwista_rows(
        LibraryView.RADIOARCHIWISTA_STATIONS, [latest, station_row()]
    )
    navigator.sessions[SessionId.RADIOARCHIWISTA].model.select_id("rarch:latest")
    intents = navigator.activate_selected()
    assert isinstance(intents[0], OpenRadioarchiwistaLatest)
    back = navigator.go_to_parent()
    assert isinstance(back[0], OpenRadioarchiwistaStations)


def test_stacje_i_wejscie_w_lata() -> None:
    navigator = make_navigator()
    intents = navigator.apply_radioarchiwista_rows(
        LibraryView.RADIOARCHIWISTA_STATIONS,
        [station_row("pr1"), station_row("r357", "Radio 357")],
    )
    assert isinstance(intents[0], Announce)
    state = navigator.sessions[SessionId.RADIOARCHIWISTA]
    assert state.library_view is LibraryView.RADIOARCHIWISTA_STATIONS

    state.model.select_id("rarch:station:pr1")
    intents = navigator.activate_selected()
    assert isinstance(intents[0], OpenRadioarchiwistaLevel)
    assert intents[0].view is LibraryView.RADIOARCHIWISTA_YEARS
    assert intents[0].slug == "pr1"


def test_hierarchia_rok_miesiac_dzien_i_powrot() -> None:
    navigator = make_navigator()
    navigator.apply_radioarchiwista_rows(
        LibraryView.RADIOARCHIWISTA_STATIONS, [station_row()], preferred_id="rarch:station:pr1"
    )
    state = navigator.sessions[SessionId.RADIOARCHIWISTA]

    navigator.apply_radioarchiwista_rows(
        LibraryView.RADIOARCHIWISTA_YEARS, [year_row()], slug="pr1", preferred_id="rarch:years:pr1:2025"
    )
    state.model.select_id("rarch:years:pr1:2025")
    intents = navigator.activate_selected()
    entered = intents[0]
    assert isinstance(entered, OpenRadioarchiwistaLevel)
    assert entered.view is LibraryView.RADIOARCHIWISTA_MONTHS
    assert entered.year == 2025

    navigator.apply_radioarchiwista_rows(
        LibraryView.RADIOARCHIWISTA_MONTHS,
        [month_row()],
        slug="pr1",
        year=2025,
        preferred_id="rarch:months:pr1:2025:10",
    )
    state.model.select_id("rarch:months:pr1:2025:10")
    intents = navigator.activate_selected()
    entered = intents[0]
    assert isinstance(entered, OpenRadioarchiwistaLevel)
    assert entered.view is LibraryView.RADIOARCHIWISTA_DAYS
    assert (entered.year, entered.month) == (2025, 10)

    navigator.apply_radioarchiwista_rows(
        LibraryView.RADIOARCHIWISTA_DAYS,
        [day_row()],
        slug="pr1",
        year=2025,
        month=10,
        preferred_id="rarch:days:pr1:2025:10:28",
    )
    state.model.select_id("rarch:days:pr1:2025:10:28")
    intents = navigator.activate_selected()
    entered = intents[0]
    assert isinstance(entered, OpenRadioarchiwistaLevel)
    assert entered.view is LibraryView.RADIOARCHIWISTA_HOURS
    assert (entered.year, entered.month, entered.day) == (2025, 10, 28)

    # Backspace wraca o jeden poziom i staje na opuszczonym dniu.
    back = navigator.go_to_parent()
    assert isinstance(back[0], OpenRadioarchiwistaLevel)
    assert back[0].view is LibraryView.RADIOARCHIWISTA_DAYS
    assert back[0].preferred_id == "rarch:days:pr1:2025:10:28"


def test_backspace_z_lat_wraca_na_stacje() -> None:
    navigator = make_navigator()
    navigator.apply_radioarchiwista_rows(
        LibraryView.RADIOARCHIWISTA_STATIONS,
        [station_row()],
        preferred_id="rarch:station:pr1",
    )
    navigator.sessions[SessionId.RADIOARCHIWISTA].model.select_id("rarch:station:pr1")
    navigator.activate_selected()

    navigator.apply_radioarchiwista_rows(
        LibraryView.RADIOARCHIWISTA_YEARS, [year_row()], slug="pr1"
    )
    back = navigator.go_to_parent()
    assert isinstance(back[0], OpenRadioarchiwistaStations)
    assert back[0].preferred_id == "rarch:station:pr1"


def test_backspace_na_stacjach_bez_histori_jest_cichy() -> None:
    navigator = make_navigator()
    navigator.apply_radioarchiwista_rows(
        LibraryView.RADIOARCHIWISTA_STATIONS, [station_row()]
    )
    assert navigator.go_to_parent() == []


def test_enter_na_nagraniu_odtwarza_z_pozycja() -> None:
    navigator = make_navigator()
    navigator.apply_radioarchiwista_rows(
        LibraryView.RADIOARCHIWISTA_HOURS,
        [recording_row("rarch:rec:a", 0.0), recording_row("rarch:rec:b", 137.0)],
        slug="pr1",
        year=2025,
        month=10,
        day=28,
    )
    state = navigator.sessions[SessionId.RADIOARCHIWISTA]
    state.model.select_id("rarch:rec:b")
    intents = navigator.activate_selected()
    play = next(intent for intent in intents if isinstance(intent, PlayMedia))
    assert play.position_seconds == 137.0
    assert play.source.endswith(".mp3")
    assert state.now_playing_id == "rarch:rec:b"
    from amc_wx_lite.navigation import View

    assert state.view is View.PLAYER


def test_nagranie_bez_pliku_odmawia() -> None:
    navigator = make_navigator()
    row = Row(
        item_id="rarch:rec:bezpliku",
        title="09:00",
        kind="recording",
        url=None,
        show_kind=False,
    )
    navigator.apply_radioarchiwista_rows(
        LibraryView.RADIOARCHIWISTA_HOURS, [row], slug="pr1"
    )
    navigator.sessions[SessionId.RADIOARCHIWISTA].model.select_id(row.item_id)
    intents = navigator.activate_selected()
    assert isinstance(intents[0], Announce)


def test_load_more_przenosi_kontekst_dnia() -> None:
    navigator = make_navigator()
    navigator.apply_radioarchiwista_rows(
        LibraryView.RADIOARCHIWISTA_HOURS,
        [recording_row(), Row(item_id="rarch:load-more", title="Wczytaj więcej", kind="loadMore")],
        slug="pr1",
        year=2025,
        month=10,
        day=28,
    )
    navigator.sessions[SessionId.RADIOARCHIWISTA].model.select_id("rarch:load-more")
    intents = navigator.activate_selected()
    entered = intents[0]
    assert isinstance(entered, OpenRadioarchiwistaLevel)
    assert entered.load_more is True
    assert (entered.slug, entered.year, entered.month, entered.day) == (
        "pr1",
        2025,
        10,
        28,
    )


def test_backspace_z_ulubionych_wraca_na_stacje() -> None:
    navigator = make_navigator()
    navigator.apply_radioarchiwista_rows(
        LibraryView.RADIOARCHIWISTA_FAVORITES, [recording_row()]
    )
    back = navigator.go_to_parent()
    assert isinstance(back[0], OpenRadioarchiwistaStations)


def test_blad_sieci_zostawia_widok_i_zaznaczenie() -> None:
    navigator = make_navigator()
    navigator.apply_radioarchiwista_rows(
        LibraryView.RADIOARCHIWISTA_STATIONS, [station_row()], preferred_id="rarch:station:pr1"
    )
    state = navigator.sessions[SessionId.RADIOARCHIWISTA]
    intents = navigator.apply_radioarchiwista_message(
        "Radio Archiwista: brak połączenia z katalogiem"
    )
    assert isinstance(intents[0], Announce)
    assert state.library_view is LibraryView.RADIOARCHIWISTA_STATIONS
    assert state.model.selected_id == "rarch:station:pr1"
