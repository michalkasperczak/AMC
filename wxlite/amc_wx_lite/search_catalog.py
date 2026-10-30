"""Accessible, service-aware search over AMC rows.

The module is deliberately free of wx so matching and result labels can be
verified without starting the application or disturbing a recording.
"""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
import unicodedata

from .list_model import Row
from .navigation import LibraryView, SessionId


SESSION_LABELS: dict[SessionId, str] = {
    SessionId.FILES: "Pliki lokalne",
    SessionId.RADIO: "Radio",
    SessionId.PODCASTS: "Podcasty i YouTube",
    SessionId.TIDAL: "TIDAL",
    SessionId.WIIM: "WiiM",
    SessionId.SONOS: "Sonos",
    SessionId.SPOTIFY: "Spotify",
    SessionId.FRONTIER_SMART: "Frontier Smart",
    SessionId.RADIOARCHIWISTA: "Radio Archiwista",
}


class SearchResultAction(Enum):
    OPEN = "open"
    RELATIONS = "relations"
    TOGGLE_PLAYBACK = "togglePlayback"
    QUEUE = "queue"
    PLAY_NEXT = "playNext"
    FAVORITE = "favorite"
    LIBRARY = "library"
    PLAYLIST = "playlist"
    PRESET = "preset"
    INFORMATION = "information"
    DOWNLOAD = "download"
    SAVE_AS = "saveAs"


@dataclass(frozen=True, slots=True)
class SearchEntry:
    """One result plus its private navigation target.

    ``session_label`` and :attr:`accessible_label` are the only values meant
    for screen-reader output.  IDs and view values remain model data.
    """

    session_id: SessionId
    row: Row
    source_view: LibraryView | None = None
    parent_id: str | None = None
    # Rodzaj wyniku publicznego katalogu służy tylko do materializacji po
    # Enterze. Nie trafia do nazwy dostępnej ani tekstu wiersza.
    remote_kind: str | None = None
    # Jawna nazwa rodzaju używana tylko w oknie wyników. Pozwala adapterom
    # opisać element bez dopisywania tego samego tekstu do każdej pozycji
    # zwykłej, jednorodnej listy.
    result_kind_label: str = ""

    @property
    def session_label(self) -> str:
        return SESSION_LABELS[self.session_id]

    @property
    def accessible_label(self) -> str:
        parts = [self.row.title]
        if self.row.artist_name and self.row.artist_name not in parts:
            parts.append(self.row.artist_name)
        if self.kind_label:
            parts.append(self.kind_label)
        if self.row.detail and self.row.detail not in parts:
            parts.append(self.row.detail)
        parts.append(self.session_label)
        return ", ".join(part for part in parts if part)

    @property
    def kind_label(self) -> str:
        return self.result_kind_label.strip() or self.row.kind_label


def normalize_search_text(value: str) -> str:
    """Case- and accent-insensitive text, matching full AMC semantics."""

    decomposed = unicodedata.normalize("NFKD", value.casefold())
    return "".join(char for char in decomposed if not unicodedata.combining(char))


def query_words(query: str) -> tuple[str, ...]:
    return tuple(
        word
        for word in normalize_search_text(query).split()
        if word
    )


def record_query(history: list[str], query: str, *, limit: int = 50) -> list[str]:
    """Newest-first, case-insensitive history with a strict size limit."""

    value = query.strip()
    if not value:
        return list(history[:limit])
    key = value.casefold()
    return [
        value,
        *(item for item in history if item.strip().casefold() != key),
    ][:limit]


def matches(entry: SearchEntry, words: tuple[str, ...]) -> bool:
    """Require every word in intentional user-facing metadata."""

    if not words:
        return False
    row = entry.row
    searchable = normalize_search_text(" ".join((
        row.title,
        row.artist_name,
        entry.kind_label,
        row.detail,
        entry.session_label,
    )))
    return all(word in searchable for word in words)


def search(entries: list[SearchEntry], query: str) -> list[SearchEntry]:
    """Return stable, de-duplicated results in source order."""

    words = query_words(query)
    if not words:
        return []
    result: list[SearchEntry] = []
    seen: set[tuple[SessionId, str]] = set()
    for entry in entries:
        identity = (entry.session_id, entry.row.item_id)
        if identity in seen or not matches(entry, words):
            continue
        seen.add(identity)
        result.append(entry)
    return result
