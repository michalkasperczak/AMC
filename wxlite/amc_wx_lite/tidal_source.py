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
from typing import Callable, Iterable, Sequence, TypeVar

from .collation import (
    COLLATION_TITLE_IGNORE_CASE,
    HostCollation,
    HostCollationUnavailable,
)
from .list_model import Row
from . import list_filter
from .paging import PAGE_SIZE, load_more_row
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
VIEW_SONGS = "songs"
VIEW_ALBUMS = "albums"
VIEW_ARTISTS = "artists"
TIDAL_VIEWS = (
    VIEW_LIBRARY,
    VIEW_FAVORITES,
    VIEW_PLAYLISTS,
    VIEW_SONGS,
    VIEW_ALBUMS,
    VIEW_ARTISTS,
)
TIDAL_SORT_MODES = (SORT_ADDED_NEWEST, SORT_ALPHABETICAL, SORT_CUSTOM)

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
    VIEW_SONGS: "Utwory TIDAL",
    VIEW_ALBUMS: "Albumy TIDAL",
    VIEW_ARTISTS: "Wykonawcy TIDAL",
}
_PROFILE_VIEW_NAMES = {
    VIEW_LIBRARY: "Biblioteka",
    VIEW_FAVORITES: "Ulubione",
    VIEW_PLAYLISTS: "Playlisty",
    VIEW_SONGS: "Utwory",
    VIEW_ALBUMS: "Albumy",
    VIEW_ARTISTS: "Wykonawcy",
}


def native_track_id(external_id: object) -> str | None:
    """Return the numeric playback ID from a catalog track ID."""
    if not isinstance(external_id, str):
        return None
    value = external_id.removeprefix("tracks:")
    if 1 <= len(value) <= 18 and value.isascii() and value.isdecimal():
        return value
    return None


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
    related_artist_service_id: str | None
    related_artist_name: str
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
    sort_mode: str


def paged_collection_rows(
    rows: Sequence[Row],
    *,
    scope: str,
    query: str = "",
    requested: int = PAGE_SIZE,
) -> tuple[tuple[Row, ...], int, int]:
    """Page a complete ordered snapshot, searching all titles before slicing."""

    matches = list_filter.filter_rows(rows, query)
    total = len(matches)
    loaded = min(total, max(PAGE_SIZE, int(requested)))
    visible = matches[:loaded]
    if loaded < total:
        visible.append(load_more_row(f"tidal:{scope}", loaded))
    return tuple(visible), total, loaded


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
            related_artist_service_id=(
                _text(entry.get("relatedArtistExternalId")) or None
            ),
            related_artist_name=_text(entry.get("relatedArtistName")),
            added_ticks=_optional_ticks(entry.get("collectionAddedUtcTicks")),
            is_favorite=entry.get("isFavorite") is True,
            is_in_library=entry.get("isInLibrary") is True,
            is_available=entry.get("isAvailable") is not False,
        ))
    return tuple(result)


def _membership(items: Iterable[TidalCachedItem], view: str) -> list[TidalCachedItem]:
    if view in (VIEW_FAVORITES, VIEW_SONGS):
        return [
            item for item in items
            if item.is_available
            and item.is_favorite
            and (
                item.kind == "track"
                if view == VIEW_SONGS else item.kind in _PLAYABLE_KINDS
            )
        ]
    if view == VIEW_PLAYLISTS:
        return [
            item for item in items
            if item.is_available and item.kind == "playlist" and item.is_in_library
        ]
    if view in (VIEW_ALBUMS, VIEW_ARTISTS):
        kind = "album" if view == VIEW_ALBUMS else "artist"
        return [
            item for item in items
            if item.is_available and item.kind == kind and item.is_in_library
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
    if view in (VIEW_FAVORITES, VIEW_SONGS):
        field = "favoriteItemIdsBySession" if custom else "favoriteAddedItemIdsBySession"
        storage_key = "tidal"
    else:
        field = "libraryItemIdsBySession" if custom else "libraryAddedItemIdsBySession"
        storage_key = "tidal|Playlisty" if view == VIEW_PLAYLISTS else "tidal"
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


_Orderable = TypeVar("_Orderable")


def _manual_order(
    items: Sequence[_Orderable],
    stored: Sequence[str],
    *,
    id_of: Callable[[_Orderable], str] = lambda item: item.item_id,  # type: ignore[attr-defined]
) -> list[_Orderable]:
    first: dict[str, int] = {}
    for index, stored_id in enumerate(stored):
        first.setdefault(stored_id, index)
    sentinel = len(stored) + 1
    return [
        item
        for _, _, item in sorted(
            (
                (first.get(id_of(item), sentinel), original, item)
                for original, item in enumerate(items)
            ),
            key=lambda triple: (triple[0], triple[1]),
        )
    ]


def _effective_sort_mode(raw: object, view: str, override: str | None) -> str:
    if override in TIDAL_SORT_MODES:
        return override
    modes = collection_sort_modes_from_amc_state(
        raw if isinstance(raw, dict) else {}, session="tidal"
    )
    profile_view = _PROFILE_VIEW_NAMES[view]
    mode = modes.get(profile_view.casefold(), SORT_ADDED_NEWEST)
    return mode if mode in TIDAL_SORT_MODES else SORT_ADDED_NEWEST


def _alphabetical_items(
    items: Sequence[TidalCachedItem],
    collation: HostCollation | None,
) -> tuple[list[TidalCachedItem], bool]:
    fallback = sorted(
        items,
        key=lambda item: (
            item.artist.casefold() if item.artist else item.title.casefold(),
            item.title.casefold(),
            item.artist.casefold(),
            item.item_id,
        ),
    )
    if collation is None:
        return fallback, False
    try:
        ordered = sorted(items, key=lambda item: item.item_id)
        ordered = collation.sort_rows(
            ordered,
            key=lambda item: item.artist,
            mode=COLLATION_TITLE_IGNORE_CASE,
        )
        ordered = collation.sort_rows(
            ordered,
            key=lambda item: item.title,
            mode=COLLATION_TITLE_IGNORE_CASE,
        )
        ordered = collation.sort_rows(
            ordered,
            key=lambda item: item.artist or item.title,
            mode=COLLATION_TITLE_IGNORE_CASE,
        )
        return ordered, True
    except HostCollationUnavailable:
        return fallback, False


def _alphabetical_rows(
    rows: Sequence[Row],
    collation: HostCollation | None,
) -> tuple[list[Row], bool]:
    fallback = sorted(
        rows,
        key=lambda row: (
            row.artist_name.casefold() if row.artist_name else row.title.casefold(),
            row.title.casefold(),
            row.artist_name.casefold(),
            row.item_id,
        ),
    )
    if collation is None:
        return fallback, False
    try:
        ordered = sorted(rows, key=lambda row: row.item_id)
        ordered = collation.sort_rows(
            ordered,
            key=lambda row: row.artist_name,
            mode=COLLATION_TITLE_IGNORE_CASE,
        )
        ordered = collation.sort_rows(
            ordered,
            key=lambda row: row.title,
            mode=COLLATION_TITLE_IGNORE_CASE,
        )
        ordered = collation.sort_rows(
            ordered,
            key=lambda row: row.artist_name or row.title,
            mode=COLLATION_TITLE_IGNORE_CASE,
        )
        return ordered, True
    except HostCollationUnavailable:
        return fallback, False


def _order_items(
    raw: object,
    items: Sequence[TidalCachedItem],
    view: str,
    *,
    sort_mode: str | None = None,
    collation: HostCollation | None = None,
) -> tuple[list[TidalCachedItem], bool, str]:
    mode = _effective_sort_mode(raw, view, sort_mode)

    if mode == SORT_ALPHABETICAL:
        ordered, exact = _alphabetical_items(items, collation)
        return ordered, exact, mode

    stored = _collection_orders(raw, view, custom=mode == SORT_CUSTOM)
    if stored:
        ordered = _manual_order(items, stored)
        if mode == SORT_ADDED_NEWEST:
            ordered.reverse()
        return ordered, True, mode

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
        return ordered, True, mode

    # No durable order and no complete service timestamps.  Keep the snapshot
    # order and say that it is a fallback; silently reversing it would invent
    # chronology that the data does not prove.
    return list(items), False, mode


def _order_rows(
    raw: object,
    rows: Sequence[Row],
    view: str,
    *,
    sort_mode: str | None = None,
    collation: HostCollation | None = None,
) -> tuple[list[Row], bool, str]:
    """Order freshly synchronized host rows with the same durable policy."""
    mode = _effective_sort_mode(raw, view, sort_mode)
    if mode == SORT_ALPHABETICAL:
        ordered, exact = _alphabetical_rows(rows, collation)
        return ordered, exact, mode

    # Świeża odpowiedź hosta niesie czas z usługi. Jest silniejszym dowodem
    # chronologii niż starsza lista Id z profilu, która może jeszcze nie znać
    # albumu dodanego podczas bieżącej synchronizacji.
    if mode == SORT_ADDED_NEWEST and rows and all(
        row.collection_added_ticks is not None for row in rows
    ):
        return (
            sorted(
                rows,
                key=lambda row: (row.collection_added_ticks or 0, row.item_id),
                reverse=True,
            ),
            True,
            mode,
        )

    stored = _collection_orders(raw, view, custom=mode == SORT_CUSTOM)
    if stored:
        ordered = _manual_order(rows, stored, id_of=lambda row: row.item_id)
        # Freshly synchronized items missing from the saved order are retained,
        # but the order can no longer be claimed byte-for-byte exact.
        exact = all(row.item_id in stored for row in rows)
        if mode == SORT_ADDED_NEWEST:
            ordered.reverse()
        return ordered, exact, mode

    return list(rows), False, mode


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
        show_kind=item.kind not in ("album", "track"),
        detail=", ".join(details),
        activation_message=unavailable,
        service_id=item.service_id,
        service_kind=item.kind,
        artist_name=item.artist,
        related_album_service_id=item.related_album_service_id,
        related_album_title=item.related_album_title,
        related_artist_service_id=item.related_artist_service_id,
        related_artist_name=item.related_artist_name,
        collection_added_ticks=item.added_ticks,
        duration_ticks=item.duration_ticks,
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
    related_artist_service_id = (
        _text(entry.get("relatedArtistExternalId")) or None
    )
    related_artist_name = _text(entry.get("relatedArtistName"))
    activation_message = None if (
        kind in _CONTAINER_KINDS
        or (kind == "track" and related_album_service_id)
    ) else "Tego elementu nie da się przekazać do oryginalnego TIDALa"
    return Row(
        item_id=item_id,
        title=title,
        kind=kind,
        url=_text(entry.get("publicUri")) or None,
        show_kind=kind not in ("album", "track"),
        detail=", ".join(details),
        activation_message=activation_message,
        service_id=service_id,
        service_kind=kind,
        artist_name=artist,
        related_album_service_id=related_album_service_id,
        related_album_title=related_album_title,
        related_artist_service_id=related_artist_service_id,
        related_artist_name=related_artist_name,
        collection_added_ticks=_optional_ticks(
            entry.get("collectionAddedUtcTicks")
        ),
        duration_ticks=duration,
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
            related_artist_service_id=(
                _text(entry.get("relatedArtistExternalId")) or None
            ),
            related_artist_name=_text(entry.get("relatedArtistName")),
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
            "relatedArtistExternalId": row.related_artist_service_id or "",
            "relatedArtistName": row.related_artist_name,
        })
    return result


def build_view(
    raw: object,
    view: str,
    *,
    sort_mode: str | None = None,
    collation: HostCollation | None = None,
) -> TidalViewResult:
    if view not in TIDAL_VIEWS:
        raise ValueError("Nieznany widok TIDAL")
    members = _membership(items_from_amc_state(raw), view)
    ordered, exact, effective_mode = _order_items(
        raw,
        members,
        view,
        sort_mode=sort_mode,
        collation=collation,
    )
    return TidalViewResult(
        view=view,
        heading=_VIEW_HEADINGS[view],
        rows=tuple(_row(item) for item in ordered),
        order_matches_amc=exact,
        sort_mode=effective_mode,
    )


class TidalSource:
    """Reads one current snapshot from AMC's profile, always read-only."""

    def __init__(self, layout: ProfileLayout) -> None:
        self.layout = layout

    @property
    def is_available(self) -> bool:
        return Path(self.layout.state_json).is_file()

    def _read_state(self) -> dict:
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
        return raw

    def load_view(
        self,
        view: str,
        *,
        sort_mode: str | None = None,
        collation: HostCollation | None = None,
    ) -> TidalViewResult:
        return build_view(
            self._read_state(),
            view,
            sort_mode=sort_mode,
            collation=collation,
        )

    def order_rows(
        self,
        view: str,
        heading: str,
        rows: Sequence[Row],
        *,
        sort_mode: str | None = None,
        collation: HostCollation | None = None,
    ) -> TidalViewResult:
        """Order a fresh host snapshot without writing either AMC profile."""
        if view not in TIDAL_VIEWS:
            raise ValueError("Nieznany widok TIDAL")
        ordered, exact, effective_mode = _order_rows(
            self._read_state(),
            rows,
            view,
            sort_mode=sort_mode,
            collation=collation,
        )
        return TidalViewResult(
            view=view,
            heading=heading,
            rows=tuple(ordered),
            order_matches_amc=exact,
            sort_mode=effective_mode,
        )
