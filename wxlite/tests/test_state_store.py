"""Testy prywatnego magazynu: izolacja od pelnego AMC, zapis atomowy, stacje."""

from __future__ import annotations

import json
import os
import tempfile
from pathlib import Path

from amc_wx_lite.navigation import SessionId
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
    os.environ["APPDATA"] = "C:\\Users\\test\\AppData\\Roaming"
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


def test_window_title_preference_round_trips_and_rejects_unknown_value() -> None:
    store, _ = temp_store()
    store.save(LiteState(options=Options(window_title_playback_position="before")))
    assert store.load().options.window_title_playback_position == "before"
    assert Options(window_title_playback_position="unexpected").clamp().window_title_playback_position == "after"


def test_startup_target_round_trips_and_rejects_technical_garbage() -> None:
    store, _ = temp_store()
    store.save(LiteState(options=Options(startup_target="SessionList")))

    assert store.load().options.startup_target == "SessionList"
    assert (
        Options(startup_target="StartupTarget.SessionList")
        .clamp()
        .startup_target
        == "MediaList"
    )


def test_session_order_round_trip_is_normalized_without_exposing_labels() -> None:
    store, directory = temp_store()
    store.save(LiteState(options=Options(session_order=[
        "sonos", "radio", "sonos", "obca-wartosc",
    ])))

    loaded = store.load()

    assert loaded.options.session_order == ["sonos", "radio"]
    raw = json.loads((directory / StateStore.FILE_NAME).read_text(encoding="utf-8"))
    assert raw["session_manager_version"] == StateStore.SESSION_MANAGER_VERSION
    assert raw["options"]["session_order"] == loaded.options.session_order
    assert "Sonos" not in raw["options"]["session_order"]


def test_legacy_last_used_rate_does_not_become_the_default_of_every_session() -> None:
    # Przed 6ce2241 ``rate`` było ostatnio użytą prędkością (np. 2x podcastu)
    # i bez migracji radio na żywo startowało z 2x.
    store, _ = temp_store()
    store.path.write_text(json.dumps({
        "version": 1,
        "session_manager_version": 1,
        "options": {"rate": 2.0, "volume": 70},
    }), encoding="utf-8")

    loaded = store.load()

    assert loaded.options.rate == 1.0
    assert loaded.options.volume == 70
    store.save(loaded)
    assert json.loads(store.path.read_text(encoding="utf-8"))["playback_rate_version"] == (
        StateStore.PLAYBACK_RATE_VERSION
    )


def test_legacy_state_migration_preserves_order_and_does_not_disable_services() -> None:
    store, _ = temp_store()
    store.path.write_text(json.dumps({
        "version": 1,
        "options": {"session_order": ["sonos", "radio", "sonos"]},
        "navigation": {"active_session": "sonos", "sessions": {}},
    }), encoding="utf-8")

    loaded = store.load()

    assert loaded.options.session_order[:2] == ["sonos", "radio"]
    assert set(loaded.options.session_order) == set(item.value for item in SessionId)
    assert loaded.navigation["active_session"] == "sonos"


def test_missing_state_starts_with_local_radio_and_podcasts_only() -> None:
    store, _ = temp_store()

    loaded = store.load()

    assert loaded.options.session_order == ["files", "radio", "podcasts"]
    assert loaded.navigation == {}


def test_round_trip_keeps_private_audio_edit_backup_choice() -> None:
    store, _ = temp_store()
    store.save(LiteState(options=Options(keep_audio_edit_backups=True)))

    assert store.load().options.keep_audio_edit_backups is True


def test_invalid_audio_edit_backup_choice_returns_to_inherited_value() -> None:
    assert Options(keep_audio_edit_backups="tak").clamp().keep_audio_edit_backups is None  # type: ignore[arg-type]


def test_round_trip_keeps_and_clamps_private_custom_seek_length() -> None:
    store, _ = temp_store()
    store.save(LiteState(options=Options(custom_seek_seconds=9999)))

    assert store.load().options.custom_seek_seconds == 1800


def test_invalid_custom_seek_type_returns_to_inherited_value() -> None:
    assert Options(custom_seek_seconds="pięć").clamp().custom_seek_seconds is None  # type: ignore[arg-type]


def test_round_trip_keeps_global_transient_preview_choice() -> None:
    store, _ = temp_store()
    store.save(LiteState(options=Options(global_transient_previews=False)))

    assert store.load().options.global_transient_previews is False


def test_invalid_global_transient_preview_choice_returns_to_inherited_value() -> None:
    assert Options(global_transient_previews="nie").clamp().global_transient_previews is None  # type: ignore[arg-type]


def test_local_resume_override_round_trips_and_invalid_value_inherits() -> None:
    store, _ = temp_store()
    store.save(LiteState(options=Options(remember_local_playback_positions=False)))
    assert store.load().options.remember_local_playback_positions is False
    assert (
        Options(remember_local_playback_positions="tak")
        .clamp()
        .remember_local_playback_positions
        is None
    )


def test_detailed_hints_override_round_trips_and_invalid_value_inherits() -> None:
    store, _ = temp_store()
    store.save(LiteState(options=Options(detailed_hints=True)))
    assert store.load().options.detailed_hints is True
    assert Options(detailed_hints="tak").clamp().detailed_hints is None  # type: ignore[arg-type]


def test_message_overrides_round_trip_and_keep_user_facing_mode() -> None:
    store, _ = temp_store()
    store.save(LiteState(options=Options(
        message_enabled=False,
        message_seek_messages=False,
        message_arrow_seek_messages=True,
        message_percentage_seek_messages=False,
        message_volume_messages=True,
        message_playback_messages=False,
        message_bookmark_navigation_messages=False,
        message_percent_announcement="PercentAndTime",
    )))

    loaded = store.load().options

    assert loaded.message_enabled is False
    assert loaded.message_seek_messages is False
    assert loaded.message_arrow_seek_messages is True
    assert loaded.message_percentage_seek_messages is False
    assert loaded.message_volume_messages is True
    assert loaded.message_playback_messages is False
    assert loaded.message_bookmark_navigation_messages is False
    assert loaded.message_percent_announcement == "PercentAndTime"


def test_invalid_message_overrides_return_to_inherited_values() -> None:
    options = Options(
        message_enabled="tak",  # type: ignore[arg-type]
        message_seek_messages=1,  # type: ignore[arg-type]
        message_bookmark_navigation_messages="tak",  # type: ignore[arg-type]
        message_percent_announcement="TechnicalEnum.Value",
    ).clamp()

    assert options.message_enabled is None
    assert options.message_seek_messages is None
    assert options.message_bookmark_navigation_messages is None
    assert options.message_percent_announcement is None


def test_component_update_choices_round_trip_and_invalid_values_inherit() -> None:
    store, _ = temp_store()
    store.save(LiteState(options=Options(
        component_check_automatically=False,
        component_download_automatically=True,
    )))

    loaded = store.load().options
    assert loaded.component_check_automatically is False
    assert loaded.component_download_automatically is True

    invalid = Options(
        component_check_automatically="nie",  # type: ignore[arg-type]
        component_download_automatically=1,  # type: ignore[arg-type]
    ).clamp()
    assert invalid.component_check_automatically is None
    assert invalid.component_download_automatically is None


def test_application_update_choice_round_trips_and_invalid_value_defaults_on() -> None:
    store, _ = temp_store()
    store.save(LiteState(options=Options(application_check_automatically=False)))

    assert store.load().options.application_check_automatically is False
    assert Options().application_check_automatically is True
    invalid = Options(
        application_check_automatically="nie",  # type: ignore[arg-type]
    ).clamp()
    assert invalid.application_check_automatically is True


def test_message_templates_round_trip_empty_text_and_reject_unknown_values() -> None:
    store, directory = temp_store()
    store.save(LiteState(options=Options(message_templates={
        "favorite.added": "Mam: {item}",
        "queue.removed": "",
        "RuntimeObject { Id = private }": "nie zapisuj",
        "time.total": 123,  # type: ignore[dict-item]
    })))

    loaded = store.load().options.message_templates

    assert loaded == {
        "favorite.added": "Mam: {item}",
        "queue.removed": "",
    }
    raw = json.loads((directory / StateStore.FILE_NAME).read_text(encoding="utf-8"))
    assert "RuntimeObject" not in json.dumps(raw, ensure_ascii=False)


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


def test_round_trip_keeps_private_tidal_sort_per_collection_view() -> None:
    store, _ = temp_store()
    store.save(LiteState(tidal_sort_modes={
        "library": "Alphabetical",
        "favorites": "AddedNewest",
        "playlists": "Custom",
    }))

    assert store.load().tidal_sort_modes == {
        "library": "Alphabetical",
        "favorites": "AddedNewest",
        "playlists": "Custom",
    }


def test_round_trip_keeps_private_spotify_sort_per_collection_view() -> None:
    store, _ = temp_store()
    store.save(LiteState(spotify_sort_modes={
        "liked": "Alphabetical",
        "albums": "AddedNewest",
        "podcasts": "Custom",
    }))

    assert store.load().spotify_sort_modes == {
        "liked": "Alphabetical",
        "albums": "AddedNewest",
        "podcasts": "Custom",
    }


def test_search_history_is_sanitized_bounded_and_kept_per_scope() -> None:
    store, _ = temp_store()
    values = [" Radio ", "radio", *(str(index) for index in range(60)), 7]
    store.save(LiteState(search_history={
        "files": values,
        "global": ["Podcast"],
        "broken": "not a list",
    }))

    loaded = store.load().search_history
    assert loaded["files"][0] == "Radio"
    assert len(loaded["files"]) == 50
    assert loaded["global"] == ["Podcast"]
    assert "broken" not in loaded


def test_round_trip_keeps_only_safe_private_tidal_queue_fields() -> None:
    store, _ = temp_store()
    store.save(LiteState(tidal_queue_items=[{
        "itemId": "tidal:tracks:1",
        "externalId": "tracks:1",
        "title": "Utwór",
        "kind": "track",
        "artist": "Wykonawca",
        "relatedAlbumExternalId": "albums:44",
        "relatedAlbumTitle": "Album",
        "relatedArtistExternalId": "artists:7",
        "relatedArtistName": "Wykonawca",
        "token": "nie zapisuj",
    }]))

    loaded = store.load().tidal_queue_items

    assert len(loaded) == 1
    assert loaded[0]["title"] == "Utwór"
    assert loaded[0]["relatedArtistExternalId"] == "artists:7"
    assert loaded[0]["relatedArtistName"] == "Wykonawca"
    assert "token" not in loaded[0]


def test_round_trip_keeps_only_safe_private_spotify_history_fields() -> None:
    store, _ = temp_store()
    store.save(LiteState(spotify_playback_history=[{
        "itemId": "private-row",
        "externalId": "spotify-track-id",
        "title": "Utwór",
        "kind": "track",
        "artist": "Wykonawca",
        "durationTicks": 123,
        "token": "nie zapisuj",
    }]))

    loaded = store.load().spotify_playback_history

    assert len(loaded) == 1
    assert loaded[0]["title"] == "Utwór"
    assert loaded[0]["durationTicks"] == 123
    assert "token" not in loaded[0]


def test_round_trip_keeps_separate_sanitized_streamer_histories() -> None:
    store, _ = temp_store()
    store.save(LiteState(playback_history_by_session={
        "wiim": [{
            "itemId": "wiim:observed:one",
            "title": "Audycja",
            "kind": "station",
            "source": "radio internetowe",
            "replaySupported": False,
            "runtimeObject": "WiiMPlaybackInformation { secret = 1 }",
        }],
        "sonos": [{
            "itemId": "sonos:observed:one",
            "title": "Utwór",
            "kind": "track",
            "artist": "Wykonawca",
            "replaySupported": False,
            "token": "nie zapisuj",
        }],
        "local": [{
            "itemId": "local-file",
            "title": "Nie wolno importować",
            "kind": "track",
        }],
    }))

    loaded = store.load().playback_history_by_session

    assert set(loaded) == {"wiim", "sonos"}
    assert loaded["wiim"][0]["title"] == "Audycja"
    assert "runtimeObject" not in loaded["wiim"][0]
    assert loaded["sonos"][0]["artist"] == "Wykonawca"
    assert "token" not in loaded["sonos"][0]


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


def test_unknown_tidal_sort_views_and_values_are_not_restored_or_rewritten() -> None:
    store, directory = temp_store()
    store.path.write_text(json.dumps({
        "tidal_sort_modes": {
            "library": "Alphabetical",
            "favorites": "CollectionSortMode.Custom { technical = 1 }",
            "secretView": "Custom",
        }
    }), encoding="utf-8")

    loaded = store.load()
    assert loaded.tidal_sort_modes == {"library": "Alphabetical"}
    store.save(loaded)
    payload = json.loads(store.path.read_text(encoding="utf-8"))
    assert payload["tidal_sort_modes"] == {"library": "Alphabetical"}
    assert "technical" not in json.dumps(payload, ensure_ascii=False)


def test_unknown_spotify_sort_views_and_values_are_not_restored_or_rewritten() -> None:
    store, _ = temp_store()
    store.path.write_text(json.dumps({
        "spotify_sort_modes": {
            "liked": "Alphabetical",
            "episodes": "CollectionSortMode.Custom { technical = 1 }",
            "secretView": "Custom",
        }
    }), encoding="utf-8")

    loaded = store.load()
    assert loaded.spotify_sort_modes == {"liked": "Alphabetical"}
    store.save(loaded)
    payload = json.loads(store.path.read_text(encoding="utf-8"))
    assert payload["spotify_sort_modes"] == {"liked": "Alphabetical"}
    assert "technical" not in json.dumps(payload, ensure_ascii=False)


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


def test_round_trip_keeps_only_safe_last_spotify_playlist_destinations(tmp_path) -> None:
    store = StateStore(tmp_path)
    state = LiteState(
        spotify_playlist_destination_ids=[
            "playlist-one",
            " playlist-two ",
            "playlist-one",
            "",
        ]
    )

    store.save(state)
    loaded = store.load()

    assert loaded.spotify_playlist_destination_ids == [
        "playlist-one",
        "playlist-two",
    ]
    assert loaded.service_picker_destination_ids == {
        "spotify:playlists": ["playlist-one", "playlist-two"]
    }


def test_round_trip_partitions_remote_picker_destinations_by_service(tmp_path) -> None:
    store = StateStore(tmp_path)
    state = LiteState(
        service_picker_destination_ids={
            "spotify:playlists": [" spotify-one ", "spotify-one", "spotify-two"],
            "tidal:playlists": ["tidal-one"],
            "Nieprawidłowy zakres": ["do-usunięcia"],
        }
    )

    store.save(state)
    loaded = store.load()

    assert loaded.service_picker_destination_ids == {
        "spotify:playlists": ["spotify-one", "spotify-two"],
        "tidal:playlists": ["tidal-one"],
    }
    assert loaded.spotify_playlist_destination_ids == [
        "spotify-one",
        "spotify-two",
    ]


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
            "wiim-device:salon": [{
                "slot": 2,
                "targetId": "native-preset:5",
                "targetKind": "wiimNativePreset",
                "targetTitle": "Radio Poznań",
            }],
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
        assert loaded.preset_overrides["wiim-device:salon"] == [{
            "slot": 2,
            "targetId": "native-preset:5",
            "targetKind": "wiimNativePreset",
            "targetTitle": "Radio Poznań",
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
        json.dumps({
            "playback_rate_version": StateStore.PLAYBACK_RATE_VERSION,
            "options": {"volume": 5000, "rate": 99, "timeshift_minutes": 0, "inter_track_silence_ms": 777},
        }),
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


def test_youtube_live_channel_selection_is_unique_bounded_and_persistent() -> None:
    store, _ = temp_store()
    store.save(LiteState(
        youtube_live_channel_ids=[
            " kanał-1 ",
            "kanał-1",
            *[f"kanał-{index}" for index in range(2, 30)],
            "",
        ]
    ))
    loaded = store.load()
    assert loaded.youtube_live_channel_ids[0] == "kanał-1"
    assert len(loaded.youtube_live_channel_ids) == 20
    assert len(set(loaded.youtube_live_channel_ids)) == 20


def test_spotify_queue_round_trip_keeps_order_and_safe_fields() -> None:
    store, _ = temp_store()
    store.save(LiteState(spotify_queue_items=[
        {"itemId": "one", "externalId": "first", "title": "Pierwszy", "kind": "track", "token": "secret"},
        {"itemId": "two", "externalId": "second", "title": "Drugi", "kind": "episode", "token": "secret"},
    ]))

    loaded = store.load().spotify_queue_items

    assert [item["title"] for item in loaded] == ["Pierwszy", "Drugi"]
    assert all("token" not in item for item in loaded)
