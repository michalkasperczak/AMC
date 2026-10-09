"""Prezentacja stanu stacji radiowej, bez ingerencji w tor audio.

Ten modul przechowuje wylacznie wybory interfejsu i tworzy bardzo krotkie,
ciche sygnaly WAV. Nie wysyla zadnych polecen do hosta, nagrywania ani
odtwarzania.
"""

from __future__ import annotations

import io
import math
import struct
import wave


STATE_POSITION_VALUES: tuple[str, ...] = ("off", "before", "after")
STATE_POSITION_LABELS: tuple[str, ...] = (
    "Nie odczytuj",
    "Przed nazwą stacji",
    "Po nazwie stacji",
)


def normalize_state_position(raw: object, *, default: str = "after") -> str:
    """Oddaj tylko znana wartosc modelu; obcy zapis nie trafia do interfejsu."""
    return raw if isinstance(raw, str) and raw in STATE_POSITION_VALUES else default


def state_position_index(raw: object) -> int:
    return STATE_POSITION_VALUES.index(normalize_state_position(raw))


def state_position_value(index: int) -> str:
    if 0 <= index < len(STATE_POSITION_VALUES):
        return STATE_POSITION_VALUES[index]
    return "after"


def join_spoken_prefix(labels: list[str]) -> str:
    """Naturalne polaczenie stanow stojacych przed nazwa stacji."""
    clean = [label.strip() for label in labels if label and label.strip()]
    if len(clean) < 2:
        return clean[0] if clean else ""
    return ", ".join(clean[:-1]) + " i " + clean[-1]


def activity_cue_wav(*, playback: bool, recording: bool) -> bytes:
    """Zbuduj delikatny sygnal PCM dla stanu odtwarzania/nagrywania.

    Odtwarzanie ma wyzszy ton, nagrywanie nizszy, a oba stany jednoczesnie
    tworza krotki dwudzwiek. Amplituda to tylko okolo osiem procent zakresu.
    """
    if not playback and not recording:
        return b""

    sample_rate = 22_050
    duration = 0.040 if playback and recording else 0.035
    sample_count = max(1, int(sample_rate * duration))
    frequencies = (
        (1046.5, 659.25)
        if playback and recording
        else ((1046.5,) if playback else (659.25,))
    )
    peak = 2_500
    fade_samples = max(1, int(sample_rate * 0.006))
    samples: list[bytes] = []
    for index in range(sample_count):
        envelope = min(
            1.0,
            index / fade_samples,
            (sample_count - 1 - index) / fade_samples,
        )
        value = sum(
            math.sin(2.0 * math.pi * frequency * index / sample_rate)
            for frequency in frequencies
        ) / len(frequencies)
        samples.append(struct.pack("<h", int(peak * envelope * value)))

    buffer = io.BytesIO()
    with wave.open(buffer, "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(sample_rate)
        output.writeframes(b"".join(samples))
    return buffer.getvalue()
