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
from typing import TYPE_CHECKING, Iterable
from urllib.parse import urlparse

from .list_model import Row
from .navigation import SessionId

if TYPE_CHECKING:  # pragma: no cover - tylko typ, bez cyklu importow w runtime
    from .wiim_source import WiiMPresetChoice


PRESET_COUNT = 12
SESSION_PROFILE_KEYS = {
    SessionId.FILES: "local",
    SessionId.RADIO: "radio",
    SessionId.RADIOARCHIWISTA: "radioarchiwista",
    SessionId.PODCASTS: "podcasts",
    SessionId.TIDAL: "tidal",
    SessionId.WIIM: "wiim",
    SessionId.SONOS: "sonos",
    SessionId.SPOTIFY: "spotify",
}
WIIM_DEVICE_PRESET_PREFIX = "wiim-device:"
WIIM_NATIVE_PRESET_PREFIX = "native-preset:"


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
    sonos_household_id: str | None = None
    sonos_fixed_player_ids: tuple[str, ...] = ()

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
        if self.sonos_household_id:
            payload["sonosHouseholdId"] = self.sonos_household_id
        if self.sonos_fixed_player_ids:
            payload["sonosFixedPlayerIds"] = list(self.sonos_fixed_player_ids)
        return payload


RADIOARCHIWISTA_DEFAULT_ENTRIES = (
    PresetEntry(1, "rarch:latest", "rarchLatest", "Najnowsze nagrania", None),
    PresetEntry(2, "rarch:station:pr1", "rarchStation", "Program 1", "pr1"),
)


@dataclass(frozen=True, slots=True)
class PresetTarget:
    """Element, ktory wxPython potrafi zapisac i pozniej naprawde uruchomic."""

    target_id: str
    target_kind: str
    title: str
    location: str | None
    sonos_household_id: str | None = None
    sonos_fixed_player_ids: tuple[str, ...] = ()

    def as_entry(self, slot: int) -> PresetEntry:
        return PresetEntry(
            slot=slot,
            target_id=self.target_id,
            target_kind=self.target_kind,
            title=self.title,
            location=self.location,
            sonos_household_id=self.sonos_household_id,
            sonos_fixed_player_ids=self.sonos_fixed_player_ids,
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
    target_kind: str | None
    sonos_household_id: str | None

    @property
    def label(self) -> str:
        title = self.target_title if self.target_id is not None else "pusty"
        return f"{self.slot_label}, {title}, Ctrl+Shift+{self.spoken_shortcut_label}"


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
        household = item.get("sonosHouseholdId")
        raw_players = item.get("sonosFixedPlayerIds")
        if not isinstance(target_id, str) or not target_id.strip():
            continue
        if not isinstance(target_kind, str) or not target_kind.strip():
            continue
        used.add(slot)
        fixed_players = tuple(sorted({
            player.strip()
            for player in raw_players
            if isinstance(player, str) and player.strip()
        })) if isinstance(raw_players, list) else ()
        result.append(PresetEntry(
            slot=slot,
            target_id=target_id.strip(),
            target_kind=target_kind.strip(),
            title=title.strip() if isinstance(title, str) else "",
            location=location.strip() if isinstance(location, str) and location.strip() else None,
            sonos_household_id=(
                household.strip()
                if isinstance(household, str) and household.strip()
                else None
            ),
            sonos_fixed_player_ids=fixed_players,
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
        dynamic_wiim = (
            isinstance(key, str)
            and key.startswith(WIIM_DEVICE_PRESET_PREFIX)
            and bool(key.removeprefix(WIIM_DEVICE_PRESET_PREFIX).strip())
        )
        if (key not in known and not dynamic_wiim) or not isinstance(value, list):
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
        if entry.slot != slot and _entry_identity(entry) != _target_identity(target)
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
            target_kind=entry.target_kind if entry else None,
            sonos_household_id=entry.sonos_household_id if entry else None,
        ))
    return tuple(result)


def first_free_slot(entries: tuple[PresetEntry, ...]) -> int | None:
    used = {entry.slot for entry in entries}
    return next((slot for slot in range(1, PRESET_COUNT + 1) if slot not in used), None)


def existing_target_slot(
    entries: tuple[PresetEntry, ...], target: PresetTarget
) -> int | None:
    identity = _target_identity(target)
    return next(
        (entry.slot for entry in entries if _entry_identity(entry) == identity),
        None,
    )


def adjacent_entry(
    entries: tuple[PresetEntry, ...],
    *,
    direction: int,
    current_target: PresetTarget | None = None,
    current_slot: int | None = None,
) -> PresetEntry | None:
    """Wybierz poprzedni albo następny zajęty preset z zawijaniem.

    Numer ostatnio uruchomionego presetu ma pierwszeństwo. Po świeżym starcie
    można jednak rozpoznać bieżący materiał po modelu celu. Gdy nie znamy
    żadnego z nich, następny zaczyna od pierwszego zajętego miejsca, a
    poprzedni od ostatniego. Puste miejsca nie są fikcyjnymi przystankami.
    """

    ordered = tuple(sorted(entries, key=lambda entry: entry.slot))
    if not ordered:
        return None
    step = 1 if direction >= 0 else -1
    slot = current_slot if type(current_slot) is int else None
    if slot is None and current_target is not None:
        slot = existing_target_slot(ordered, current_target)
    index = next(
        (position for position, entry in enumerate(ordered) if entry.slot == slot),
        None,
    )
    if index is None:
        return ordered[0] if step > 0 else ordered[-1]
    return ordered[(index + step) % len(ordered)]


def _sonos_material_identity(
    target_kind: str, target_id: str, household_id: str | None
) -> tuple[str, str, str]:
    kind = target_kind.casefold()
    household = (
        household_id or ""
        if kind in {"sonos-favorite", "sonos-playlist"}
        else ""
    )
    return kind, household, target_id


def _entry_identity(entry: PresetEntry) -> tuple[str, str, str]:
    kind = entry.target_kind.casefold()
    if kind.startswith("sonos-"):
        return _sonos_material_identity(
            kind, entry.target_id, entry.sonos_household_id
        )
    return "", "", entry.target_id


def _target_identity(target: PresetTarget) -> tuple[str, str, str]:
    kind = target.target_kind.casefold()
    if kind.startswith("sonos-"):
        return _sonos_material_identity(
            kind, target.target_id, target.sonos_household_id
        )
    return "", "", target.target_id


def choice_matches_target(choice: PresetChoice, target: PresetTarget) -> bool:
    """Porównaj model, nie sam napis Id; etykieta kontrolki pozostaje czysta."""
    if choice.target_id is None or choice.target_kind is None:
        return False
    synthetic = PresetEntry(
        choice.slot,
        choice.target_id,
        choice.target_kind,
        choice.target_title or "",
        choice.location,
        sonos_household_id=choice.sonos_household_id,
    )
    return _entry_identity(synthetic) == _target_identity(target)


def target_from_row(session: SessionId, row: Row | None) -> PresetTarget | None:
    """Zamien widoczny element na cel tylko wtedy, gdy umiemy go uruchomic."""
    if row is None or row.kind == "parent" or not row.title.strip():
        return None
    if session is SessionId.RADIOARCHIWISTA:
        if row.service_kind == "rarchLatest":
            return PresetTarget("rarch:latest", "rarchLatest", "Najnowsze nagrania", None)
        if row.service_kind == "rarchStation" and row.service_id:
            from .radioarchiwista_source import check_slug, station_item_id
            try:
                slug = check_slug(row.service_id)
            except RuntimeError:
                return None
            title = row.title.rsplit(", ", 1)[0] if ", " in row.title else row.title
            return PresetTarget(station_item_id(slug), "rarchStation", title, slug)
        return None
    if session is SessionId.RADIO:
        if row.kind != "station" or not row.url:
            return None
        return PresetTarget(row.item_id, "station", row.title, row.url)
    if session is SessionId.SONOS and row.service_id:
        kind = {
            "favorite": "sonos-favorite",
            "playlist": "sonos-playlist",
            "ownStream": "sonos-own-stream",
        }.get(row.service_kind or "")
        if kind is None:
            return None
        household = row.parent_id if kind != "sonos-own-stream" else None
        if kind != "sonos-own-stream" and not household:
            return None
        return PresetTarget(
            row.service_id,
            kind,
            row.title,
            None,
            sonos_household_id=household,
        )
    if (
        session is SessionId.SPOTIFY
        and row.service_id
        and row.kind in {
            "track", "episode", "album", "artist", "playlist", "podcast"
        }
    ):
        # Zapisujemy katalogową tożsamość osobno od etykiety. Prefiks rodzaju
        # odróżnia te cele od lokalnych utworów i playlist AMC, a NVDA widzi
        # wyłącznie ``title`` przez PresetChoice.label.
        return PresetTarget(
            row.item_id,
            f"spotify-{row.kind}",
            row.title,
            row.service_id,
        )
    if row.kind == "playlist":
        playlist_id = row.item_id.split(":", 1)[1] if ":" in row.item_id else row.item_id
        return PresetTarget(row.item_id, "amcPlaylist", row.title, playlist_id)
    if row.kind == "folder" and row.path:
        return PresetTarget(row.item_id, "folder", row.title, row.path)
    if row.kind == "track" and row.path:
        return PresetTarget(row.item_id, "track", row.title, row.path)
    return None


def spotify_entry_target(entry: PresetEntry) -> tuple[str, str] | None:
    """Return a safe Spotify kind and catalogue ID from new or legacy presets.

    Full AMC stored the shareable Spotify URI/URL in ``targetLocation`` while
    the Python interface stores the already-normalized catalogue identity.
    Neither representation is ever used as a spoken label.
    """

    kind = entry.target_kind.casefold().removeprefix("spotify-")
    service_type = {
        "track": "track",
        "episode": "episode",
        "album": "album",
        "artist": "artist",
        "playlist": "playlist",
        "podcast": "show",
    }.get(kind)
    if service_type is None:
        return None

    for raw in (entry.location, entry.target_id):
        candidate = raw.strip() if isinstance(raw, str) else ""
        if not candidate:
            continue
        if candidate.casefold().startswith("spotify:"):
            parts = candidate.split(":")
            if (
                len(parts) == 3
                and parts[1].casefold() == service_type
            ):
                candidate = parts[2]
            else:
                continue
        elif candidate.casefold().startswith(("https://", "http://")):
            parsed = urlparse(candidate)
            parts = [part for part in parsed.path.split("/") if part]
            if (
                parsed.scheme.casefold() != "https"
                or parsed.netloc.casefold() != "open.spotify.com"
                or len(parts) < 2
                or parts[-2].casefold() != service_type
            ):
                continue
            candidate = parts[-1]
        elif candidate.casefold().startswith("spotify-"):
            # Syntetyczne Id wiersza nie jest tożsamością katalogową.
            continue
        if 0 < len(candidate) <= 128 and candidate.isalnum():
            return kind, candidate
    return None


def wiim_device_preset_key(device_id: str) -> str:
    """Prywatny klucz mapowania jednego urzadzenia; nigdy tekst kontrolki."""
    normalized = device_id.strip() if isinstance(device_id, str) else ""
    if not normalized:
        raise ValueError("Brak identyfikatora urządzenia WiiM")
    return WIIM_DEVICE_PRESET_PREFIX + normalized


def wiim_native_preset_number(entry: PresetEntry | None) -> int | None:
    if entry is None or entry.target_kind.casefold() != "wiimnativepreset":
        return None
    if not entry.target_id.casefold().startswith(WIIM_NATIVE_PRESET_PREFIX):
        return None
    try:
        number = int(entry.target_id[len(WIIM_NATIVE_PRESET_PREFIX):])
    except ValueError:
        return None
    return number if 1 <= number <= PRESET_COUNT else None


def wiim_native_target(choice: "WiiMPresetChoice") -> PresetTarget:
    """Zamien jawny wybor urzadzenia na prywatny cel skrotu AMC."""
    if not choice.occupied or not 1 <= choice.number <= PRESET_COUNT:
        raise ValueError("Można przypisać wyłącznie zajęty preset WiiM")
    return PresetTarget(
        target_id=f"{WIIM_NATIVE_PRESET_PREFIX}{choice.number}",
        target_kind="wiimNativePreset",
        title=choice.title or f"Preset {choice.number}",
        location=None,
    )


def effective_wiim_entries(
    path: Path | str,
    device_id: str,
    native_choices: Iterable["WiiMPresetChoice"],
    overrides: object,
) -> tuple[PresetEntry, ...]:
    """Mapowanie Ctrl+Shift osobno dla urzadzenia, zgodne z glownym AMC.

    Prywatna zmiana wxPython wygrywa. Potem czytamy istniejacy klucz
    ``wiim-device:<id>`` pelnego AMC. Gdy nigdy nie powstal, zajete natywne
    presety dostaja domyslnie odpowiadajace im miejsca 1-12.
    """
    choices_snapshot = tuple(native_choices)
    key = wiim_device_preset_key(device_id)
    cleaned = read_preset_overrides(overrides)
    if key in cleaned:
        source = _entry_list(cleaned[key])
    else:
        try:
            raw = _read_profile(path)
        except PresetProfileError:
            raw = {}
        session_presets = raw.get("sessionPresets")
        by_session = (
            session_presets.get("entriesBySession")
            if isinstance(session_presets, dict)
            else None
        )
        if isinstance(by_session, dict) and key in by_session:
            source = _entry_list(by_session.get(key))
        else:
            source = tuple(
                wiim_native_target(choice).as_entry(choice.number)
                for choice in choices_snapshot
                if getattr(choice, "occupied", False)
                and type(getattr(choice, "number", None)) is int
                and 1 <= choice.number <= PRESET_COUNT
            )

    current = {
        choice.number: choice
        for choice in choices_snapshot
        if type(getattr(choice, "number", None)) is int
    }
    result: list[PresetEntry] = []
    for entry in source:
        number = wiim_native_preset_number(entry)
        if number is None:
            continue
        choice = current.get(number)
        result.append(PresetEntry(
            slot=entry.slot,
            target_id=entry.target_id,
            target_kind=entry.target_kind,
            title=(
                choice.title
                if choice is not None and getattr(choice, "occupied", False)
                else entry.title
            ),
            location=None,
        ))
    return tuple(result)


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
    if session is SessionId.RADIOARCHIWISTA:
        return RADIOARCHIWISTA_DEFAULT_ENTRIES
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
    if session is SessionId.RADIOARCHIWISTA:
        entries = (
            _entry_list(cleaned_overrides[session.value])
            if has_override else RADIOARCHIWISTA_DEFAULT_ENTRIES
        )
        return ResolvedPreset(next((item for item in entries if item.slot == slot), None))
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
