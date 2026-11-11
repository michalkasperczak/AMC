"""Model historii i ustawien rozpoznawania utworow z Radia.

Siec, podpis akustyczny i probka PCM pozostaja w hoscie C#.  Ten modul
przyjmuje jedynie gotowe, jawne pola wyniku i sklada z nich tekst przeznaczony
dla uzytkownika.  Techniczne identyfikatory nigdy nie sa etykietami listy.
"""

from __future__ import annotations

import csv
import json
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path

from .recognition_actions import cached_matches, search_links


CURRENT_STATION = "CurrentStation"
RECORDING_STATIONS = "RecordingStations"
CURRENT_AND_RECORDING_STATIONS = "CurrentAndRecordingStations"
SCOPES = (
    CURRENT_STATION,
    RECORDING_STATIONS,
    CURRENT_AND_RECORDING_STATIONS,
)
SCOPE_LABELS = {
    CURRENT_STATION: "Tylko aktualnie odtwarzana stacja",
    RECORDING_STATIONS: "Tylko stacje nagrywane w tle",
    CURRENT_AND_RECORDING_STATIONS: (
        "Aktualnie odtwarzana stacja i wszystkie stacje nagrywane w tle"
    ),
}

DOTNET_UNIX_EPOCH_TICKS = 621_355_968_000_000_000
TICKS_PER_SECOND = 10_000_000
RECENT_DUPLICATE_TICKS = 30 * 60 * TICKS_PER_SECOND


def normalize_scope(value: object) -> str:
    """Oddaj tylko znana wartosc modelu; nigdy tekst obiektu lub enuma."""
    return value if isinstance(value, str) and value in SCOPES else CURRENT_STATION


def scope_label(value: object) -> str:
    return SCOPE_LABELS[normalize_scope(value)]


def now_dotnet_ticks() -> int:
    return int(datetime.now(timezone.utc).timestamp() * TICKS_PER_SECOND) + DOTNET_UNIX_EPOCH_TICKS


def _text(value: object) -> str:
    return value.strip() if isinstance(value, str) else ""


def _provider_uri(value: object) -> str:
    uri = _text(value)
    return uri if uri.casefold().startswith(("https://", "http://")) else ""


def read_tracks(raw: object) -> list[dict]:
    """Wczytaj najwyzej 2000 bezpiecznych wpisow prywatnej historii."""
    if not isinstance(raw, list):
        return []
    result: list[dict] = []
    seen: set[str] = set()
    for candidate in raw:
        if len(result) >= 2_000 or not isinstance(candidate, dict):
            continue
        entry_id = _text(candidate.get("id"))
        if not entry_id or entry_id in seen:
            continue
        title = _text(candidate.get("title"))
        artist = _text(candidate.get("artist"))
        if not title and not artist:
            continue
        ticks = candidate.get("recognizedUtcTicks")
        if isinstance(ticks, bool) or not isinstance(ticks, int) or ticks <= 0:
            continue
        seen.add(entry_id)
        entry = {
            "id": entry_id,
            "stationId": _text(candidate.get("stationId")),
            "stationName": _text(candidate.get("stationName")) or "Nieznana stacja",
            "title": title,
            "artist": artist,
            "album": _text(candidate.get("album")),
            "releaseDate": _text(candidate.get("releaseDate")),
            "providerUri": _provider_uri(candidate.get("providerUri")),
            "recognizedUtcTicks": ticks,
        }
        matches = cached_matches(candidate)
        if matches:
            entry["serviceMatches"] = matches
        result.append(entry)
    # The order is user data: Alt+Up/Down may deliberately differ from the
    # recognition timestamp.  New recognitions are inserted at the front by
    # ``merge_host_result`` and older state files were already newest-first.
    return result


def tracks_from_amc_state(raw: object) -> tuple[dict, ...]:
    """Historia z profilu glownego tylko do odczytu, bez innych pol Radia."""
    if not isinstance(raw, dict):
        return ()
    radio = raw.get("radio")
    if not isinstance(radio, dict):
        return ()
    return tuple(read_tracks(radio.get("recognizedTracks")))


@dataclass(frozen=True, slots=True)
class MergeResult:
    added: tuple[dict, ...]
    duplicates: tuple[dict, ...]
    failures: tuple[str, ...]
    requested: int
    message: str


def _same_recent_track(left: dict, right: dict, *, now_ticks: int) -> bool:
    return (
        left.get("stationId", "") == right.get("stationId", "")
        and _text(left.get("title")).casefold() == _text(right.get("title")).casefold()
        and _text(left.get("artist")).casefold() == _text(right.get("artist")).casefold()
        and int(left.get("recognizedUtcTicks") or 0)
        >= now_ticks - RECENT_DUPLICATE_TICKS
    )


def merge_host_result(
    existing: list[dict], payload: object, *, current_ticks: int | None = None
) -> MergeResult:
    """Dodaj wynik hosta, tlumiac ten sam utwor tej stacji przez 30 minut."""
    data = payload if isinstance(payload, dict) else {}
    now_ticks = current_ticks or now_dotnet_ticks()
    added: list[dict] = []
    duplicates: list[dict] = []
    raw_results = data.get("recognized")
    if not isinstance(raw_results, list):
        raw_results = []
    for raw in raw_results:
        if not isinstance(raw, dict):
            continue
        title = _text(raw.get("title"))
        artist = _text(raw.get("artist"))
        if not title and not artist:
            continue
        ticks = raw.get("recognizedUtcTicks")
        if isinstance(ticks, bool) or not isinstance(ticks, int) or ticks <= 0:
            ticks = now_ticks
        entry = {
            "id": uuid.uuid4().hex,
            "stationId": _text(raw.get("stationId")),
            "stationName": _text(raw.get("stationName")) or "Nieznana stacja",
            "title": title,
            "artist": artist,
            "album": _text(raw.get("album")),
            "releaseDate": _text(raw.get("releaseDate")),
            "providerUri": _provider_uri(raw.get("providerUri")),
            "recognizedUtcTicks": ticks,
        }
        clip_token = _text(raw.get("clipToken"))
        if clip_token:
            # Żeton żyje tylko w bieżącym procesie hosta. read_tracks go nie utrwala.
            entry["clipToken"] = clip_token
        duplicate = next(
            (old for old in existing if _same_recent_track(old, entry, now_ticks=now_ticks)),
            None,
        )
        if duplicate is not None:
            if clip_token:
                duplicate["clipToken"] = clip_token
            duplicates.append(entry)
            continue
        existing.insert(0, entry)
        added.append(entry)
    del existing[2_000:]

    failures: list[str] = []
    raw_failures = data.get("failures")
    if isinstance(raw_failures, list):
        for failure in raw_failures:
            if not isinstance(failure, dict):
                continue
            station = _text(failure.get("stationName"))
            message = _text(failure.get("message")) or "Nie rozpoznano utworu"
            failures.append(f"{station}: {message}" if station else message)
    requested = data.get("requested")
    requested = requested if isinstance(requested, int) and not isinstance(requested, bool) else 0
    return MergeResult(
        tuple(added),
        tuple(duplicates),
        tuple(failures),
        max(0, requested),
        _text(data.get("message")),
    )


def track_label(entry: dict) -> str:
    track = " — ".join(
        value for value in (_text(entry.get("title")), _text(entry.get("artist"))) if value
    ) or "Nieznany utwór"
    station = _text(entry.get("stationName")) or "Nieznana stacja"
    return f"{track}, {station}, {local_time_label(entry)}"


def recognition_label(entry: dict) -> str:
    return " — ".join(
        value for value in (_text(entry.get("title")), _text(entry.get("artist"))) if value
    ) or "nieznany utwór"


def local_datetime(entry: dict) -> datetime:
    ticks = int(entry.get("recognizedUtcTicks") or DOTNET_UNIX_EPOCH_TICKS)
    timestamp = max(0.0, (ticks - DOTNET_UNIX_EPOCH_TICKS) / TICKS_PER_SECOND)
    try:
        return datetime.fromtimestamp(timestamp, timezone.utc).astimezone()
    except (OverflowError, OSError, ValueError):
        return datetime.fromtimestamp(0, timezone.utc).astimezone()


def local_time_label(entry: dict, *, seconds: bool = False) -> str:
    return local_datetime(entry).strftime("%Y-%m-%d %H:%M:%S" if seconds else "%Y-%m-%d %H:%M")


def copy_text(entry: dict) -> str:
    lines = [
        f"Tytuł: {_text(entry.get('title'))}",
        f"Wykonawca: {_text(entry.get('artist'))}",
    ]
    if album := _text(entry.get("album")):
        lines.append(f"Album: {album}")
    if release := _text(entry.get("releaseDate")):
        lines.append(f"Wydanie: {release}")
    lines.extend((
        f"Stacja: {_text(entry.get('stationName')) or 'Nieznana stacja'}",
        f"Rozpoznano: {local_time_label(entry, seconds=True)}",
    ))
    return "\n".join(lines)


def service_links(entry: dict) -> dict[str, str]:
    links = search_links(entry)
    # Exact links are resolved only after a deliberate user action.  Once
    # cached, copy/export prefers the exact public track without changing the
    # always-available search destinations for the other services.
    matches = cached_matches(entry)
    for service, label in (("appleMusic", "Apple Music"), ("spotify", "Spotify"), ("tidal", "TIDAL")):
        match = matches.get(service)
        if match and match.get("publicUrl"):
            links[label] = match["publicUrl"]
    return links


def rich_text(entry: dict) -> str:
    links = service_links(entry)
    return copy_text(entry) + "\n" + "\n".join(
        f"{name}: {uri}" for name, uri in links.items()
    )


def export_history(path: str | Path, entries: list[dict]) -> None:
    """Eksport zgodny znaczeniowo z oknem pelnego AMC: JSON albo CSV."""
    target = Path(path)
    ordered = sorted(entries, key=lambda item: item["recognizedUtcTicks"], reverse=True)
    if target.suffix.casefold() == ".csv":
        with target.open("w", encoding="utf-8-sig", newline="") as stream:
            writer = csv.writer(stream, delimiter=";")
            writer.writerow((
                "data", "stacja", "tytuł", "wykonawca", "album", "wydanie",
                "Apple Music", "Spotify", "TIDAL", "YouTube Music", "Discogs", "MusicBrainz",
            ))
            for entry in ordered:
                links = service_links(entry)
                writer.writerow((
                    local_time_label(entry, seconds=True),
                    _text(entry.get("stationName")),
                    _text(entry.get("title")),
                    _text(entry.get("artist")),
                    _text(entry.get("album")),
                    _text(entry.get("releaseDate")),
                    *(links[name] for name in (
                        "Apple Music", "Spotify", "TIDAL", "YouTube Music", "Discogs", "MusicBrainz"
                    )),
                ))
        return
    payload = []
    for entry in ordered:
        links = service_links(entry)
        payload.append({
            "recognizedAt": local_datetime(entry).isoformat(),
            "station": _text(entry.get("stationName")),
            "title": _text(entry.get("title")),
            "artist": _text(entry.get("artist")),
            "album": _text(entry.get("album")),
            "releaseDate": _text(entry.get("releaseDate")),
            "recognitionProviderUrl": _provider_uri(entry.get("providerUri")) or None,
            "serviceSearch": {
                "appleMusic": links["Apple Music"],
                "spotify": links["Spotify"],
                "tidal": links["TIDAL"],
                "youtubeMusic": links["YouTube Music"],
            },
            "catalogueSearch": {
                "discogs": links["Discogs"],
                "musicBrainz": links["MusicBrainz"],
            },
        })
    target.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
