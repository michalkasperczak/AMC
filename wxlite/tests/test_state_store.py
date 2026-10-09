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


def test_round_trip_keeps_radio_activity_presentation_options() -> None:
    store, _ = temp_store()
    state = LiteState(options=Options(
        radio_playback_state_position="before",
        radio_recording_state_position="off",
        radio_playback_state_sound=True,
        radio_recording_state_sound=True,
    ))

    store.save(state)
    loaded = store.load().options

    assert loaded.radio_playback_state_position == "before"
    assert loaded.radio_recording_state_position == "off"
    assert loaded.radio_playback_state_sound is True
    assert loaded.radio_recording_state_sound is True


def test_round_trip_keeps_private_podcast_inbox_sort_override() -> None:
    store, _ = temp_store()
    store.save(LiteState(podcast_inbox_sort_mode="Custom"))

    assert store.load().podcast_inbox_sort_mode == "Custom"


def test_unknown_podcast_sort_value_is_not_restored_or_rewritten() -> None:
    store, directory = temp_store()
    store.path.write_text(json.dumps({
        "podcast_inbox_sort_mode": "CollectionSortMode.Custom { technical = 1 }"
    }), encoding="utf-8")

    loaded = store.load()
    assert loaded.podcast_inbox_sort_mode is None
    store.save(loaded)
    payload = json.loads(store.path.read_text(encoding="utf-8"))
    assert "podcast_inbox_sort_mode" not in payload


def test_invalid_radio_activity_options_return_to_safe_defaults() -> None:
    options = Options(
        radio_playback_state_position="enum-z-kodu",
        radio_recording_state_position="",
        radio_playback_state_sound="tak",  # type: ignore[arg-type]
        radio_recording_state_sound=1,  # type: ignore[arg-type]
    ).clamp()

    assert options.radio_playback_state_position == "after"
    assert options.radio_recording_state_position == "after"
    assert options.radio_playback_state_sound is False
    assert options.radio_recording_state_sound is False


def test_round_trip_keeps_private_recording_history() -> None:
    store, _ = temp_store()
    state = LiteState(recording_history=[{
        "id": "nagranie-1",
        "stationId": "stacja-1",
        "stationName": "Radio Test",
        "path": r"D:\Nagrania\audycja.mp3",
        "outcome": "Stopped",
        "reason": "",
        "scheduleName": "Poranna audycja",
        "startedUtcTicks": 100,
        "finishedUtcTicks": 200,
        "savedFileCount": 1,
        "technicalObject": {"nie": "zapisuj"},
    }])
    store.save(state)
    loaded = store.load()
    assert loaded.recording_history == [{
        "id": "nagranie-1",
        "stationId": "stacja-1",
        "stationName": "Radio Test",
        "path": r"D:\Nagrania\audycja.mp3",
        "reason": "",
        "scheduleName": "Poranna audycja",
        "outcome": "stopped",
        "startedUtcTicks": 100,
        "finishedUtcTicks": 200,
        "savedFileCount": 1,
    }]


def test_recording_history_rejects_invalid_ids_and_duplicate_objects() -> None:
    store, directory = temp_store()
    (directory / StateStore.FILE_NAME).write_text(json.dumps({
        "recording_history": [
            {"id": "", "stationName": "bez identyfikatora"},
            {"id": "jedno", "stationName": {"repr": "nie"}, "savedFileCount": True},
            {"id": "jedno", "stationName": "duplikat"},
            "obcy obiekt",
        ]
    }), encoding="utf-8")
    history = store.load().recording_history
    assert len(history) == 1
    assert history[0]["id"] == "jedno"
    assert history[0]["stationName"] == ""
    assert history[0]["savedFileCount"] == 0


def test_round_trip_keeps_private_audio_clip_selections() -> None:
    with tempfile.TemporaryDirectory() as raw:
        store = StateStore(Path(raw))
        state = LiteState(clip_selections=[{
            "itemId": "file:żółć",
            "sourcePath": r"D:\Muzyka\Żółć.wav",
            "startSeconds": 1.25,
            "endSeconds": 9.75,
        }])
        store.save(state)
        assert store.load().clip_selections == [{
            "itemId": "file:żółć",
            "sourcePath": r"D:\Muzyka\Żółć.wav",
            "startSeconds": 1.25,
            "endSeconds": 9.75,
        }]


def test_round_trip_keeps_private_preset_override_including_empty_session() -> None:
    with tempfile.TemporaryDirectory(prefix="amc-wx-state-") as directory:
        store = StateStore(directory)
        state = LiteState(preset_overrides={
            "files": [{
                "slot": 4,
                "targetId": "dir:D:\\Muzyka",
                "targetKind": "folder",
                "targetTitle": "Muzyka",
                "targetLocation": "D:\\Muzyka",
            }],
            "radio": [],
        })

        store.save(state)
        loaded = store.load()

        assert loaded.preset_overrides["radio"] == []
        assert loaded.preset_overrides["files"] == [{
            "slot": 4,
            "targetId": "dir:D:\\Muzyka",
            "targetKind": "folder",
            "targetTitle": "Muzyka",
            "targetLocation": "D:\\Muzyka",
        }]


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
