"""Testy prywatnego magazynu: izolacja od pelnego AMC, zapis atomowy, stacje."""

from __future__ import annotations

import json
import os
import tempfile
from pathlib import Path

from amc_wx_lite.state_store import (
    LiteState,
    Options,
    Station,
    StationList,
    StateStore,
    default_state_dir,
)


def temp_store() -> tuple[StateStore, Path]:
    directory = Path(tempfile.mkdtemp(prefix="amc-wx-lite-test-"))
    return StateStore(directory), directory


# ----------------------------------------------------------------- izolacja


def test_state_directory_is_private_and_not_shared_with_full_amc() -> None:
    old = os.environ.pop("AMC_WX_LITE_HOME", None)
    os.environ["APPDATA"] = "C:\\Users\\micha\\AppData\\Roaming"
    try:
        directory = default_state_dir()
    finally:
        if old is not None:
            os.environ["AMC_WX_LITE_HOME"] = old
    assert directory.name == "AMC-wx-Lite"
    assert "AccessibleMediaController" not in str(directory), "nie wchodzimy w dane pelnego AMC"


def test_env_override_wins_so_tests_never_touch_real_state() -> None:
    os.environ["AMC_WX_LITE_HOME"] = "/tmp/amc-wx-lite-xyz"
    try:
        assert default_state_dir() == Path("/tmp/amc-wx-lite-xyz")
    finally:
        os.environ.pop("AMC_WX_LITE_HOME", None)


# -------------------------------------------------------------------- zapis


def test_round_trip_keeps_options_and_stations() -> None:
    store, _ = temp_store()
    state = LiteState(
        options=Options(volume=70, rate=1.5, last_folder="/m/muzyka"),
        stations=[Station.new("Radio 1", "http://a/1")],
    )
    store.save(state)
    loaded = store.load()
    assert loaded.options.volume == 70
    assert loaded.options.rate == 1.5
    assert loaded.options.last_folder == "/m/muzyka"
    assert [s.url for s in loaded.stations] == ["http://a/1"]


def test_missing_file_gives_defaults_not_an_error() -> None:
    store, _ = temp_store()
    state = store.load()
    assert state.options.volume == 35
    assert state.stations == []


def test_corrupted_json_does_not_block_start() -> None:
    store, directory = temp_store()
    (directory / StateStore.FILE_NAME).write_text("{to nie jest json", encoding="utf-8")
    state = store.load()
    assert state.options.volume == 35, "wracamy do domyslnych zamiast wywracac program"


def test_save_is_atomic_and_leaves_no_temporary_files() -> None:
    store, directory = temp_store()
    store.save(LiteState())
    store.save(LiteState(options=Options(volume=50)))
    leftovers = [p.name for p in directory.iterdir() if p.name != StateStore.FILE_NAME]
    assert leftovers == [], f"zostaly pliki tymczasowe: {leftovers}"
    assert store.load().options.volume == 50


def test_failed_save_keeps_previous_good_file() -> None:
    store, directory = temp_store()
    store.save(LiteState(options=Options(volume=42)))
    good = (directory / StateStore.FILE_NAME).read_text(encoding="utf-8")

    class Unserializable:
        pass

    broken = LiteState()
    broken.navigation = {"zly": Unserializable()}  # type: ignore[dict-item]
    try:
        store.save(broken)
    except TypeError:
        pass
    else:
        raise AssertionError("ZALOZENIE NIESPELNIONE: zapis mial sie nie udac")

    assert (directory / StateStore.FILE_NAME).read_text(encoding="utf-8") == good
    assert store.load().options.volume == 42
    leftovers = [p.name for p in directory.iterdir() if p.name != StateStore.FILE_NAME]
    assert leftovers == [], "nieudany zapis nie zostawia smieci"


def test_out_of_range_values_are_clamped_on_load() -> None:
    store, directory = temp_store()
    (directory / StateStore.FILE_NAME).write_text(
        json.dumps({"options": {"volume": 5000, "rate": 99, "timeshift_minutes": 0, "inter_track_silence_ms": 777}}),
        encoding="utf-8",
    )
    options = store.load().options
    assert options.volume == 100
    assert options.rate == 2.0
    assert options.timeshift_minutes == 1
    assert options.inter_track_silence_ms == 0, "tylko wartosci znane silnikowi"


def test_unknown_fields_from_a_newer_version_are_ignored() -> None:
    store, directory = temp_store()
    (directory / StateStore.FILE_NAME).write_text(
        json.dumps({"version": 99, "options": {"volume": 44, "czegosTakiegoNieMa": True}, "stations": []}),
        encoding="utf-8",
    )
    assert store.load().options.volume == 44


def test_broken_station_entries_are_skipped_not_fatal() -> None:
    store, directory = temp_store()
    (directory / StateStore.FILE_NAME).write_text(
        json.dumps({"stations": [{"name": "ok", "url": "http://a/1"}, {"name": "zly"}, "nie-obiekt", {"url": ""}]}),
        encoding="utf-8",
    )
    stations = store.load().stations
    assert len(stations) == 1
    assert stations[0].id, "brakujacy identyfikator zostal nadany"


# ------------------------------------------------------------------- stacje


def test_add_rejects_empty_and_non_http_addresses() -> None:
    stations = StationList()
    for bad in ("", "   ", "file:///c:/tajne.mp3", "ftp://a/b"):
        try:
            stations.add("x", bad)
        except ValueError:
            continue
        raise AssertionError(f"ZALOZENIE NIESPELNIONE: {bad!r} powinno byc odrzucone")
    assert len(stations) == 0


def test_add_uses_address_as_name_when_name_is_empty() -> None:
    stations = StationList()
    station = stations.add("  ", "http://a/1")
    assert station.name == "http://a/1"


def test_duplicate_address_is_refused_with_a_clear_message() -> None:
    stations = StationList()
    stations.add("Radio 1", "http://a/1")
    try:
        stations.add("Inna nazwa", "HTTP://A/1")
    except ValueError as error:
        assert "Radio 1" in str(error), "komunikat wskazuje kolidujaca stacje"
    else:
        raise AssertionError("ZALOZENIE NIESPELNIONE: duplikat mial byc odrzucony")


def test_edit_changes_entry_and_keeps_identifier() -> None:
    stations = StationList()
    station = stations.add("Radio 1", "http://a/1")
    stations.edit(station.id, "Radio Jeden", "http://a/2")
    assert stations.find(station.id) is not None
    assert stations.find(station.id).url == "http://a/2"  # type: ignore[union-attr]


def test_edit_cannot_collide_with_another_station() -> None:
    stations = StationList()
    first = stations.add("A", "http://a/1")
    stations.add("B", "http://a/2")
    try:
        stations.edit(first.id, "A", "http://a/2")
    except ValueError:
        return
    raise AssertionError("ZALOZENIE NIESPELNIONE: kolizja adresu mial byc bledem")


def test_remove_deletes_only_the_chosen_station() -> None:
    stations = StationList()
    first = stations.add("A", "http://a/1")
    stations.add("B", "http://a/2")
    stations.remove(first.id)
    assert [s.name for s in stations.stations] == ["B"]


def test_import_merges_without_duplicating_user_entries() -> None:
    # Importer M3U/PLS siedzi w HOScie (kod AMC). Tu laczymy jego wynik
    # z wlasna lista uzytkownika.
    stations = StationList()
    stations.add("Moje radio", "http://a/1")
    added, skipped = stations.merge_imported(
        [
            {"name": "Z pliku", "url": "http://a/1"},  # duplikat
            {"name": "Nowa", "url": "http://a/9"},
            {"name": "Bez adresu", "url": ""},
        ]
    )
    assert (added, skipped) == (1, 2)
    assert stations.find_by_url("http://a/1").name == "Moje radio"  # type: ignore[union-attr]


def test_station_payload_feeds_the_list_model() -> None:
    from amc_wx_lite.list_model import rows_from_stations

    stations = StationList()
    stations.add("Radio 1", "http://a/1")
    rows = rows_from_stations(stations.as_payload())
    assert rows[0].kind == "station"
    assert rows[0].url == "http://a/1"


def test_navigation_snapshot_survives_round_trip() -> None:
    from amc_wx_lite.navigation import Navigator, SessionId

    store, _ = temp_store()
    nav = Navigator()
    nav.sessions[SessionId.FILES].folder_path = "/m/muzyka"
    store.save(LiteState(navigation=nav.snapshot()))

    fresh = Navigator()
    fresh.restore(store.load().navigation)
    assert fresh.sessions[SessionId.FILES].folder_path == "/m/muzyka"
