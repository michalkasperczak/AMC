"""Wyjscie audio per sesja bez ujawniania identyfikatorow w interfejsie.

Pelny AMC zapisuje wybor w ``settings.audio.outputDeviceIdsBySession``.
Wariant wxPython tylko go odczytuje, a wlasna decyzje zapisuje w prywatnym
stanie. Brak klucza prywatnego oznacza dziedziczenie profilu; pusty napis jest
jawna decyzja „urzadzenie domyslne”.
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

from .navigation import SessionId


_SUPPORTED_OUTPUT_SESSIONS = (
    SessionId.FILES,
    SessionId.RADIO,
    SessionId.PODCASTS,
)
_SESSION_KEYS = {session.value for session in _SUPPORTED_OUTPUT_SESSIONS}
_PROFILE_TO_LITE = {"local": SessionId.FILES.value}
_MAXIMUM_DEVICE_ID_LENGTH = 4_096


@dataclass(frozen=True, slots=True)
class AudioOutputChoice:
    """Model wyboru; ``label`` jest jedynym tekstem przekazywanym do wx."""

    device_id: str | None
    label: str
    available: bool = True


def read_output_overrides(raw: object) -> dict[str, str]:
    """Przyjmij tylko znane sesje oraz bezpieczne, tekstowe identyfikatory."""

    if not isinstance(raw, dict):
        return {}
    result: dict[str, str] = {}
    for key, value in raw.items():
        if key not in _SESSION_KEYS or not isinstance(value, str):
            continue
        normalized = value.strip()
        if len(normalized) <= _MAXIMUM_DEVICE_ID_LENGTH:
            # Pusty napis jest wazny: oznacza jawny wybor wyjscia domyslnego.
            result[key] = normalized
    return result


def read_profile_outputs(path: Path | str) -> dict[str, str]:
    """Odczytaj wybory pelnego AMC. Kazdy blad daje bezpieczne domyslne."""

    try:
        raw = json.loads(Path(path).read_text(encoding="utf-8-sig"))
    except (FileNotFoundError, OSError, UnicodeDecodeError, json.JSONDecodeError):
        return {}
    if not isinstance(raw, dict):
        return {}
    settings = raw.get("settings")
    audio = settings.get("audio") if isinstance(settings, dict) else None
    values = (
        audio.get("outputDeviceIdsBySession")
        if isinstance(audio, dict)
        else None
    )
    if not isinstance(values, dict):
        return {}

    mapped: dict[str, str] = {}
    for raw_key, value in values.items():
        key = _PROFILE_TO_LITE.get(raw_key, raw_key)
        if key not in _SESSION_KEYS or not isinstance(value, str):
            continue
        normalized = value.strip()
        if normalized and len(normalized) <= _MAXIMUM_DEVICE_ID_LENGTH:
            mapped[key] = normalized
    return mapped


def effective_output_device_id(
    session: SessionId,
    private_overrides: object,
    profile_outputs: object,
) -> str | None:
    """Prywatny wybor wygrywa; jego brak dziedziczy profil glownego AMC."""

    private = read_output_overrides(private_overrides)
    if session.value in private:
        return private[session.value] or None
    profile = read_output_overrides(profile_outputs)
    return profile.get(session.value) or None


def choices_from_payload(payload: object) -> tuple[AudioOutputChoice, ...]:
    """Oddziel dane protokolu od jawnych etykiet podawanych czytnikowi."""

    if not isinstance(payload, dict) or not isinstance(payload.get("devices"), list):
        return ()
    result: list[AudioOutputChoice] = []
    seen: set[str | None] = set()
    for item in payload["devices"]:
        if not isinstance(item, dict):
            continue
        raw_id = item.get("id")
        device_id = raw_id.strip() if isinstance(raw_id, str) else None
        if device_id is not None and (
            not device_id or len(device_id) > _MAXIMUM_DEVICE_ID_LENGTH
        ):
            continue
        label = item.get("name")
        if not isinstance(label, str) or not label.strip() or device_id in seen:
            continue
        seen.add(device_id)
        result.append(AudioOutputChoice(
            device_id=device_id,
            label=label.strip(),
            available=item.get("available") is not False,
        ))
    return tuple(result)
