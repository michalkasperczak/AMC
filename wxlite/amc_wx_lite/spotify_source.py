"""Pure, read-only Spotify DTO and accessibility presenter.

This module deliberately has no network, credentials, wx or playback code.
It accepts only a small explicit payload shaped like the existing TIDAL host
DTO, converts it into :class:`Row` objects, and keeps opaque Spotify IDs/URIs
outside every ordinary accessible label.
"""

from __future__ import annotations

from dataclasses import dataclass
from collections.abc import Sequence

from .list_model import Row
from .podcast_source import PodcastDescription
from .radio_recording import format_duration


_CONTAINER_KINDS = {"artist", "album", "playlist", "podcast"}
_PLAYABLE_KINDS = {"track", "episode"}
_KNOWN_KINDS = _CONTAINER_KINDS | _PLAYABLE_KINDS
_COLLECTION_VIEWS = {
    "liked",
    "albums",
    "artists",
    "playlists",
    "podcasts",
    "episodes",
}
_MAX_HISTORY_ITEMS = 500
_HISTORY_TEXT_FIELDS = (
    "itemId",
    "externalId",
    "title",
    "kind",
    "artist",
    "publicUri",
    "relatedAlbumExternalId",
    "relatedAlbumTitle",
    "relatedArtistExternalId",
    "relatedArtistName",
    "relatedPodcastExternalId",
    "relatedPodcastTitle",
)


class SpotifyContainerPayloadError(RuntimeError):
    """A short user-facing error for an invalid controller payload."""


@dataclass(frozen=True, slots=True)
class SpotifyContainerResult:
    heading: str
    rows: tuple[Row, ...]
    warning: str = ""
    available: bool = True
    has_more: bool = False


@dataclass(frozen=True, slots=True)
class SpotifyRootResult:
    heading: str
    rows: tuple[Row, ...]


@dataclass(frozen=True, slots=True)
class SpotifyAccountSummary:
    label: str
    connected: bool


def _text(value: object) -> str:
    return value.strip() if isinstance(value, str) else ""


def _ticks(value: object) -> int:
    return max(0, value) if type(value) is int else 0


def _kind(value: object) -> str | None:
    candidate = _text(value).casefold()
    return candidate if candidate in _KNOWN_KINDS else None


def _row(entry: object) -> Row | None:
    if not isinstance(entry, dict):
        return None
    item_id = _text(entry.get("id"))
    service_id = _text(entry.get("externalId"))
    title = _text(entry.get("title"))
    kind = _kind(entry.get("kind"))
    if not item_id or not service_id or not title or kind is None:
        return None

    artist = _text(entry.get("artist"))
    duration = _ticks(entry.get("durationTicks"))
    details: list[str] = []
    if artist and artist.casefold() != title.casefold():
        details.append(artist)
    if duration:
        details.append(format_duration(duration / 10_000_000))

    # LiteHost nazywa pole zgodnie ze wspólnym modelem ``isAvailable``.
    # ``isPlayable`` przyjmujemy także, aby czysty model mógł obsłużyć
    # późniejszy adapter odtwarzacza bez zmiany kontraktu wiersza.
    playable = (
        entry.get("isAvailable") is not False
        and entry.get("isPlayable") is not False
    )
    activation_message = None
    if kind in _PLAYABLE_KINDS and not playable:
        activation_message = "Ten materiał nie jest obecnie dostępny do odtworzenia"

    related_album_service_id = _text(entry.get("relatedAlbumExternalId")) or None
    related_album_title = _text(entry.get("relatedAlbumTitle"))
    related_podcast_service_id = (
        _text(entry.get("relatedPodcastExternalId")) or None
    )
    related_podcast_title = _text(entry.get("relatedPodcastTitle"))
    # Istniejący wspólny ``MediaItem`` przechowuje rodzica odcinka w polach
    # RelatedAlbum*. LiteHost celowo nie wystawia typu technicznego, więc na
    # granicy wx zamieniamy te pola na użytkową relację „podcast”.
    if kind == "episode":
        related_podcast_service_id = (
            related_podcast_service_id or related_album_service_id
        )
        related_podcast_title = related_podcast_title or related_album_title
        related_album_service_id = None
        related_album_title = ""

    return Row(
        item_id=item_id,
        title=title,
        kind=kind,
        show_kind=kind != "track",
        url=_text(entry.get("publicUri")) or None,
        detail=", ".join(details),
        activation_message=activation_message,
        service_id=service_id,
        service_kind=kind,
        artist_name=artist,
        related_album_service_id=related_album_service_id,
        related_album_title=related_album_title,
        related_artist_service_id=(
            _text(entry.get("relatedArtistExternalId")) or None
        ),
        related_artist_name=_text(entry.get("relatedArtistName")),
        related_podcast_service_id=related_podcast_service_id,
        related_podcast_title=related_podcast_title,
        collection_added_ticks=(
            _ticks(entry.get("collectionAddedUtcTicks")) or None
        ),
        duration_ticks=duration,
    )


def normalize_history_payload(raw: object) -> list[dict]:
    """Keep only safe, playable Spotify rows in newest-first order.

    This is AMC's own playback history, not a claim about the complete
    Spotify-account history. Opaque identifiers stay as model data and are
    never used as accessible labels.
    """

    if not isinstance(raw, list):
        return []
    result: list[dict] = []
    seen: set[str] = set()
    for raw_item in raw:
        if len(result) >= _MAX_HISTORY_ITEMS or not isinstance(raw_item, dict):
            continue
        item = {
            name: _text(raw_item.get(name))
            for name in _HISTORY_TEXT_FIELDS
        }
        if (
            not item["itemId"]
            or not item["externalId"]
            or not item["title"]
            or item["kind"] not in _PLAYABLE_KINDS
        ):
            continue
        key = item["externalId"]
        if key in seen:
            continue
        seen.add(key)
        item["durationTicks"] = _ticks(raw_item.get("durationTicks"))
        result.append(item)
    return result


def history_rows(raw: object) -> tuple[Row, ...]:
    """Turn private history data back into deliberate, accessible rows."""

    rows: list[Row] = []
    for item in normalize_history_payload(raw):
        row = _row({
            "id": item["itemId"],
            "externalId": item["externalId"],
            "title": item["title"],
            "kind": item["kind"],
            "artist": item["artist"],
            "publicUri": item["publicUri"],
            "durationTicks": item["durationTicks"],
            "relatedAlbumExternalId": item["relatedAlbumExternalId"],
            "relatedAlbumTitle": item["relatedAlbumTitle"],
            "relatedArtistExternalId": item["relatedArtistExternalId"],
            "relatedArtistName": item["relatedArtistName"],
            "relatedPodcastExternalId": item["relatedPodcastExternalId"],
            "relatedPodcastTitle": item["relatedPodcastTitle"],
        })
        if row is not None:
            rows.append(row)
    return tuple(rows)


def record_history_row(raw: object, row: Row) -> list[dict]:
    """Move one confirmed Spotify playback to the front of AMC history."""

    current = normalize_history_payload(raw)
    if (
        row.kind not in _PLAYABLE_KINDS
        or not row.item_id
        or not row.service_id
        or not row.title
    ):
        return current
    entry = {
        "itemId": row.item_id,
        "externalId": row.service_id,
        "title": row.title,
        "kind": row.kind,
        "artist": row.artist_name,
        "publicUri": row.url or "",
        "relatedAlbumExternalId": row.related_album_service_id or "",
        "relatedAlbumTitle": row.related_album_title,
        "relatedArtistExternalId": row.related_artist_service_id or "",
        "relatedArtistName": row.related_artist_name,
        "relatedPodcastExternalId": row.related_podcast_service_id or "",
        "relatedPodcastTitle": row.related_podcast_title,
        "durationTicks": max(0, row.duration_ticks),
    }
    return normalize_history_payload([
        entry,
        *(item for item in current if item["externalId"] != row.service_id),
    ])


def queue_payload(rows: Sequence[Row]) -> list[dict]:
    """Persist only playable rows, preserving the user-selected order."""
    entries: list[dict] = []
    for row in rows:
        item = record_history_row([], row)
        if item:
            entries.append(item[0])
    return normalize_history_payload(entries)


def container_result_from_payload(payload: object) -> SpotifyContainerResult:
    """Sanitize a controller answer before any value can reach wx or NVDA.

    The accepted payload is intentionally small::

        {"heading": str, "items": [{
            "id": str, "externalId": str, "title": str,
            "kind": "artist|album|playlist|podcast|track|episode",
            "artist": str?, "durationTicks": int?, "publicUri": str?,
            "isPlayable": bool?,
            "relatedAlbumExternalId": str?, "relatedAlbumTitle": str?,
            "relatedArtistExternalId": str?, "relatedArtistName": str?,
            "relatedPodcastExternalId": str?, "relatedPodcastTitle": str?
        }]}

    Unknown fields are ignored. IDs and ``publicUri`` stay private model data.
    """

    if not isinstance(payload, dict):
        raise SpotifyContainerPayloadError(
            "Katalog Spotify zwrócił nieprawidłową odpowiedź"
        )
    heading = _text(payload.get("heading"))
    raw_items = payload.get("items")
    if not heading or not isinstance(raw_items, list):
        raise SpotifyContainerPayloadError(
            "Katalog Spotify zwrócił niepełną odpowiedź"
        )

    rows: list[Row] = []
    seen: set[str] = set()
    for entry in raw_items:
        row = _row(entry)
        if row is None or row.item_id in seen:
            continue
        seen.add(row.item_id)
        rows.append(row)
    warning = _text(payload.get("warning"))
    available = payload.get("available") is not False
    has_more = payload.get("hasMore") is True
    return SpotifyContainerResult(
        heading,
        tuple(rows),
        warning=warning,
        available=available,
        has_more=has_more,
    )


def root_result_from_payload(payload: object) -> SpotifyRootResult:
    """Build the six intentional collection rows from the host root.

    The technical view key stays in model-only fields.  Only the explicit
    host label is ever placed in a native list cell or spoken by NVDA.
    """

    if not isinstance(payload, dict):
        raise SpotifyContainerPayloadError(
            "Biblioteka Spotify zwróciła nieprawidłową odpowiedź"
        )
    heading = _text(payload.get("heading"))
    raw_views = payload.get("views")
    if not heading or not isinstance(raw_views, list):
        raise SpotifyContainerPayloadError(
            "Biblioteka Spotify zwróciła niepełną odpowiedź"
        )

    rows: list[Row] = []
    seen: set[str] = set()
    for entry in raw_views:
        if not isinstance(entry, dict):
            continue
        view = _text(entry.get("view")).casefold()
        label = _text(entry.get("label"))
        if view not in _COLLECTION_VIEWS or not label or view in seen:
            continue
        seen.add(view)
        rows.append(Row(
            item_id=f"spotify-collection:{view}",
            title=label,
            kind="folder",
            service_id=view,
            service_kind="spotifyCollection",
        ))
    return SpotifyRootResult(heading, tuple(rows))


def account_summary_from_payload(payload: object) -> SpotifyAccountSummary:
    """Return only the deliberate catalogue status label from account data."""

    if not isinstance(payload, dict) or not isinstance(payload.get("catalog"), dict):
        return SpotifyAccountSummary("", False)
    catalog = payload["catalog"]
    label = _text(catalog.get("label"))
    connected = catalog.get("connected") is True
    return SpotifyAccountSummary(label, connected)


def artist_overview_rows(artist: Row) -> tuple[Row, ...]:
    """Two explicit destinations below a Spotify artist."""

    if artist.kind != "artist" or not artist.service_id:
        return ()
    return tuple(
        Row(
            item_id=f"spotify-section:{artist.item_id}:{section}",
            title=label,
            kind="folder",
            service_id=artist.service_id,
            service_kind="artist",
            artist_name=artist.title,
            service_section=section,
        )
        for section, label in (("albums", "Albumy"), ("tracks", "Utwory"))
    )


def container_duration_ticks(rows: tuple[Row, ...] | list[Row]) -> int:
    """Sum real playable durations returned for one Spotify container."""

    return sum(
        max(0, row.duration_ticks)
        for row in rows
        if row.kind in _PLAYABLE_KINDS
    )


def information_text(
    row: Row,
    *,
    duration_ticks: int | None = None,
    fields: tuple[str, ...] | None = None,
) -> str:
    """One deliberate Left-Arrow announcement without technical values."""

    # Nazwę, rodzaj i wykonawcę właśnie przeczytała natywna lista. Lewa
    # strzałka podaje tylko uzupełnienie, przede wszystkim czas. Nie dodajemy
    # nazwy usługi, nazw pól ani danych relacji, bo wydłużały wypowiedź bez
    # nowej informacji. Brakującego czasu nie pobieramy podczas chodzenia
    # strzałkami góra/dół; album może go policzyć dopiero na jawne Left.
    values: list[str] = []
    effective_duration = (
        row.duration_ticks if duration_ticks is None else max(0, duration_ticks)
    )
    selected = set(fields) if fields is not None else {"duration", "other"}
    has_duration = bool(effective_duration and "duration" in selected)
    if has_duration:
        values.append(format_duration(effective_duration / 10_000_000))
        if row.kind == "track":
            values.append(row.title)
    if row.artist_name and "artist" in selected and not (
        row.kind == "track" and has_duration
    ):
        values.append(row.artist_name)
    if row.activation_message and "other" in selected:
        values.append(row.activation_message)

    result: list[str] = []
    seen: set[str] = set()
    for value in values:
        key = value.casefold()
        if key not in seen:
            seen.add(key)
            result.append(value)
    return ", ".join(result)


def description_from_payload(
    payload: object,
    row: Row,
) -> PodcastDescription | None:
    """Build the native Alt+D document without exposing catalogue IDs."""

    if row.kind not in ("podcast", "episode") or not isinstance(payload, dict):
        return None
    raw = payload.get("description")
    if not isinstance(raw, str):
        return None
    safe = "".join(
        character
        for character in raw
        if ord(character) >= 32 or character in "\r\n\t"
    )
    description = "\n".join(
        line.strip()
        for line in safe.replace("\r\n", "\n").replace("\r", "\n").split("\n")
    ).strip()
    if not description:
        return None
    # This text becomes the accessible name announced when the native Alt+D
    # dialog opens.  Keep the complete, already sanitised description: a
    # 500-character preview made NVDA stop before later paragraphs.
    normalized = " ".join(description.split())
    return PodcastDescription(
        window_title=(
            "Opis podcastu Spotify"
            if row.kind == "podcast"
            else "Opis odcinka Spotify"
        ),
        text=description,
        initial_focus_name=normalized,
    )
