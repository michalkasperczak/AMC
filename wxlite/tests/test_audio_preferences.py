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
    }
