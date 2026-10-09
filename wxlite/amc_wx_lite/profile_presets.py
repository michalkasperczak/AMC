"""Presety sesji: odczyt profilu AMC i bezpieczne nadpisania wxPython.

Pelne AMC przechowuje 12 miejsc osobno dla kazdej sesji w
``sessionPresets.entriesBySession``. Dopoki uzytkownik nie zmieni presetow w
wxPython, czytamy te wpisy wprost. Pierwsza zmiana kopiuje biezacy zestaw danej
sesji do PRYWATNEGO magazynu ``AMC-wx-Lite``. Nie zapisujemy wspolnego
``state.json`` i nie mozemy przez to scigac sie z dzialajacym starym AMC.

W tym module model i etykiety sa rozdzielone. Kontrolka dostaje wylacznie
``PresetChoice.label``; identyfikator celu, rodzaj i sciezka nie trafiaja do
nazwy dostepnosciowej przez domyslne ``repr`` obiektu.
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

from .list_model import Row
from .navigation import SessionId


PRESET_COUNT = 12
SESSION_PROFILE_KEYS = {
    SessionId.FILES: "local",
    SessionId.RADIO: "radio",
    SessionId.PODCASTS: "podcasts",
    SessionId.TIDAL: "tidal",
    SessionId.WIIM: "wiim",
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

    def to_payload(self) -> dict:
        """Zwykle dane JSON, bez tekstu kontrolki i bez reprezentacji obiektu."""
        payload = {
            "slot": self.slot,
            "targetId": self.target_id,
            "targetKind": self.target_kind,
            "targetTitle": self.title,
        }
        if self.location:
            payload["targetLocation"] = self.location
        return payload


@dataclass(frozen=True, slots=True)
class PresetTarget:
    """Element, ktory wxPython potrafi zapisac i pozniej naprawde uruchomic."""

    target_id: str
    target_kind: str
    title: str
    location: str | None

    def as_entry(self, slot: int) -> PresetEntry:
        return PresetEntry(
            slot=slot,
            target_id=self.target_id,
            target_kind=self.target_kind,
            title=self.title,
            location=self.location,
        )


@dataclass(frozen=True, slots=True)
class PresetChoice:
    """Jedno z 12 miejsc oraz jego JAWNA, uzytkowa etykieta."""

    slot: int
    slot_label: str
    spoken_shortcut_label: str
    target_id: str | None
    target_title: str | None
    location: str | None

    @property
    def label(self) -> str:
        position = (
            f"Preset numer {self.slot_label}"
            if self.slot <= 9
            else f"Preset numer {self.slot_label}, klawisz {self.spoken_shortcut_label}"
        )
        shortcut = f"skrót Ctrl+Shift+{self.spoken_shortcut_label}"
        return (
            f"{position}, {shortcut} — {self.target_title}"
            if self.target_id is not None
            else f"{position}, {shortcut} — pusty"
        )


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


def _entry_list(source: object) -> tuple[PresetEntry, ...]:
    """Oczysc liste wpisow. Pierwszy poprawny wpis danego miejsca wygrywa."""
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


def _entries(raw: dict, session: SessionId) -> tuple[PresetEntry, ...]:
    session_presets = raw.get("sessionPresets")
    if not isinstance(session_presets, dict):
        return ()
    by_session = session_presets.get("entriesBySession")
    if not isinstance(by_session, dict):
        return ()
    return _entry_list(by_session.get(SESSION_PROFILE_KEYS[session]))


def read_preset_overrides(raw: object) -> dict[str, list[dict]]:
    """Wczytaj prywatne nadpisania, zachowujac znaczenie pustej listy.

    Brak klucza sesji znaczy: nadal odbijaj profil pelnego AMC. Obecny klucz
    z pusta lista znaczy: uzytkownik swiadomie usunal wszystkie presety tej
    sesji. Tych stanow nie wolno zlewac.
    """
    if not isinstance(raw, dict):
        return {}
    known = {session.value for session in SESSION_PROFILE_KEYS}
    result: dict[str, list[dict]] = {}
    for key, value in raw.items():
        if key not in known or not isinstance(value, list):
            continue
        result[key] = [entry.to_payload() for entry in _entry_list(value)]
    return result


def entries_payload(entries: object) -> list[dict]:
    """Kanoniczna lista do prywatnego JSON-a."""
    if isinstance(entries, tuple) and all(isinstance(item, PresetEntry) for item in entries):
        cleaned = entries
    elif isinstance(entries, list) and all(isinstance(item, PresetEntry) for item in entries):
        cleaned = tuple(entries)
    else:
        cleaned = _entry_list(entries)
    return [entry.to_payload() for entry in sorted(cleaned, key=lambda item: item.slot)]


def replace_entry(
    entries: tuple[PresetEntry, ...], target: PresetTarget, slot: int
) -> tuple[PresetEntry, ...]:
    """Zapisz cel w miejscu. Ten sam cel moze miec tylko jedno miejsce."""
    if not 1 <= slot <= PRESET_COUNT:
        raise ValueError("Numer presetu musi należeć do zakresu od 1 do 12")
    kept = [
        entry for entry in entries
        if entry.slot != slot and entry.target_id != target.target_id
    ]
    kept.append(target.as_entry(slot))
    return tuple(sorted(kept, key=lambda entry: entry.slot))


def remove_entry(
    entries: tuple[PresetEntry, ...], slot: int
) -> tuple[PresetEntry, ...]:
    return tuple(entry for entry in entries if entry.slot != slot)


def choices(entries: tuple[PresetEntry, ...]) -> tuple[PresetChoice, ...]:
    by_slot = {entry.slot: entry for entry in entries}
    result: list[PresetChoice] = []
    for slot in range(1, PRESET_COUNT + 1):
        entry = by_slot.get(slot)
        result.append(PresetChoice(
            slot=slot,
            slot_label=shortcut_label(slot),
            spoken_shortcut_label=shortcut_label(slot, spoken=True),
            target_id=entry.target_id if entry else None,
            target_title=(entry.title or "Element bez nazwy") if entry else None,
            location=entry.location if entry else None,
        ))
    return tuple(result)


def first_free_slot(entries: tuple[PresetEntry, ...]) -> int | None:
    used = {entry.slot for entry in entries}
    return next((slot for slot in range(1, PRESET_COUNT + 1) if slot not in used), None)


def existing_target_slot(entries: tuple[PresetEntry, ...], target_id: str) -> int | None:
    return next((entry.slot for entry in entries if entry.target_id == target_id), None)


def target_from_row(session: SessionId, row: Row | None) -> PresetTarget | None:
    """Zamien widoczny element na cel tylko wtedy, gdy umiemy go uruchomic."""
    if row is None or row.kind == "parent" or not row.title.strip():
        return None
    if session is SessionId.RADIO:
        if row.kind != "station" or not row.url:
            return None
        return PresetTarget(row.item_id, "station", row.title, row.url)
    if row.kind == "playlist":
        playlist_id = row.item_id.split(":", 1)[1] if ":" in row.item_id else row.item_id
        return PresetTarget(row.item_id, "amcPlaylist", row.title, playlist_id)
    if row.kind == "folder" and row.path:
        return PresetTarget(row.item_id, "folder", row.title, row.path)
    if row.kind == "track" and row.path:
        return PresetTarget(row.item_id, "track", row.title, row.path)
    return None


def _radio_targets(
    raw: dict,
    entries: tuple[PresetEntry, ...],
    current_stations: object = None,
) -> tuple[RadioPresetTarget, ...]:
    radio = raw.get("radio")
    stations = radio.get("stations") if isinstance(radio, dict) else None
    by_id = {}
    if isinstance(stations, list):
        by_id.update({
            station.get("id"): station
            for station in stations
            if isinstance(station, dict) and isinstance(station.get("id"), str)
        })
    # Biezaca lista wygrywa z migawka profilu: zawiera prywatne stacje oraz
    # aktualny zakres Biblioteka/Ulubione. ``url`` to pole prywatnego modelu.
    if isinstance(current_stations, list):
        by_id.update({
            station.get("id"): station
            for station in current_stations
            if isinstance(station, dict) and isinstance(station.get("id"), str)
        })
    result: list[RadioPresetTarget] = []
    for entry in entries:
        if entry.target_kind.casefold() != "station":
            continue
        station = by_id.get(entry.target_id)
        if not isinstance(station, dict):
            continue
        url = station.get("url") or station.get("streamUrl") or station.get("backupStreamUrl")
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


def _read_profile(path: Path | str) -> dict:
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
    return raw


def effective_entries(
    path: Path | str,
    session: SessionId,
    overrides: object,
) -> tuple[PresetEntry, ...]:
    """Prywatny klucz sesji wygrywa; jego brak odbija profil AMC."""
    cleaned = read_preset_overrides(overrides)
    if session.value in cleaned:
        return _entry_list(cleaned[session.value])
    return _entries(_read_profile(path), session)


def resolve_preset(
    path: Path | str,
    session: SessionId,
    slot: int,
    *,
    overrides: object = None,
    current_stations: object = None,
) -> ResolvedPreset:
    """Odczytaj jedno przypisanie i aktualne dane celu.

    Dla Radia zwracamy tez wszystkie dzialajace presety w kolejnosci miejsc.
    To jest kontekst Page Up/Page Down po uruchomieniu presetu, zgodny z WPF.
    """
    cleaned_overrides = read_preset_overrides(overrides)
    has_override = session.value in cleaned_overrides
    # Dla Radia profil nadal jest uzytecznym slownikiem stacji. Gdy prywatna
    # lista wystarcza, brak starego profilu nie moze jednak unieruchomic
    # swiadomie zapisanych presetow wxPython.
    try:
        raw = _read_profile(path)
    except PresetProfileError:
        if not has_override or session is SessionId.RADIO and not isinstance(current_stations, list):
            raise
        raw = {}
    entries = (
        _entry_list(cleaned_overrides[session.value])
        if has_override
        else _entries(raw, session)
    )
    entry = next((candidate for candidate in entries if candidate.slot == slot), None)
    if session is not SessionId.RADIO:
        return ResolvedPreset(entry=entry)

    targets = _radio_targets(raw, entries, current_stations)
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
