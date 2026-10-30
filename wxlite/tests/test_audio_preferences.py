"""Integration choices for real tempo engines; no GUI/audio claim."""
import json
import tempfile
from pathlib import Path
from amc_wx_lite.state_store import Options, StateStore, LiteState


def test_new_lite_profile_selects_speech_and_round_trips_each_algorithm():
    assert getattr(Options(), "tempo_algorithm", None) == 1
    with tempfile.TemporaryDirectory() as folder:
        store = StateStore(folder)
        for algorithm in (0, 1, 2):
            state = LiteState()
            state.options.tempo_algorithm = algorithm
            store.save(state)
            assert store.load().options.tempo_algorithm == algorithm


def test_invalid_algorithm_does_not_turn_into_a_different_valid_choice():
    for value in (-1, 3, "music", True, None):
        options = Options()
        options.tempo_algorithm = value
        assert options.clamp().tempo_algorithm == 1


def test_broken_options_container_keeps_valid_stations():
    with tempfile.TemporaryDirectory() as folder:
        store = StateStore(folder)
        for invalid in (["volume"], "broken", 10, None):
            store.path.write_text(json.dumps({"options": invalid, "stations": [
                {"id": "one", "name": "Próba", "url": "https://example.invalid/radio"}
            ]}), encoding="utf-8")
            state = store.load()
            assert state.options.volume == 35
            assert state.stations[0].id == "one"


def test_options_payload_contains_the_selected_real_algorithm():
    options = Options()
    assert hasattr(options, "audio_payload"), "One payload is used at startup and after choosing an algorithm"
    options.tempo_algorithm = 2
    options.loudness_normalization = True
    assert options.audio_payload() == {
        "tempoAlgorithm": 2, "loudnessNormalization": True,
        "smoothTrackTransitions": False, "interTrackSilenceMs": 0,
        "pauseOnOutputFallback": False,
    }


def test_radio_recording_overrides_round_trip_and_are_clamped():
    with tempfile.TemporaryDirectory() as folder:
        state = LiteState()
        state.options.radio_recording_format = "aac"
        state.options.radio_recording_bitrate_kbps = 255
        state.options.radio_recordings_folder = r"D:\Nagrania"
        state.options.radio_use_podcast_downloads_folder = True
        state.options.radio_separate_station_folders = True
        state.options.radio_prefer_station_folder_in_new_schedules = False
        store = StateStore(folder)
        store.save(state)
        loaded = store.load().options
    assert loaded.radio_recording_format == "Aac"
    assert loaded.radio_recording_bitrate_kbps == 256
    assert loaded.radio_recordings_folder == r"D:\Nagrania"
    assert loaded.radio_use_podcast_downloads_folder is True
    assert loaded.radio_separate_station_folders is True
    assert loaded.radio_prefer_station_folder_in_new_schedules is False


def test_invalid_radio_recording_overrides_return_to_profile_inheritance():
    options = Options()
    options.radio_recording_format = "TechnicalEnumValue"
    options.radio_recording_bitrate_kbps = True
    options.radio_prefer_station_folder_in_new_schedules = "false"
    options.radio_use_podcast_downloads_folder = "false"
    options.clamp()
    assert options.radio_recording_format is None
    assert options.radio_recording_bitrate_kbps is None
    assert options.radio_use_podcast_downloads_folder is None
    assert options.radio_prefer_station_folder_in_new_schedules is None


def test_podcast_global_settings_round_trip_without_technical_labels():
    with tempfile.TemporaryDirectory() as folder:
        state = LiteState()
        state.options.podcast_downloads_folder = r"D:\Podcasty"
        state.options.podcast_separate_source_folders = True
        state.options.podcast_rss_refresh_interval_minutes = 45
        state.options.podcast_youtube_refresh_interval_minutes = 180
        state.options.podcast_automatic_refresh_batch_size = 7
        store = StateStore(folder)
        store.save(state)
        loaded = store.load().options
    assert loaded.podcast_downloads_folder == r"D:\Podcasty"
    assert loaded.podcast_separate_source_folders is True
    assert loaded.podcast_rss_refresh_interval_minutes == 45
    assert loaded.podcast_youtube_refresh_interval_minutes == 180
    assert loaded.podcast_automatic_refresh_batch_size == 7


def test_podcast_global_settings_are_clamped_or_return_to_inheritance():
    options = Options()
    options.podcast_downloads_folder = "  D:/Podcasty  "
    options.podcast_rss_refresh_interval_minutes = -10
    options.podcast_youtube_refresh_interval_minutes = 20_000
    options.podcast_automatic_refresh_batch_size = 500
    options.clamp()
    assert options.podcast_downloads_folder == "D:/Podcasty"
    assert options.podcast_rss_refresh_interval_minutes == 0
    assert options.podcast_youtube_refresh_interval_minutes == 10_080
    assert options.podcast_automatic_refresh_batch_size == 50

    options.podcast_rss_refresh_interval_minutes = True
    options.podcast_youtube_refresh_interval_minutes = "nie liczba"
    options.podcast_automatic_refresh_batch_size = False
    options.clamp()
    assert options.podcast_rss_refresh_interval_minutes is None
    assert options.podcast_youtube_refresh_interval_minutes is None
    assert options.podcast_automatic_refresh_batch_size is None
