"""Kontrakt nagrywania radia miedzy wxPython i silnikiem AMC.

Python nie koduje dzwieku i nie zapisuje wspolnego profilu. Czyta jedynie
ustawienia pelnego AMC, a nagranie wykonuje host C# tym samym silnikiem, co
glowne okno. Dzieki temu zwykle stacje i YouTube na zywo maja jedna droge.
"""

from __future__ import annotations

import ntpath
import os
import stat
from dataclasses import dataclass, field, replace
from datetime import datetime, timedelta, timezone
from typing import TYPE_CHECKING, Callable, Sequence

from .state_store import Station
from .list_model import Row
from .radio_activity import join_spoken_prefix, normalize_state_position

if TYPE_CHECKING:
    from .library_db import LibraryItem


_FORMATS = ("Mp3", "Aac", "Flac", "Original", "Wav")
_BITRATES = (96, 128, 160, 192, 256, 320)
_OUTCOMES = ("completed", "stopped", "interrupted", "failed")
_POLISH_MONTHS = (
    "", "stycznia", "lutego", "marca", "kwietnia", "maja", "czerwca",
    "lipca", "sierpnia", "września", "października", "listopada", "grudnia",
)


@dataclass(frozen=True, slots=True)
class RadioRecordingHistoryEntry:
    id: str
    station_id: str
    station_name: str
    path: str
    outcome: str
    reason: str
    schedule_name: str
    started_utc_ticks: int
    finished_utc_ticks: int
    saved_file_count: int


@dataclass(frozen=True, slots=True)
class RadioRecordingPreferences:
    """Ustawienia odczytane z ``state.json`` pelnego AMC."""

    format: str = "Mp3"
    bitrate_kbps: int = 192
    default_folder: str | None = None
    folder_preset: str = "radio"
    station_folders: dict[str, str] = field(default_factory=dict)
    prefer_station_folder_in_new_schedules: bool = True

    def payload_for(self, station: Station) -> dict:
        """Argumenty hosta. Id jest techniczne; nazwa jest jedyna etykieta UI."""
        payload = {
            "stationId": station.id,
            "stationName": station.name,
            "url": station.url,
            "format": self.format,
            "bitrateKbps": self.bitrate_kbps,
            "folderPreset": self.folder_preset,
        }
        folder = self.station_folders.get(station.id) or self.default_folder
        if folder:
            payload["folder"] = folder
        return payload


def preferences_from_amc_state(raw: dict) -> RadioRecordingPreferences:
    """Odtworz wybor folderu/formatu pelnego AMC bez zapisu profilu."""
    radio = raw.get("radio")
    if not isinstance(radio, dict):
        return RadioRecordingPreferences()

    raw_format = radio.get("recordingFormat", "Mp3")
    if isinstance(raw_format, int) and not isinstance(raw_format, bool):
        recording_format = _FORMATS[raw_format] if 0 <= raw_format < len(_FORMATS) else "Mp3"
    elif isinstance(raw_format, str):
        recording_format = next(
            (value for value in _FORMATS if value.casefold() == raw_format.casefold()),
            "Mp3",
        )
    else:
        recording_format = "Mp3"

    raw_bitrate = radio.get("recordingBitrateKbps", 192)
    if not isinstance(raw_bitrate, int) or isinstance(raw_bitrate, bool):
        raw_bitrate = 192
    bitrate = min(_BITRATES, key=lambda value: abs(value - raw_bitrate))

    use_podcasts = radio.get("usePodcastDownloadsFolderForRecordings") is True
    folder_preset = "podcasts" if use_podcasts else "radio"
    configured: str | None = None
    if use_podcasts:
        podcasts = raw.get("podcasts")
        if isinstance(podcasts, dict):
            value = podcasts.get("downloadsFolder")
            if isinstance(value, str) and value.strip():
                configured = value.strip()
    else:
        value = radio.get("recordingsFolder")
        if isinstance(value, str) and value.strip():
            configured = value.strip()

    station_folders: dict[str, str] = {}
    for entry in radio.get("stations") or []:
        if not isinstance(entry, dict):
            continue
        station_id = entry.get("id")
        folder = entry.get("recordingFolder")
        if (isinstance(station_id, str) and station_id
                and isinstance(folder, str) and folder.strip()):
            station_folders[station_id] = folder.strip()

    return RadioRecordingPreferences(
        format=recording_format,
        bitrate_kbps=bitrate,
        default_folder=configured,
        folder_preset=folder_preset,
        station_folders=station_folders,
        prefer_station_folder_in_new_schedules=(
            radio.get("preferStationFolderInNewSchedules") is not False
        ),
    )


def format_duration(seconds: object) -> str:
    """Krotki czas nagrania bez technicznych wartosci w komunikacie NVDA."""
    try:
        total = max(0, int(float(seconds)))
    except (TypeError, ValueError, OverflowError):
        total = 0
    hours, rest = divmod(total, 3600)
    minutes, secs = divmod(rest, 60)
    return f"{hours}:{minutes:02d}:{secs:02d}" if hours else f"{minutes}:{secs:02d}"


def active_recording_rows(payload: object) -> list[Row]:
    """Czytelne wiersze widoku ``Nagrywane`` z odpowiedzi hosta.

    Techniczne ``recordingId`` nie trafia do tytulu ani opisu. Gdy jedna
    stacja ma kilka nagran, pokazujemy ja raz, tak jak pelne AMC.
    """
    if not isinstance(payload, dict):
        return []
    entries = payload.get("recordings")
    if not isinstance(entries, list):
        return []
    rows: list[Row] = []
    seen: set[str] = set()
    state_labels = {
        "starting": "przygotowywanie nagrania",
        "recording": "nagrywanie trwa",
        "paused": "nagrywanie wstrzymane",
        "stopping": "zatrzymywanie nagrania",
    }
    for entry in entries:
        if not isinstance(entry, dict):
            continue
        station_id = entry.get("stationId")
        station_name = entry.get("stationName")
        url = entry.get("url")
        if not isinstance(station_id, str) or not station_id:
            continue
        if station_id in seen:
            continue
        seen.add(station_id)
        title = station_name.strip() if isinstance(station_name, str) else ""
        if not title:
            title = "Stacja bez nazwy"
        state = str(entry.get("state") or "")
        detail = state_labels.get(state, "stan nagrania nieznany")
        if state in ("recording", "paused"):
            detail += f", {format_duration(entry.get('durationSeconds'))}"
        completed = entry.get("completedFileCount")
        if isinstance(completed, int) and not isinstance(completed, bool) and completed > 0:
            detail += f", zapisane części: {completed}"
        rows.append(Row(
            item_id=station_id,
            title=title,
            kind="station",
            url=url.strip() if isinstance(url, str) and url.strip() else None,
            detail=detail,
            show_kind=False,
        ))
    return rows


def station_activity_rows(
    rows: Sequence[Row],
    *,
    transport_status: object,
    recording_status: object,
    playback_position: str = "after",
    recording_position: str = "after",
) -> list[Row]:
    """Dopisz do zwyklych wierszy stacji ich zywy, czytelny stan.

    Dane techniczne hosta (``engine``, ``loadedId``, ``stationId`` i surowe
    wartosci stanu) sluza tylko do dopasowania. Do ``state_detail`` trafiaja
    wylacznie zamierzone etykiety dla uzytkownika. Funkcja wymienia osobne pole
    dynamiczne, wiec kolejne odswiezenie nie powiela tekstu i nie niszczy
    zwyklego ``detail`` wiersza.
    """
    transport = transport_status if isinstance(transport_status, dict) else {}
    loaded_id = str(transport.get("loadedId") or "")
    radio_loaded = transport.get("engine") == "radio" and bool(loaded_id)
    paused = bool(transport.get("paused"))

    states_by_station: dict[str, list[str]] = {}
    status = recording_status if isinstance(recording_status, dict) else {}
    entries = status.get("recordings")
    if isinstance(entries, list):
        for entry in entries:
            if not isinstance(entry, dict):
                continue
            station_id = entry.get("stationId")
            if not isinstance(station_id, str) or not station_id:
                continue
            states_by_station.setdefault(station_id, []).append(
                str(entry.get("state") or "")
            )

    playback_position = normalize_state_position(playback_position)
    recording_position = normalize_state_position(recording_position)

    result: list[Row] = []
    for row in rows:
        if row.kind != "station":
            result.append(
                row
                if not (
                    row.state_detail
                    or row.state_prefix
                    or row.playback_activity
                    or row.recording_activity
                )
                else replace(
                    row,
                    state_detail="",
                    state_prefix="",
                    playback_activity=False,
                    recording_activity=False,
                )
            )
            continue

        playback_label = ""
        if radio_loaded and row.item_id == loaded_id:
            playback_label = "odtwarzanie wstrzymane" if paused else "odtwarzane"

        recording_states = states_by_station.get(row.item_id, [])
        active_states = [state for state in recording_states if state != "stopping"]
        recording_label = ""
        if "stopping" in recording_states and not active_states:
            recording_label = "zatrzymywanie nagrania"
        elif "starting" in active_states and not any(
            state in ("recording", "paused") for state in active_states
        ):
            recording_label = "przygotowywanie nagrania"
        else:
            ready = [state for state in active_states if state in ("recording", "paused")]
            paused_count = ready.count("paused")
            if ready and paused_count == 0:
                recording_label = "nagrywane"
            elif ready and paused_count == len(ready):
                recording_label = "nagrywanie wstrzymane"
            elif ready:
                recording_label = "część nagrań wstrzymana"

        before: list[str] = []
        after: list[str] = []
        for label, position in (
            (playback_label, playback_position),
            (recording_label, recording_position),
        ):
            if not label or position == "off":
                continue
            (before if position == "before" else after).append(label)

        state_prefix = join_spoken_prefix(before)
        state_detail = ", ".join(after)
        playback_activity = bool(playback_label)
        recording_activity = bool(recording_label)
        result.append(
            row
            if (
                row.state_detail == state_detail
                and row.state_prefix == state_prefix
                and row.playback_activity == playback_activity
                and row.recording_activity == recording_activity
            )
            else replace(
                row,
                state_detail=state_detail,
                state_prefix=state_prefix,
                playback_activity=playback_activity,
                recording_activity=recording_activity,
            )
        )
    return result


def recording_history_from_amc_state(raw: object) -> tuple[RadioRecordingHistoryEntry, ...]:
    """Odczytaj historie z ``radio.recordingHistory`` bez zmiany profilu."""
    if not isinstance(raw, dict):
        return ()
    radio = raw.get("radio")
    if not isinstance(radio, dict):
        return ()
    source = radio.get("recordingHistory")
    if not isinstance(source, list):
        return ()

    result: list[RadioRecordingHistoryEntry] = []
    for index, item in enumerate(source):
        if not isinstance(item, dict):
            continue
        raw_outcome = item.get("outcome", "Completed")
        if isinstance(raw_outcome, int) and not isinstance(raw_outcome, bool):
            outcome = _OUTCOMES[raw_outcome] if 0 <= raw_outcome < len(_OUTCOMES) else "completed"
        elif isinstance(raw_outcome, str):
            outcome = raw_outcome.strip().casefold()
            if outcome not in _OUTCOMES:
                outcome = "completed"
        else:
            outcome = "completed"

        def text_value(name: str) -> str:
            value = item.get(name)
            return value.strip() if isinstance(value, str) else ""

        def int_value(name: str) -> int:
            value = item.get(name)
            return value if isinstance(value, int) and not isinstance(value, bool) else 0

        entry_id = text_value("id") or f"bez-identyfikatora-{index}"
        result.append(RadioRecordingHistoryEntry(
            id=entry_id,
            station_id=text_value("stationId"),
            station_name=text_value("stationName"),
            path=text_value("path"),
            outcome=outcome,
            reason=text_value("reason"),
            schedule_name=text_value("scheduleName"),
            started_utc_ticks=int_value("startedUtcTicks"),
            finished_utc_ticks=int_value("finishedUtcTicks"),
            saved_file_count=max(0, int_value("savedFileCount")),
        ))
    return tuple(result)


def recording_history_payload_from_event(payload: object) -> dict | None:
    """Zamien koncowe zdarzenie hosta na wpis prywatnej historii wxPython.

    Surowy identyfikator pozostaje w modelu i sluzy tylko do usuwania
    duplikatow. Funkcja nie buduje zadnej etykiety dostepnosciowej.
    """
    if not isinstance(payload, dict):
        return None
    recording_id = payload.get("recordingId")
    if not isinstance(recording_id, str) or not recording_id.strip():
        return None
    source = {
        "id": recording_id,
        "stationId": payload.get("stationId"),
        "stationName": payload.get("stationName"),
        "path": payload.get("path"),
        "outcome": payload.get("outcome"),
        "reason": payload.get("error"),
        "scheduleName": payload.get("scheduleName"),
        "startedUtcTicks": payload.get("startedUtcTicks"),
        "finishedUtcTicks": payload.get("finishedUtcTicks"),
        "savedFileCount": payload.get("savedFileCount"),
    }
    entries = recording_history_from_amc_state({
        "radio": {"recordingHistory": [source]}
    })
    if not entries:
        return None
    entry = entries[0]
    return {
        "id": entry.id,
        "stationId": entry.station_id,
        "stationName": entry.station_name,
        "path": entry.path,
        "outcome": entry.outcome,
        "reason": entry.reason,
        "scheduleName": entry.schedule_name,
        "startedUtcTicks": entry.started_utc_ticks,
        "finishedUtcTicks": entry.finished_utc_ticks,
        "savedFileCount": entry.saved_file_count,
    }


def _when_label(ticks: int, now: datetime) -> str:
    if ticks <= 0:
        return "nieznany czas"
    try:
        finished = (
            datetime(1, 1, 1, tzinfo=timezone.utc)
            + timedelta(microseconds=ticks // 10)
        ).astimezone()
    except (OverflowError, OSError, ValueError):
        return "nieznany czas"
    local_now = now.astimezone() if now.tzinfo else now
    if finished.date() == local_now.date():
        return f"dziś {finished:%H:%M}"
    if finished.date() == (local_now - timedelta(days=1)).date():
        return f"wczoraj {finished:%H:%M}"
    return f"{finished.day} {_POLISH_MONTHS[finished.month]}, {finished:%H:%M}"


def _file_name(path: str) -> str:
    return ntpath.splitext(ntpath.basename(path))[0].strip()


def _folder_label(path: str) -> str:
    directory = ntpath.dirname(path.rstrip("\\/"))
    if not directory:
        return ""
    name = ntpath.basename(directory.rstrip("\\/")) or directory
    return f"folder {name}" if name else ""


def _normalized_recording_path(path: str) -> str:
    if not path:
        return ""
    return ntpath.normcase(ntpath.normpath(path.strip()))


def _recorded_file_detail(item: "LibraryItem") -> str:
    parts: list[str] = []
    if item.duration_ticks > 0:
        parts.append(format_duration(item.duration_ticks / 10_000_000))
    if item.is_favorite:
        parts.append("ulubione")
    if not item.is_in_library:
        parts.append("poza Biblioteką")
    return ", ".join(parts)


def _merge_recorded_files(
    entries: Sequence[RadioRecordingHistoryEntry],
    recorded_files: Sequence["LibraryItem"],
) -> tuple[list[RadioRecordingHistoryEntry], dict[str, str]]:
    """Polacz historie prob z istniejacymi plikami bez podwojnych wierszy."""
    interrupted_paths = {
        _normalized_recording_path(entry.path)
        for entry in entries
        if entry.outcome in ("stopped", "interrupted") and entry.path
    }
    usable_files = [
        item
        for item in recorded_files
        if item.is_radio_recording
        and item.is_available
        and item.path
        and _normalized_recording_path(item.path) not in interrupted_paths
    ]
    file_paths = {_normalized_recording_path(item.path) for item in usable_files}
    history_only = [
        entry
        for entry in entries
        if entry.outcome == "failed"
        or not entry.path
        or _normalized_recording_path(entry.path) not in file_paths
    ]
    history_by_path = {
        _normalized_recording_path(entry.path): entry
        for entry in entries
        if entry.path
    }
    details: dict[str, str] = {}
    merged = list(history_only)
    for item in usable_files:
        source = history_by_path.get(_normalized_recording_path(item.path))
        entry_id = f"plik-biblioteki:{item.id}"
        merged.append(RadioRecordingHistoryEntry(
            id=entry_id,
            station_id=source.station_id if source else "",
            station_name=item.title.strip() or _file_name(item.path) or "Nagranie radia",
            path=item.path,
            outcome="completed",
            reason="",
            schedule_name="",
            started_utc_ticks=source.started_utc_ticks if source else 0,
            finished_utc_ticks=(
                item.radio_recording_completed_utc_ticks
                or (source.finished_utc_ticks if source else 0)
            ),
            saved_file_count=1,
        ))
        details[entry_id] = _recorded_file_detail(item)
    return merged, details


def _path_state(path: str) -> str:
    """``available`` / ``missing`` / ``unavailable`` bez wyjatku w GUI."""
    if not path:
        return "missing"
    try:
        return "available" if stat.S_ISREG(os.stat(path).st_mode) else "missing"
    except FileNotFoundError:
        return "missing"
    except OSError:
        return "unavailable"


def recording_history_rows(
    entries: tuple[RadioRecordingHistoryEntry, ...] | list[RadioRecordingHistoryEntry],
    *,
    recorded_files: Sequence["LibraryItem"] = (),
    now: datetime | None = None,
    path_probe: Callable[[str], str] = _path_state,
) -> list[Row]:
    """Wiersze „Historii nagrywania” zgodne z kolejnoscia etykiet AMC."""
    now_value = now or datetime.now().astimezone()
    merged_entries, file_details = _merge_recorded_files(entries, recorded_files)
    outcome_labels = {
        "completed": "Nagrane",
        "stopped": "Zatrzymane",
        "interrupted": "Przerwane",
        "failed": "Nieudane",
    }
    rows: list[Row] = []
    for entry in sorted(
        merged_entries,
        key=lambda value: (-value.finished_utc_ticks, value.station_name.casefold()),
    ):
        station_name = entry.station_name or "Nieznana stacja"
        playable = entry.outcome in ("completed", "stopped", "interrupted") and bool(entry.path)
        state = path_probe(entry.path) if playable else "missing"
        outcome = outcome_labels.get(entry.outcome, "Nagrane")
        if playable and state == "missing":
            outcome = (
                "Brak pliku nagrania" if entry.outcome == "completed"
                else f"{outcome}, brak pliku nagrania"
            )
        elif playable and state == "unavailable":
            outcome = (
                "Plik nagrania niedostępny" if entry.outcome == "completed"
                else f"{outcome}, plik nagrania niedostępny"
            )

        parts = [outcome, station_name]
        file_name = _file_name(entry.path)
        if file_name and file_name.casefold() not in ", ".join(parts).casefold():
            parts.append(file_name)
        parts.append(_when_label(entry.finished_utc_ticks, now_value))
        if entry.schedule_name:
            parts.append(f"harmonogram {entry.schedule_name}")
        if entry.saved_file_count > 1:
            parts.append(f"plików {entry.saved_file_count}")
        if entry.reason:
            parts.append(entry.reason.rstrip("."))
        folder = _folder_label(entry.path)
        if folder:
            parts.append(folder)
        label = ", ".join(parts)

        activation_message: str | None = None
        path: str | None = entry.path if playable and state == "available" else None
        if not playable:
            reason = entry.reason.rstrip(".") or "Nie zapisano pliku"
            if entry.outcome == "failed":
                activation_message = f"Nagranie {station_name} nie powstało. {reason}"
            else:
                activation_message = f"Brak pliku nagrania {station_name}. {reason}"
        elif state == "missing":
            where = f", {folder}" if folder else ""
            activation_message = (
                f"Plik nagrania {station_name} nie istnieje już na dysku"
                + (f": {file_name}{where}" if file_name else "")
            )
        elif state == "unavailable":
            activation_message = label

        rows.append(Row(
            item_id=f"radio-recording-history:{entry.id}",
            title=label,
            kind="track",
            path=path,
            detail=file_details.get(entry.id, ""),
            show_kind=False,
            activation_message=activation_message,
        ))
    return rows
