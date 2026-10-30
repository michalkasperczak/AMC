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


def test_nazwa_sesji_jest_zdefiniowana_dla_calego_enumu() -> None:
    # Regresja z zywej maszyny: brak wpisu RADIOARCHIWISTA w mapie nazw
    # navigatora ubijal cale GUI (KeyError w switch_session przy Ctrl+4),
    # a testy przechodzily, bo nikt nie przelaczal NA te sesje. Mapa musi
    # pokrywac CALY enum — inaczej pierwszy uzytkownik nowej sesji traci okno.
    for session_id in SessionId:
        name = Navigator._session_name(session_id)
        assert isinstance(name, str) and name.strip()


def test_przelaczenie_na_i_z_radioarchiwisty_oglasza_nazwe() -> None:
    # Ctrl+4 wchodzil w sesje, ale kazde przełączenie ogłasza nazwe PRZEZ
    # mape nazw; sprawdzamy obie strony dla kazdej sesji, lacznie z powrotem.
    for target in SessionId:
        navigator = Navigator()
        navigator.active = SessionId.RADIOARCHIWISTA
        intents = navigator.switch_session(target)
        assert intents and isinstance(intents[0], Announce)
        assert intents[0].text.strip()
        back = navigator.switch_session(SessionId.RADIOARCHIWISTA)
        assert back and isinstance(back[0], Announce)
        assert "Radio Archiwista" in back[0].text


def test_enter_dziala_na_wierszach_prosty_z_builderow_katalogu() -> None:
    # Regresja z odbioru: buildery year_rows/month_rows/day_rows NIE
    # ustawialy service_kind ani service_id, wiec Enter konczyl sie
    # „nie można otworzyć". Testy hierarchii przechodzily, bo uzywaly
    # receznych helperow z service_kind. Tu wiersze ida PROSTO z
    # produkcyjnych builderow, bez przerabiania.
    from amc_wx_lite import radioarchiwista_source as source

    navigator = make_navigator()
    state = navigator.sessions[SessionId.RADIOARCHIWISTA]

    navigator.apply_radioarchiwista_rows(
        LibraryView.RADIOARCHIWISTA_YEARS,
        source.year_rows("pr1", (2025,)),
        slug="pr1",
        preferred_id="rarch:years:pr1:2025",
    )
    state.model.select_id("rarch:years:pr1:2025")
    entered = navigator.activate_selected()[0]
    assert isinstance(entered, OpenRadioarchiwistaLevel)
    assert entered.view is LibraryView.RADIOARCHIWISTA_MONTHS

    navigator.apply_radioarchiwista_rows(
        LibraryView.RADIOARCHIWISTA_MONTHS,
        source.month_rows("pr1", 2025, (10,)),
        slug="pr1",
        year=2025,
        preferred_id="rarch:months:pr1:2025:10",
    )
    state.model.select_id("rarch:months:pr1:2025:10")
    entered = navigator.activate_selected()[0]
    assert isinstance(entered, OpenRadioarchiwistaLevel)
    assert entered.view is LibraryView.RADIOARCHIWISTA_DAYS

    navigator.apply_radioarchiwista_rows(
        LibraryView.RADIOARCHIWISTA_DAYS,
        source.day_rows("pr1", 2025, 10, (28,)),
        slug="pr1",
        year=2025,
        month=10,
        preferred_id="rarch:days:pr1:2025:10:28",
    )
    state.model.select_id("rarch:days:pr1:2025:10:28")
    entered = navigator.activate_selected()[0]
    assert isinstance(entered, OpenRadioarchiwistaLevel)
    assert entered.view is LibraryView.RADIOARCHIWISTA_HOURS


def test_najnowsze_nazywaja_stacje_w_tytule() -> None:
    # Zgłoszenie z odbioru: w widoku „Najnowsze" byla sama godzina (23:00)
    # i nie wiadomo, ktorej stacji dotyczy. Tytul musi nazywac stacje.
    from amc_wx_lite import radioarchiwista_source as source

    recordings = (
        source.ArchiveRecording(
            station_slug="pr1", station_name="Polskie Radio Program 1",
            item_id="rarch:rec:pr1:a", title="29.10 23:00",
            audio_url="https://example.net/a.mp3", duration_seconds=3600.0,
            local_date_label="29.10.2026", local_hour_label="23:00",
            local_label="29.10 23:00", utc_started_at="2026-10-29T23:00:00Z",
            hour=23, completeness_ratio=1.0, status="published",
        ),
        source.ArchiveRecording(
            station_slug="r357", station_name="Radio 357",
            item_id="rarch:rec:r357:b", title="29.10 23:00",
            audio_url="https://example.net/b.mp3", duration_seconds=1800.0,
            local_date_label="29.10.2026", local_hour_label="23:00",
            local_label="29.10 23:00", utc_started_at="2026-10-29T23:00:00Z",
            hour=23, completeness_ratio=0.4, status="published",
        ),
    )
    rows = source.latest_rows(recordings, favorite_ids={"rarch:rec:r357:b"})
    assert rows[0].title == "Polskie Radio Program 1, 29.10.2026 23:00"
    assert rows[1].title == "Radio 357, 29.10.2026 23:00"
    # Ulubiona z gwiazdka, niepelne z opisem w szczegolach.
    assert rows[0].state_prefix == ""
    assert rows[1].state_prefix == "★ "
    assert "niepełne" in rows[1].detail


def test_odmiana_i_kompletnosc_kluczy_rarch() -> None:
    from amc_wx_lite import i18n

    i18n.configure("pl")
    assert i18n.recordings_phrase(1) == "1 nagranie"
    assert i18n.recordings_phrase(3) == "3 nagrania"
    assert i18n.recordings_phrase(5) == "5 nagrań"
    assert i18n.recordings_phrase(12) == "12 nagrań"
    assert i18n.recordings_phrase(22) == "22 nagrania"
    assert i18n.recordings_phrase(4916) == "4916 nagrań"
    assert i18n.month_name(10) == "październik"

    i18n.configure("en")
    try:
        assert i18n.recordings_phrase(1) == "1 recording"
        assert i18n.recordings_phrase(3) == "3 recordings"
        assert i18n.month_name(10) == "October"
        assert i18n.tr("rarch.latest.menu") == "Latest recordings"
        # Kompletnosc: kazdy klucz rarch.* z PL ma odpowiednik w EN.
        polish = i18n.catalogs()[i18n.POLISH]
        english = i18n.catalogs()[i18n.ENGLISH]
        missing = sorted(
            key for key in polish
            if key.startswith("rarch.") and key not in english
        )
        assert not missing, missing
    finally:
        i18n.configure("pl")


def test_start_zapamietanej_sesji_dociaga_stacje() -> None:
    """Start z aktywna sesja Radio Archiwista nie moze pokazywac pustej listy.

    Navigator.restore odtwarza sesje, widok i fokus, ale wiersze katalogu
    pobiera dopiero otwarcie poziomu. Wczesniej robilo to wylacznie Ctrl+4,
    wiec po restarcie z zapamietana sesja uzytkownik widzial pusta liste.
    """
    from types import SimpleNamespace

    from amc_wx_lite.gui import LiteFrame

    navigator = make_navigator()
    navigator.sessions[SessionId.RADIOARCHIWISTA].list_anchor_id = "rarch:anchor"
    opened: list[str | None] = []
    frame = SimpleNamespace(
        navigator=navigator,
        announcer=SimpleNamespace(say=lambda *_args, **_kwargs: None),
        stations=SimpleNamespace(as_payload=lambda: []),
        _radio_snapshot=SimpleNamespace(current_id=None, load_error=None),
        options=SimpleNamespace(last_folder=""),
        library=SimpleNamespace(is_available=False, describe=lambda: ""),
        messages=SimpleNamespace(custom_seek_seconds=300),
        _radio_effective_sort_modes={},
        _run=lambda _intents: None,
        _open_radio_view=lambda _intent, **_kwargs: None,
        _open_podcast_library=lambda **_kwargs: None,
        _load_wiim_devices=lambda **_kwargs: None,
        _load_frontier_smart_devices=lambda **_kwargs: None,
        _load_sonos_targets=lambda **_kwargs: None,
        _open_radioarchiwista_stations=(
            lambda preferred_id=None, **_kwargs: opened.append(preferred_id)
        ),
    )
    LiteFrame._load_initial_content(frame)
    assert opened == ["rarch:anchor"], (
        "Start z zapamietana sesja Radio Archiwista nie otworzyl katalogu stacji"
    )
