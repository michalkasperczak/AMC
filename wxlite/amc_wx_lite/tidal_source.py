"""Read-only TIDAL collection views for the wxPython interface.

The full AMC persists the last complete TIDAL collection snapshot in
``tidal.cachedCollectionItems``.  Tokens stay in Windows Credential Manager;
this reader never needs them and never opens the profile for writing.

Model values and spoken labels deliberately stay separate.  Opaque service
identifiers are used only as stable row keys.  A list row receives the title,
artist, kind, duration and public share address, never ``repr`` of the source
object or the private playback handle stored in ``source``.
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Sequence

from .list_model import Row
from .profile_layout import ProfileLayout
from .radio_recording import format_duration
from .radio_source import (
    SORT_ADDED_NEWEST,
    SORT_ALPHABETICAL,
    SORT_CUSTOM,
    collection_sort_modes_from_amc_state,
)


VIEW_LIBRARY = "library"
VIEW_FAVORITES = "favorites"
VIEW_PLAYLISTS = "playlists"
TIDAL_VIEWS = (VIEW_LIBRARY, VIEW_FAVORITES, VIEW_PLAYLISTS)

_CONTAINER_KINDS = {"album", "artist", "playlist"}
_PLAYABLE_KINDS = {"track", "video"}
_KNOWN_KINDS = _CONTAINER_KINDS | _PLAYABLE_KINDS
_NUMERIC_KINDS = {
    0: "track",
    1: "album",
    2: "playlist",
    3: "artist",
    9: "video",
}

_VIEW_HEADINGS = {
    VIEW_LIBRARY: "Biblioteka TIDAL",
    VIEW_FAVORITES: "Ulubione TIDAL",
    VIEW_PLAYLISTS: "Playlisty TIDAL",
}
_PROFILE_VIEW_NAMES = {
    VIEW_LIBRARY: "Biblioteka",
    VIEW_FAVORITES: "Ulubione",
    VIEW_PLAYLISTS: "Playlisty",
}


class TidalProfileError(RuntimeError):
    """A short, user-facing profile read failure."""


@dataclass(frozen=True, slots=True)
class TidalCachedItem:
    item_id: str
    service_id: str | None
    title: str
    artist: str
    kind: str
    duration_ticks: int
    public_uri: str | None
    related_album_service_id: str | None
    related_album_title: str
    added_ticks: int | None
    is_favorite: bool
    is_in_library: bool
    is_available: bool


@dataclass(frozen=True, slots=True)
class TidalViewResult:
    view: str
    heading: str
    rows: tuple[Row, ...]
    order_matches_amc: bool


@dataclass(frozen=True, slots=True)
class TidalContainerResult:
    heading: str
    rows: tuple[Row, ...]


class TidalContainerPayloadError(RuntimeError):
    """A malformed host answer that must not become accessible list text."""


def _text(value: object) -> str:
    return value.strip() if isinstance(value, str) else ""


def _kind(value: object) -> str | None:
    if isinstance(value, str):
        candidate = value.strip().casefold()
        return candidate if candidate in _KNOWN_KINDS else None
    if type(value) is int:
        return _NUMERIC_KINDS.get(value)
    return None


def _ticks(value: object) -> int:
    return max(0, value) if type(value) is int else 0


def _optional_ticks(value: object) -> int | None:
    return max(0, value) if type(value) is int and value > 0 else None


def items_from_amc_state(raw: object) -> tuple[TidalCachedItem, ...]:
    """Sanitize the durable TIDAL snapshot without exposing unknown fields."""

    if not isinstance(raw, dict):
        return ()
    tidal = raw.get("tidal")
    source = tidal.get("cachedCollectionItems") if isinstance(tidal, dict) else None
    if not isinstance(source, list):
        return ()

    result: list[TidalCachedItem] = []
    seen: set[str] = set()
    for entry in source:
        if not isinstance(entry, dict):
            continue
        item_id = _text(entry.get("id"))
        title = _text(entry.get("title"))
        kind = _kind(entry.get("kind"))
        if not item_id or not title or kind is None or item_id in seen:
            continue
        seen.add(item_id)
        public_uri = _text(entry.get("publicUri")) or None
        result.append(TidalCachedItem(
            item_id=item_id,
            service_id=_text(entry.get("externalId")) or None,
            title=title,
            artist=_text(entry.get("artist")),
            kind=kind,
            duration_ticks=_ticks(entry.get("durationTicks")),
            public_uri=public_uri,
            related_album_service_id=(
                _text(entry.get("relatedAlbumExternalId")) or None
            ),
            related_album_title=_text(entry.get("relatedAlbumTitle")),
            added_ticks=_optional_ticks(entry.get("collectionAddedUtcTicks")),
            is_favorite=entry.get("isFavorite") is True,
            is_in_library=entry.get("isInLibrary") is True,
            is_available=entry.get("isAvailable") is not False,
        ))
    return tuple(result)


def _membership(items: Iterable[TidalCachedItem], view: str) -> list[TidalCachedItem]:
    if view == VIEW_FAVORITES:
        return [
            item for item in items
            if item.is_available and item.kind in _PLAYABLE_KINDS and item.is_favorite
        ]
    if view == VIEW_PLAYLISTS:
        return [
            item for item in items
            if item.is_available and item.kind == "playlist" and item.is_in_library
        ]
    return [
        item for item in items
        if item.is_available and item.kind in _CONTAINER_KINDS and item.is_in_library
    ]


def _collection_orders(raw: object, view: str, *, custom: bool) -> tuple[str, ...]:
    if not isinstance(raw, dict):
        return ()
    orders = raw.get("collectionOrders")
    if not isinstance(orders, dict):
        return ()
    if view == VIEW_FAVORITES:
        field = "favoriteItemIdsBySession" if custom else "favoriteAddedItemIdsBySession"
        storage_key = "tidal"
    else:
        field = "libraryItemIdsBySession" if custom else "libraryAddedItemIdsBySession"
        storage_key = "tidal" if view == VIEW_LIBRARY else "tidal|Playlisty"
    by_session = orders.get(field)
    if not isinstance(by_session, dict):
        return ()
    values = by_session.get(storage_key)
    if not isinstance(values, list):
        return ()
    return tuple(
        value.strip() for value in values
        if isinstance(value, str) and value.strip()
    )


def _manual_order(
    items: Sequence[TidalCachedItem], stored: Sequence[str]
) -> list[TidalCachedItem]:
    first: dict[str, int] = {}
    for index, item_id in enumerate(stored):
        first.setdefault(item_id, index)
    sentinel = len(stored) + 1
    return [
        item
        for _, _, item in sorted(
            (
                (first.get(item.item_id, sentinel), original, item)
                for original, item in enumerate(items)
            ),
            key=lambda triple: (triple[0], triple[1]),
        )
    ]


def _order_items(
    raw: object,
    items: Sequence[TidalCachedItem],
    view: str,
) -> tuple[list[TidalCachedItem], bool]:
    modes = collection_sort_modes_from_amc_state(
        raw if isinstance(raw, dict) else {}, session="tidal"
    )
    profile_view = _PROFILE_VIEW_NAMES[view]
    mode = modes.get(profile_view.casefold(), SORT_ADDED_NEWEST)

    if mode == SORT_ALPHABETICAL:
        # The exact full-AMC comparator is CurrentCultureIgnoreCase.  Python's
        # casefold is a usable fallback, but it is named as such instead of
        # being presented as byte-for-byte parity for all Unicode text.
        return (
            sorted(
                items,
                key=lambda item: (
                    item.artist.casefold() if item.artist else item.title.casefold(),
                    item.title.casefold(),
                    item.artist.casefold(),
                    item.item_id,
                ),
            ),
            False,
        )

    stored = _collection_orders(raw, view, custom=mode == SORT_CUSTOM)
    if stored:
        ordered = _manual_order(items, stored)
        if mode == SORT_ADDED_NEWEST:
            ordered.reverse()
        return ordered, True

    if mode == SORT_ADDED_NEWEST and items and all(
        item.added_ticks is not None for item in items
    ):
        # Same authoritative reconstruction as TidalCollectionAddedOrder:
        # oldest-to-newest by service timestamp, reversed for the view.
        ordered = sorted(
            items,
            key=lambda item: (item.added_ticks or 0, item.item_id),
            reverse=True,
        )
        return ordered, True

    # No durable order and no complete service timestamps.  Keep the snapshot
    # order and say that it is a fallback; silently reversing it would invent
    # chronology that the data does not prove.
    return list(items), False


def _row(item: TidalCachedItem) -> Row:
    details: list[str] = []
    if item.artist and item.artist.casefold() != item.title.casefold():
        details.append(item.artist)
    if item.duration_ticks > 0:
        details.append(format_duration(item.duration_ticks / 10_000_000))
    if item.kind in _CONTAINER_KINDS and item.service_id:
        unavailable = None
    elif item.kind in _CONTAINER_KINDS:
        unavailable = (
            "Ten element TIDAL nie ma danych katalogowych potrzebnych do otwarcia"
        )
    elif item.kind == "track" and item.service_id and item.related_album_service_id:
        unavailable = None
    else:
        unavailable = (
            "Tego elementu nie da się przekazać do oryginalnego TIDALa"
        )
    return Row(
        item_id=item.item_id,
        title=item.title,
        kind=item.kind,
        url=item.public_uri,
        detail=", ".join(details),
        activation_message=unavailable,
        service_id=item.service_id,
        service_kind=item.kind,
        artist_name=item.artist,
        related_album_service_id=item.related_album_service_id,
        related_album_title=item.related_album_title,
    )


def _host_item_row(entry: object) -> Row | None:
    if not isinstance(entry, dict):
        return None
    item_id = _text(entry.get("id"))
    service_id = _text(entry.get("externalId"))
    title = _text(entry.get("title"))
    kind = _kind(entry.get("kind"))
    if not item_id or not service_id or not title or kind is None:
        return None
    artist = _text(entry.get("artist"))
    details: list[str] = []
    if artist and artist.casefold() != title.casefold():
        details.append(artist)
    duration = _ticks(entry.get("durationTicks"))
    if duration:
        details.append(format_duration(duration / 10_000_000))
    related_album_service_id = (
        _text(entry.get("relatedAlbumExternalId")) or None
    )
    related_album_title = _text(entry.get("relatedAlbumTitle"))
    activation_message = None if (
        kind in _CONTAINER_KINDS
        or (kind == "track" and related_album_service_id)
    ) else "Tego elementu nie da się przekazać do oryginalnego TIDALa"
    return Row(
        item_id=item_id,
        title=title,
        kind=kind,
        url=_text(entry.get("publicUri")) or None,
        detail=", ".join(details),
        activation_message=activation_message,
        service_id=service_id,
        service_kind=kind,
        artist_name=artist,
        related_album_service_id=related_album_service_id,
        related_album_title=related_album_title,
    )


def container_result_from_host(payload: object) -> TidalContainerResult:
    """Sanitize one online answer before it reaches wx or NVDA."""

    if not isinstance(payload, dict):
        raise TidalContainerPayloadError("Katalog TIDAL zwrócił nieprawidłową odpowiedź")
    heading = _text(payload.get("heading"))
    raw_items = payload.get("items")
    if not heading or not isinstance(raw_items, list):
        raise TidalContainerPayloadError("Katalog TIDAL zwrócił niepełną odpowiedź")
    rows: list[Row] = []
    seen: set[str] = set()
    for entry in raw_items:
        row = _host_item_row(entry)
        if row is None or row.item_id in seen:
            continue
        seen.add(row.item_id)
        rows.append(row)
    return TidalContainerResult(heading=heading, rows=tuple(rows))


def artist_overview_rows(artist: Row) -> tuple[Row, ...]:
    """The same three user-facing categories as the full AMC TIDAL view."""

    if artist.kind != "artist" or not artist.service_id:
        return ()
    sections = (
        ("albums", "Albumy"),
        ("tracks", "Utwory"),
        ("similarArtists", "Podobni wykonawcy"),
    )
    return tuple(
        Row(
            item_id=f"tidal-section:{artist.item_id}:{section}",
            title=label,
            kind="folder",
            service_id=artist.service_id,
            service_kind="artist",
            artist_name=artist.title,
            service_section=section,
        )
        for section, label in sections
    )


def queue_rows(payload: object) -> tuple[Row, ...]:
    """Odtwórz prywatną kolejkę wyłącznie z jawnego, wąskiego modelu."""
    if not isinstance(payload, list):
        return ()
    rows: list[Row] = []
    seen: set[str] = set()
    for entry in payload:
        if not isinstance(entry, dict):
            continue
        item_id = _text(entry.get("itemId"))
        service_id = _text(entry.get("externalId"))
        title = _text(entry.get("title"))
        album_id = _text(entry.get("relatedAlbumExternalId"))
        if (
            not item_id or item_id in seen or not service_id or not title
            or _kind(entry.get("kind")) != "track" or not album_id
        ):
            continue
        seen.add(item_id)
        artist = _text(entry.get("artist"))
        rows.append(Row(
            item_id=item_id,
            title=title,
            kind="track",
            url=_text(entry.get("publicUri")) or None,
            detail=artist if artist.casefold() != title.casefold() else "",
            service_id=service_id,
            service_kind="track",
            artist_name=artist,
            related_album_service_id=album_id,
            related_album_title=_text(entry.get("relatedAlbumTitle")),
        ))
    return tuple(rows)


def queue_payload(rows: Sequence[Row]) -> list[dict[str, str]]:
    """Zapisz tylko pola niezbędne do ponownego przekazania utworu."""
    result: list[dict[str, str]] = []
    seen: set[str] = set()
    for row in rows:
        if (
            row.kind != "track" or not row.item_id or row.item_id in seen
            or not row.service_id or not row.related_album_service_id
        ):
            continue
        seen.add(row.item_id)
        result.append({
            "itemId": row.item_id,
            "externalId": row.service_id,
            "title": row.title,
            "kind": "track",
            "artist": row.artist_name,
            "publicUri": row.url or "",
            "relatedAlbumExternalId": row.related_album_service_id,
            "relatedAlbumTitle": row.related_album_title,
        })
    return result


def build_view(raw: object, view: str) -> TidalViewResult:
    if view not in TIDAL_VIEWS:
        raise ValueError("Nieznany widok TIDAL")
    members = _membership(items_from_amc_state(raw), view)
    ordered, exact = _order_items(raw, members, view)
    return TidalViewResult(
        view=view,
        heading=_VIEW_HEADINGS[view],
        rows=tuple(_row(item) for item in ordered),
        order_matches_amc=exact,
    )


class TidalSource:
    """Reads one current snapshot from AMC's profile, always read-only."""

    def __init__(self, layout: ProfileLayout) -> None:
        self.layout = layout

    @property
    def is_available(self) -> bool:
        return Path(self.layout.state_json).is_file()

    def load_view(self, view: str) -> TidalViewResult:
        path = Path(self.layout.state_json)
        try:
            raw = json.loads(path.read_text(encoding="utf-8-sig"))
        except FileNotFoundError as error:
            raise TidalProfileError("Nie znaleziono profilu AMC z biblioteką TIDAL") from error
        except json.JSONDecodeError as error:
            raise TidalProfileError("Profil AMC z biblioteką TIDAL jest uszkodzony") from error
        except UnicodeDecodeError as error:
            raise TidalProfileError("Nie można odczytać kodowania profilu TIDAL") from error
        except OSError as error:
            raise TidalProfileError("Nie można teraz odczytać biblioteki TIDAL") from error
        if not isinstance(raw, dict):
            raise TidalProfileError("Profil AMC ma nieoczekiwaną zawartość")
        return build_view(raw, view)
