"""Wybór informacji czytanych z gotowego wiersza i pod lewą strzałką.

Nawigacja nie uruchamia sondy, sieci ani odczytu bazy. Nazwa pozostaje zawsze.
"""

from __future__ import annotations

from typing import Mapping, Sequence

from .list_model import Row

UPDOWN_FIELDS = ("kind", "artist", "duration", "other", "status")
LEFT_FIELDS = ("duration", "bitrate", "size", "format", "sampleRate", "artist", "country", "language", "cloud", "other")
UPDOWN_LABELS = {
    "kind": "Rodzaj pozycji", "artist": "Wykonawca", "duration": "Długość",
    "other": "Inne szczegóły dostępne na liście", "status": "Stan odtwarzania i nagrywania",
}
LEFT_LABELS = {
    "duration": "Długość", "bitrate": "Bitrate", "size": "Rozmiar pliku",
    "format": "Format i kodek", "sampleRate": "Częstotliwość próbkowania",
    "artist": "Wykonawca", "country": "Kraj", "language": "Język",
    "cloud": "Informacja o pliku w chmurze", "other": "Inne szczegóły, na przykład folder",
}


# Jedno miejsce rozszerzania kolejnej sesji. Ustawienie globalne zawiera
# pełny katalog; opcje sesji i faktyczny odczyt widzą tylko jej możliwości.
LEFT_CAPABILITIES: dict[str, tuple[str, ...]] = {
    "files": LEFT_FIELDS,
    "radio": ("duration", "bitrate", "size", "format", "sampleRate", "country", "language", "other"),
    "podcasts": ("duration", "bitrate", "size", "format", "sampleRate", "artist", "other"),
    "tidal": ("duration", "artist"),
    "spotify": ("duration", "artist", "other"),
    "radioarchiwista": ("duration",),
    "wiim": ("duration", "bitrate", "format", "sampleRate", "artist", "other"),
    "sonos": ("duration", "bitrate", "format", "sampleRate", "artist", "other"),
    "frontierSmart": ("other",),
}


def available_left_fields(session: str) -> tuple[str, ...]:
    return LEFT_CAPABILITIES.get(session, ())


def normalize_fields(value: object, allowed: tuple[str, ...]) -> list[str]:
    if not isinstance(value, (list, tuple)):
        return list(allowed)
    chosen = set(item for item in value if isinstance(item, str))
    return [item for item in allowed if item in chosen]


def session_fields(options, overrides: Mapping[str, object] | None, direction: str, *, session: str = "") -> tuple[str, ...]:
    key = "list_updown_fields" if direction == "updown" else "list_left_fields"
    allowed = UPDOWN_FIELDS if direction == "updown" else LEFT_FIELDS
    own = overrides.get(key) if isinstance(overrides, dict) else None
    selected = normalize_fields(own if own is not None else getattr(options, key), allowed)
    if direction == "left" and session:
        available = set(available_left_fields(session))
        selected = [field for field in selected if field in available]
    return tuple(selected)


def duration_text(ticks: int) -> str:
    seconds = max(0, int(ticks // 10_000_000))
    hours, rest = divmod(seconds, 3600)
    minutes, seconds = divmod(rest, 60)
    return f"{hours}:{minutes:02d}:{seconds:02d}" if hours else f"{minutes}:{seconds:02d}"


def row_columns(row: Row, fields: Sequence[str]) -> tuple[str, str, str]:
    """Wyłącznie dane już zapisane w Row; żaden ruch kursora nie pyta hosta."""
    selected = set(fields)
    title = row.title
    if "status" in selected:
        if row.state_prefix:
            title = f"{row.state_prefix} {title}"
        if row.playback_state:
            title = f"{row.playback_state}, {title}"
    kind = row.kind_label if "kind" in selected and row.show_kind else ""
    artist = row.artist_name.strip()
    duration = duration_text(row.duration_ticks) if row.duration_ticks > 0 else ""
    values: list[str] = []
    if "artist" in selected and artist:
        values.append(artist)
    if "duration" in selected and duration:
        values.append(duration)
    if "other" in selected and row.detail:
        # Wiersze TIDAL i Spotify niosą wykonawcę i czas także w detail.
        # Usuwamy tylko całe, dokładnie rozpoznane części; reszta zostaje.
        remainder = [piece.strip() for piece in row.detail.split(", ")
                     if piece.strip() and piece.strip() not in (artist, duration)]
        values.extend(remainder)
    if "status" in selected and row.state_detail.strip():
        values.append(row.state_detail.strip())
    # Jeden sens czytamy raz, nawet jeśli źródło podało go w dwóch polach.
    unique = list(dict.fromkeys(value for value in values if value and value != title and value != kind))
    return title, kind, ", ".join(unique)
