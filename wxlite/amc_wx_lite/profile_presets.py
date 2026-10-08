"""Presety AMC czytane z istniejacego profilu, bez drugiego zapisu.

Pelne AMC przechowuje 12 miejsc osobno dla kazdej sesji w
``sessionPresets.entriesBySession``. Wariant wxPython jest na razie czytnikiem
tego profilu: uruchamia istniejace przypisania, ale ich nie przepisuje ani nie
tworzy konkurencyjnego ``state.json``.
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

from .navigation import SessionId


PRESET_COUNT = 12
SESSION_PROFILE_KEYS = {
    SessionId.FILES: "local",
    SessionId.RADIO: "radio",
}


def shortcut_label(slot: int, *, spoken: bool = False) -> str:
    """Etykieta zgodna z ``RadioPresetSlots`` w pelnym AMC."""
    if 1 <= slot <= 9:
        return str(slot)
    if slot == 10:
        return "0"
    if slot == 11:
        return "minus" if spoken else "-"
    if slot == 12:
        return "znak równości" if spoken else "="
    raise ValueError("Numer presetu musi należeć do zakresu od 1 do 12")


@dataclass(frozen=True, slots=True)
class PresetEntry:
    slot: int
    target_id: str
    target_kind: str
    title: str
    location: str | None


@dataclass(frozen=True, slots=True)
class RadioPresetTarget:
    slot: int
    item_id: str
    title: str
    url: str


@dataclass(frozen=True, slots=True)
class ResolvedPreset:
    entry: PresetEntry | None
    radio_target: RadioPresetTarget | None = None
    radio_sequence: tuple[RadioPresetTarget, ...] = ()


class PresetProfileError(RuntimeError):
    """Czytelna odmowa odczytu profilu, bez technicznego zrzutu do NVDA."""


def _entries(raw: dict, session: SessionId) -> tuple[PresetEntry, ...]:
    session_presets = raw.get("sessionPresets")
    if not isinstance(session_presets, dict):
        return ()
    by_session = session_presets.get("entriesBySession")
    if not isinstance(by_session, dict):
        return ()
    source = by_session.get(SESSION_PROFILE_KEYS[session])
    if not isinstance(source, list):
        return ()

    result: list[PresetEntry] = []
    used: set[int] = set()
    for item in source:
        if not isinstance(item, dict):
            continue
        slot = item.get("slot")
        if type(slot) is not int or not 1 <= slot <= PRESET_COUNT or slot in used:
            continue
        target_id = item.get("targetId")
        target_kind = item.get("targetKind")
        title = item.get("targetTitle")
        location = item.get("targetLocation")
        if not isinstance(target_id, str) or not target_id.strip():
            continue
        if not isinstance(target_kind, str) or not target_kind.strip():
            continue
        used.add(slot)
        result.append(PresetEntry(
            slot=slot,
            target_id=target_id.strip(),
            target_kind=target_kind.strip(),
            title=title.strip() if isinstance(title, str) else "",
            location=location.strip() if isinstance(location, str) and location.strip() else None,
        ))
    return tuple(sorted(result, key=lambda entry: entry.slot))


def _radio_targets(raw: dict, entries: tuple[PresetEntry, ...]) -> tuple[RadioPresetTarget, ...]:
    radio = raw.get("radio")
    stations = radio.get("stations") if isinstance(radio, dict) else None
    if not isinstance(stations, list):
        return ()
    by_id = {
        station.get("id"): station
        for station in stations
        if isinstance(station, dict) and isinstance(station.get("id"), str)
    }
    result: list[RadioPresetTarget] = []
    for entry in entries:
        if entry.target_kind.casefold() != "station":
            continue
        station = by_id.get(entry.target_id)
        if not isinstance(station, dict):
            continue
        url = station.get("streamUrl") or station.get("backupStreamUrl")
        if not isinstance(url, str) or not url.strip():
            continue
        current_title = station.get("name")
        title = (
            current_title.strip()
            if isinstance(current_title, str) and current_title.strip()
            else entry.title or url.strip()
        )
        result.append(RadioPresetTarget(entry.slot, entry.target_id, title, url.strip()))
    return tuple(result)


def resolve_preset(path: Path | str, session: SessionId, slot: int) -> ResolvedPreset:
    """Odczytaj jedno przypisanie i aktualne dane celu.

    Dla Radia zwracamy tez wszystkie dzialajace presety w kolejnosci miejsc.
    To jest kontekst Page Up/Page Down po uruchomieniu presetu, zgodny z WPF.
    """
    try:
        raw = json.loads(Path(path).read_text(encoding="utf-8-sig"))
    except FileNotFoundError as error:
        raise PresetProfileError("Nie znalazłem profilu AMC z presetami") from error
    except (json.JSONDecodeError, UnicodeDecodeError) as error:
        raise PresetProfileError("Profil AMC z presetami jest uszkodzony") from error
    except OSError as error:
        raise PresetProfileError("Nie mogę teraz odczytać presetów AMC") from error
    if not isinstance(raw, dict):
        raise PresetProfileError("Profil AMC ma nieoczekiwaną zawartość")

    entries = _entries(raw, session)
    entry = next((candidate for candidate in entries if candidate.slot == slot), None)
    if session is not SessionId.RADIO:
        return ResolvedPreset(entry=entry)

    targets = _radio_targets(raw, entries)
    target = next((candidate for candidate in targets if candidate.slot == slot), None)
    # Ten sam cel moze byc omylkowo przypisany do dwoch miejsc. Kontekst
    # Page Up/Page Down ma zawierac material raz, jak ``DistinctBy`` w WPF.
    seen: set[str] = set()
    sequence: list[RadioPresetTarget] = []
    for candidate in targets:
        if candidate.item_id in seen:
            continue
        seen.add(candidate.item_id)
        sequence.append(candidate)
    return ResolvedPreset(
        entry=entry, radio_target=target, radio_sequence=tuple(sequence)
    )
