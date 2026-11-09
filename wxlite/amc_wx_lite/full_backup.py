"""Przenośne, wersjonowane kopie danych bieżącego profilu AMC.

Rdzeń C# jest jedynym właścicielem ``state.json`` i baz SQLite. Ten moduł
dodaje prywatny stan interfejsu wxPython, filtruje go tym samym manifestem i
publikuje gotowy plik atomowo. Brak działu w manifeście zawsze znaczy
„zachowaj dane odbiorcy”, nigdy „wyczyść je”.
"""

from __future__ import annotations

import copy
import hashlib
import hmac
import json
import os
import tempfile
import uuid
from dataclasses import dataclass, fields
from datetime import date
from pathlib import Path

from .state_store import LiteState, Options, StateStore


MAXIMUM_BACKUP_BYTES = 512 * 1024 * 1024
PYTHON_SECTION = "amcPython"
PYTHON_FORMAT_VERSION = 2

CONFIGURATION = "configuration"
LIBRARY = "library"
PLAYLISTS = "playlists"
RADIO = "radio"
RADIO_SCHEDULES = "radioSchedules"
PODCASTS = "podcasts"
SERVICES = "services"
ALL_SECTIONS = (
    CONFIGURATION,
    LIBRARY,
    PLAYLISTS,
    RADIO,
    RADIO_SCHEDULES,
    PODCASTS,
    SERVICES,
)

SECTION_LABELS = {
    CONFIGURATION: "ustawienia programu, interfejsu i profile klawiatury",
    LIBRARY: "Biblioteka, Ulubione, kolejki, historia i zakładki",
    PLAYLISTS: "lokalne playlisty AMC",
    RADIO: "Radio: stacje, presety, rozpoznane utwory i historia nagrywania",
    RADIO_SCHEDULES: "harmonogramy nagrywania Radia",
    PODCASTS: "Podcasty i YouTube",
    SERVICES: "WiiM, Sonos, Frontier Smart, TIDAL i Spotify",
}


@dataclass(frozen=True, slots=True)
class BackupScope:
    key: str
    label: str
    sections: tuple[str, ...]
    extension: str
    file_stem: str

    def default_file_name(self, today: date | None = None) -> str:
        value = today or date.today()
        return f"AMC-{self.file_stem}-{value:%Y-%m-%d}{self.extension}"

    @property
    def wildcard(self) -> str:
        return f"{self.label} (*{self.extension})|*{self.extension}"


BACKUP_SCOPES = (
    BackupScope(
        "all",
        "Wszystkie dane — pełna kopia profilu",
        ALL_SECTIONS,
        ".amcbackup.json",
        "pelna-kopia",
    ),
    BackupScope(
        CONFIGURATION,
        "Ustawienia programu, interfejsu i profile klawiatury",
        (CONFIGURATION,),
        ".amcsettings.json",
        "ustawienia",
    ),
    BackupScope(
        LIBRARY,
        "Biblioteka, Ulubione, kolejki, historia i zakładki",
        (LIBRARY,),
        ".amclibrary.json",
        "biblioteka",
    ),
    BackupScope(
        PLAYLISTS,
        "Lokalne playlisty AMC",
        (PLAYLISTS,),
        ".amcplaylists.json",
        "playlisty",
    ),
    BackupScope(
        RADIO,
        "Radio bez harmonogramów",
        (RADIO,),
        ".amcradio.json",
        "radio",
    ),
    BackupScope(
        RADIO_SCHEDULES,
        "Harmonogramy nagrywania Radia",
        (RADIO_SCHEDULES,),
        ".amcschedules.json",
        "harmonogramy-radia",
    ),
    BackupScope(
        PODCASTS,
        "Podcasty i YouTube",
        (PODCASTS,),
        ".amcpodcasts.json",
        "podcasty-youtube",
    ),
    BackupScope(
        SERVICES,
        "WiiM, Sonos, Frontier Smart, TIDAL i Spotify",
        (SERVICES,),
        ".amcservices.json",
        "urzadzenia-i-uslugi",
    ),
)

IMPORT_WILDCARD = (
    "Kopie danych AMC (*.amcbackup.json;*.amcsettings.json;*.amclibrary.json;"
    "*.amcplaylists.json;*.amcradio.json;*.amcschedules.json;*.amcpodcasts.json;"
    "*.amcservices.json)|*.amcbackup.json;*.amcsettings.json;*.amclibrary.json;"
    "*.amcplaylists.json;*.amcradio.json;*.amcschedules.json;*.amcpodcasts.json;"
    "*.amcservices.json|Pliki JSON (*.json)|*.json"
)


class FullBackupError(ValueError):
    """Plik nie jest bezpieczną, zgodną kopią danych AMC."""


@dataclass(frozen=True, slots=True)
class FullBackupImportPlan:
    ui_payload: dict | None
    has_ui_state: bool
    source_profile: str | None
    included_sections: tuple[str, ...]


def normalize_sections(sections: object) -> tuple[str, ...]:
    if not isinstance(sections, (list, tuple, set, frozenset)):
        raise FullBackupError("Kopia nie podaje zakresu danych.")
    requested: set[str] = set()
    for item in sections:
        if not isinstance(item, str) or not item.strip():
            raise FullBackupError("Kopia ma nieprawidłowy zakres danych.")
        matched = next(
            (
                known
                for known in ALL_SECTIONS
                if known.casefold() == item.strip().casefold()
            ),
            None,
        )
        if matched is None:
            raise FullBackupError("Kopia zawiera nieznany zakres danych.")
        requested.add(matched)
    if not requested:
        raise FullBackupError("Kopia nie zawiera żadnego działu danych.")
    return tuple(section for section in ALL_SECTIONS if section in requested)


def describe_sections(sections: object) -> str:
    normalized = normalize_sections(sections)
    if normalized == ALL_SECTIONS:
        return "wszystkie dane profilu"
    labels = [SECTION_LABELS[section] for section in normalized]
    if len(labels) == 1:
        return labels[0]
    return ", ".join(labels[:-1]) + " oraz " + labels[-1]


def staging_path(destination: Path | str) -> Path:
    """Zwróć nieistniejącą ścieżkę roboczą obok pliku docelowego."""

    target = Path(destination)
    return target.with_name(f".{target.name}.{uuid.uuid4().hex}.stage.json")


def rollback_path(directory: Path | str) -> Path:
    """Ścieżka pełnej kopii ratunkowej na czas jednego importu."""

    return Path(directory) / (
        f".amc-import-rollback-{uuid.uuid4().hex}.amcbackup.json"
    )


def complete_export(
    staged_core_backup: Path | str,
    destination: Path | str,
    *,
    profile_id: str,
    ui_state_path: Path | str,
    sections: object = ALL_SECTIONS,
) -> None:
    """Dołącz właściwy zakres wxPython i atomowo opublikuj gotowy plik."""

    expected_sections = normalize_sections(sections)
    staged = Path(staged_core_backup)
    target = Path(destination)
    envelope = _read_envelope(staged)
    core_sections = _validate_core_envelope(envelope)
    if core_sections != expected_sections:
        raise FullBackupError("Rdzeń AMC zwrócił inny zakres danych niż wybrany.")

    try:
        ui_payload = json.loads(Path(ui_state_path).read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise FullBackupError(
            "Nie można odczytać ustawień interfejsu do kopii danych."
        ) from error
    _validate_ui_payload_shape(ui_payload)
    filtered = _filter_ui_payload(ui_payload, expected_sections)
    fingerprint = _python_fingerprint(expected_sections, filtered)
    envelope[PYTHON_SECTION] = {
        "formatVersion": PYTHON_FORMAT_VERSION,
        "releaseProfile": profile_id,
        "sections": list(expected_sections),
        "contentSha256": fingerprint,
        "state": filtered,
    }
    _write_json_atomically(target, envelope)


def inspect_import(
    path: Path | str,
    *,
    expected_profile_id: str,
    state_store: StateStore,
) -> FullBackupImportPlan:
    """Sprawdź całą kopię przed jakąkolwiek zmianą żywego profilu."""

    envelope = _read_envelope(Path(path))
    core_sections = _validate_core_envelope(envelope)
    section = envelope.get(PYTHON_SECTION)
    if section is None:
        # Starsza kopia rdzenia pozostaje użyteczna. Brak prywatnej sekcji nie
        # ma prawa wyzerować nowszych ustawień interfejsu wxPython.
        return FullBackupImportPlan(None, False, None, core_sections)
    if not isinstance(section, dict):
        raise FullBackupError("Sekcja ustawień interfejsu jest uszkodzona.")

    format_version = section.get("formatVersion")
    if format_version not in (1, PYTHON_FORMAT_VERSION):
        raise FullBackupError("Kopia ma nieobsługiwaną wersję interfejsu.")
    source_profile = section.get("releaseProfile")
    if not isinstance(source_profile, str) or not source_profile.strip():
        raise FullBackupError("Kopia nie podaje profilu wydania.")
    source_profile = source_profile.strip().casefold()
    if source_profile != expected_profile_id.strip().casefold():
        raise FullBackupError(
            "Ta kopia pochodzi z innego profilu AMC. Do kopiowania wspólnych "
            "danych między AMC Test i AMC użyj polecenia odświeżania danych "
            "z AMC Test."
        )

    payload = section.get("state")
    _validate_ui_payload_shape(payload)
    if format_version == 1:
        if core_sections != ALL_SECTIONS:
            raise FullBackupError(
                "Stara sekcja interfejsu nie opisuje kopii częściowej."
            )
        ui_sections = ALL_SECTIONS
    else:
        ui_sections = normalize_sections(section.get("sections"))
        if ui_sections != core_sections:
            raise FullBackupError("Zakres rdzenia i interfejsu w kopii jest różny.")
        expected_hash = section.get("contentSha256")
        actual_hash = _python_fingerprint(ui_sections, payload)
        if not isinstance(expected_hash, str) or not hmac.compare_digest(
            expected_hash.casefold(), actual_hash
        ):
            raise FullBackupError(
                "Ustawienia interfejsu nie przeszły kontroli integralności."
            )

    plan = FullBackupImportPlan(payload, True, source_profile, ui_sections)
    # Weryfikacja używa tej samej normalizacji co zwykły start programu.
    build_imported_ui_state(plan, state_store)
    return plan


def build_imported_ui_state(
    plan: FullBackupImportPlan,
    state_store: StateStore,
) -> LiteState:
    """Złóż nowy stan UI, pozostawiając działy nieobecne w kopii."""

    if not plan.has_ui_state or plan.ui_payload is None:
        return state_store.load()
    current_payload = state_store.to_payload(state_store.load())
    merged = _merge_ui_payload(
        current_payload,
        plan.ui_payload,
        plan.included_sections,
    )
    return state_store.load_payload(merged)


_TOP_LEVEL_SECTIONS = {
    "navigation": CONFIGURATION,
    "shortcut_overrides": CONFIGURATION,
    "list_reading_overrides": CONFIGURATION,
    "session_overrides": CONFIGURATION,
    "audio_output_device_ids_by_session": CONFIGURATION,
    "shuffle_enabled_by_session": CONFIGURATION,
    "preset_overrides": CONFIGURATION,
    "search_history": CONFIGURATION,
    "clip_selections": LIBRARY,
    "playback_history_by_session": LIBRARY,
    "stations": RADIO,
    "recording_history": RADIO,
    "recognized_tracks": RADIO,
    "recognized_tracks_imported_from_amc": RADIO,
    "radio_schedule_overrides": RADIO_SCHEDULES,
    "radio_schedule_wake_override": RADIO_SCHEDULES,
    "podcast_inbox_sort_mode": PODCASTS,
    "podcast_library_sort_mode": PODCASTS,
    "youtube_live_channel_ids": PODCASTS,
    "tidal_queue_items": SERVICES,
    "tidal_sort_modes": SERVICES,
    "spotify_sort_modes": SERVICES,
    "spotify_playlist_destination_ids": SERVICES,
    "service_picker_destination_ids": SERVICES,
    "spotify_playback_history": SERVICES,
    "spotify_queue_items": SERVICES,
}
_OPTION_NAMES = {item.name for item in fields(Options)}


def _option_section(name: str) -> str:
    if name.startswith("radio_") or name in {
        "timeshift_minutes",
        "stay_on_list_after_radio_enter",
    }:
        return RADIO
    if name.startswith("podcast_"):
        return PODCASTS
    return CONFIGURATION


def _filter_ui_payload(payload: dict, sections: tuple[str, ...]) -> dict:
    selected = set(sections)
    result = {
        "version": StateStore.VERSION,
        "session_manager_version": StateStore.SESSION_MANAGER_VERSION,
        "playback_rate_version": StateStore.PLAYBACK_RATE_VERSION,
    }
    raw_options = payload.get("options")
    options = raw_options if isinstance(raw_options, dict) else {}
    result["options"] = {
        key: copy.deepcopy(value)
        for key, value in options.items()
        if key in _OPTION_NAMES and _option_section(key) in selected
    }
    for key, section in _TOP_LEVEL_SECTIONS.items():
        if section in selected and key in payload:
            result[key] = copy.deepcopy(payload[key])
    return result


def _merge_ui_payload(
    current: dict,
    imported: dict,
    sections: tuple[str, ...],
) -> dict:
    selected = set(sections)
    result = copy.deepcopy(current)
    result["version"] = StateStore.VERSION
    result["session_manager_version"] = StateStore.SESSION_MANAGER_VERSION
    result["playback_rate_version"] = StateStore.PLAYBACK_RATE_VERSION
    # Kopia sprzed znacznika niesie w ``rate`` ostatnio użytą prędkość, nie
    # ustawienie; jej przywrócenie znów kazałoby startować wszystkiemu z 2x.
    legacy_rate = imported.get("playback_rate_version") != StateStore.PLAYBACK_RATE_VERSION

    current_options = result.get("options")
    if not isinstance(current_options, dict):
        current_options = {}
    imported_options = imported.get("options")
    if not isinstance(imported_options, dict):
        imported_options = {}
    for name in _OPTION_NAMES:
        if _option_section(name) not in selected:
            continue
        if legacy_rate and name == "rate":
            # Stare ``rate`` było ostatnio używaną prędkością. Nie importujemy
            # go, ale też nie kasujemy bieżącej, już świadomie zapisanej
            # wartości z nowego formatu.
            continue
        current_options.pop(name, None)
        if name in imported_options:
            current_options[name] = copy.deepcopy(imported_options[name])
    result["options"] = current_options

    for key, section in _TOP_LEVEL_SECTIONS.items():
        if section not in selected:
            continue
        result.pop(key, None)
        if key in imported:
            result[key] = copy.deepcopy(imported[key])
    return result


def _validate_ui_payload_shape(payload: object) -> None:
    if not isinstance(payload, dict) or payload.get("version") != StateStore.VERSION:
        raise FullBackupError("Ustawienia interfejsu w kopii są uszkodzone.")
    allowed = {
        "version",
        "session_manager_version",
        "playback_rate_version",
        "options",
        *_TOP_LEVEL_SECTIONS,
    }
    if any(not isinstance(key, str) or key not in allowed for key in payload):
        raise FullBackupError("Ustawienia interfejsu zawierają nieznane dane.")
    options = payload.get("options")
    if not isinstance(options, dict) or any(
        key not in _OPTION_NAMES for key in options
    ):
        raise FullBackupError("Ustawienia interfejsu mają nieprawidłowy format.")


def _read_envelope(path: Path) -> dict:
    try:
        size = path.stat().st_size
    except OSError as error:
        raise FullBackupError("Nie można odczytać wskazanej kopii danych.") from error
    if size <= 0 or size > MAXIMUM_BACKUP_BYTES:
        raise FullBackupError("Kopia danych ma nieprawidłowy rozmiar.")
    try:
        raw = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise FullBackupError("Kopia danych jest pusta albo uszkodzona.") from error
    if not isinstance(raw, dict):
        raise FullBackupError("Kopia danych ma nieprawidłowy format.")
    return raw


def _validate_core_envelope(envelope: dict) -> tuple[str, ...]:
    kind = envelope.get("kind")
    if kind == "fullBackup":
        sections = ALL_SECTIONS
    elif kind == "profileBackup":
        sections = normalize_sections(envelope.get("sections"))
    else:
        raise FullBackupError("Wskazany plik nie jest kopią danych AMC.")
    version = envelope.get("schemaVersion")
    if type(version) is not int or version <= 0:
        raise FullBackupError("Kopia danych nie ma prawidłowej wersji.")
    if not isinstance(envelope.get("state"), dict):
        raise FullBackupError("Kopia nie zawiera danych aplikacji.")
    return sections


def _python_fingerprint(sections: tuple[str, ...], payload: dict) -> str:
    canonical = json.dumps(
        {"sections": list(sections), "state": payload},
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
    return hashlib.sha256(canonical).hexdigest()


def _write_json_atomically(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    handle, temporary_name = tempfile.mkstemp(
        dir=str(path.parent), prefix=f".{path.name}.", suffix=".tmp"
    )
    temporary = Path(temporary_name)
    try:
        with os.fdopen(handle, "w", encoding="utf-8") as stream:
            json.dump(payload, stream, ensure_ascii=False, indent=2)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    except BaseException:
        try:
            temporary.unlink(missing_ok=True)
        except OSError:
            pass
        raise
