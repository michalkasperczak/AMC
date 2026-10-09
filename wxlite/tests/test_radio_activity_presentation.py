"""Stan stacji jest prezentacja listy, nie druga implementacja odtwarzania."""

from __future__ import annotations

import io
import struct
import wave

from amc_wx_lite.list_model import ListModel, Row
from amc_wx_lite.radio_activity import (
    STATE_POSITION_LABELS,
    activity_cue_wav,
    normalize_state_position,
)
from amc_wx_lite.radio_recording import station_activity_rows


def decorated(*, playback_position: str, recording_position: str) -> Row:
    return station_activity_rows(
        [Row("radio", "Radio Poznań", "station")],
        transport_status={
            "engine": "radio",
            "loadedId": "radio",
            "paused": False,
        },
        recording_status={
            "recordings": [{"stationId": "radio", "state": "recording"}]
        },
        playback_position=playback_position,
        recording_position=recording_position,
    )[0]


def visible_text(row: Row) -> tuple[str, str]:
    model = ListModel([row])
    return model.text_for(0, 0), model.text_for(0, 2)


def test_playback_before_and_recording_after_are_independent() -> None:
    row = decorated(playback_position="before", recording_position="after")
    assert visible_text(row) == ("odtwarzane Radio Poznań", "nagrywane")
    assert row.title == "Radio Poznań", "model nie moze dostac ozdobionej nazwy"


def test_both_states_before_form_one_natural_user_facing_label() -> None:
    row = decorated(playback_position="before", recording_position="before")
    assert visible_text(row) == (
        "odtwarzane i nagrywane Radio Poznań",
        "",
    )


def test_speech_can_be_off_while_cue_flags_remain_available() -> None:
    row = decorated(playback_position="off", recording_position="off")
    assert visible_text(row) == ("Radio Poznań", "")
    assert row.playback_activity is True
    assert row.recording_activity is True


def test_only_intentional_labels_are_exposed() -> None:
    assert STATE_POSITION_LABELS == (
        "Nie odczytuj",
        "Przed nazwą stacji",
        "Po nazwie stacji",
    )
    assert normalize_state_position("wartosc-techniczna") == "after"


def test_three_delicate_cues_are_valid_and_distinct() -> None:
    waves = {
        "playback": activity_cue_wav(playback=True, recording=False),
        "recording": activity_cue_wav(playback=False, recording=True),
        "both": activity_cue_wav(playback=True, recording=True),
    }
    assert len(set(waves.values())) == 3
    for data in waves.values():
        assert data.startswith(b"RIFF")
        with wave.open(io.BytesIO(data), "rb") as source:
            assert source.getnchannels() == 1
            assert source.getsampwidth() == 2
            assert source.getnframes() / source.getframerate() <= 0.05
            samples = source.readframes(source.getnframes())
        values = struct.unpack(f"<{len(samples) // 2}h", samples)
        assert max(abs(value) for value in values) <= 2_500
