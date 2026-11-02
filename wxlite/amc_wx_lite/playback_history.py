"""Prywatna historia odtwarzania streamerów sterowanych przez AMC Python.

WiiM i Sonos nie mają wspólnego z lokalnymi plikami katalogu, z którego można
by po fakcie odbudować wiersz Historii. Przechowujemy więc małą, oczyszczoną
migawkę tego, co AMC zobaczył w stanie grającego urządzenia. Identyfikatory
zostają wyłącznie w modelu; tekst listy powstaje z jawnych pól użytkowych.
"""

from __future__ import annotations

import hashlib
from dataclasses import replace

from .list_model import Row
from .radio_activity import join_spoken_prefix, normalize_state_position


MAX_ITEMS_PER_SESSION = 500
SUPPORTED_SESSIONS = frozenset({
    "wiim", "sonos", "frontierSmart", "tidal", "radioarchiwista",
})

_KINDS = frozenset({
    "track",
    "station",
    "video",
    "episode",
    "recording",
    "album",
    "playlist",
    "wiimNetworkStream",
    "sonosFavorite",
    "sonosPlaylist",
    "sonosOwnStream",
})

_TEXT_FIELDS = (
    "itemId",
    "title",
    "kind",
    "artist",
    "album",
    "source",
    "publicUri",
    "externalId",
    "serviceKind",
    "parentId",
    "relatedAlbumExternalId",
    "relatedAlbumTitle",
    "relatedArtistExternalId",
    "relatedArtistName",
)


def _text(value: object) -> str:
    return value.strip() if isinstance(value, str) else ""


def _number(value: object) -> float:
    if isinstance(value, (int, float)) and not isinstance(value, bool):
        return max(0.0, float(value))
    return 0.0


def _identity(item: dict) -> str:
    return "\x1f".join((
        item["kind"],
        item["externalId"],
        item["serviceKind"],
        item["parentId"],
        item["publicUri"],
        item["title"],
        item["artist"],
        item["album"],
        item["source"],
    )).casefold()


def _observed_id(session_id: str, *parts: str) -> str:
    identity = "\x1f".join(part.strip() for part in parts)
    digest = hashlib.sha256(identity.encode("utf-8")).hexdigest()[:32]
    return f"{session_id}:observed:{digest}"


def normalize_session_history(raw: object) -> list[dict]:
    """Odetnij obce pola, duplikaty i wpisy bez użytkowej nazwy."""

    if not isinstance(raw, list):
        return []
    result: list[dict] = []
    seen: set[str] = set()
    for raw_item in raw:
        if len(result) >= MAX_ITEMS_PER_SESSION or not isinstance(raw_item, dict):
            continue
        item = {name: _text(raw_item.get(name)) for name in _TEXT_FIELDS}
        if (
            not item["itemId"]
            or not item["title"]
            or item["kind"] not in _KINDS
        ):
            continue
        identity = _identity(item)
        if not identity or identity in seen:
            continue
        seen.add(identity)
        item["durationSeconds"] = _number(raw_item.get("durationSeconds"))
        item["replaySupported"] = raw_item.get("replaySupported") is True
        result.append(item)
    return result


def normalize_histories(raw: object) -> dict[str, list[dict]]:
    """Wczytaj wyłącznie znane sesje i ich bezpieczne migawki."""

    if not isinstance(raw, dict):
        return {}
    result: dict[str, list[dict]] = {}
    for session_id in SUPPORTED_SESSIONS:
        items = normalize_session_history(raw.get(session_id))
        if items:
            result[session_id] = items
    return result


def _detail(item: dict) -> str:
    parts: list[str] = []
    for value in (item["artist"], item["album"], item["source"]):
        text = value.strip()
        if (
            text
            and text.casefold() != item["title"].casefold()
            and all(existing.casefold() != text.casefold() for existing in parts)
        ):
            parts.append(text)
    return ", ".join(parts)


def history_rows(session_id: str, raw: object) -> tuple[Row, ...]:
    """Zbuduj celowe etykiety listy; nigdy repr, adres ani identyfikator."""

    if session_id not in SUPPORTED_SESSIONS:
        return ()
    unavailable = {
        "wiim": (
            "Ten wpis pokazuje materiał zaobserwowany przez WiiM. "
            "Urządzenie nie udostępniło danych potrzebnych do ponownego "
            "uruchomienia go z Historii"
        ),
        "sonos": (
            "Ten wpis pokazuje materiał zaobserwowany przez Sonos. "
            "Sonos nie udostępnił danych potrzebnych do ponownego "
            "uruchomienia go z Historii"
        ),
        "frontierSmart": (
            "Ten wpis pokazuje materiał zaobserwowany przez Frontier Smart. "
            "Urządzenie nie udostępniło celu potrzebnego do ponownego "
            "uruchomienia go z Historii"
        ),
        "tidal": (
            "Ten wpis nie zawiera danych potrzebnych do ponownego "
            "uruchomienia go w TIDALu"
        ),
        "radioarchiwista": (
            "To nagranie nie ma już adresu potrzebnego do ponownego odtworzenia"
        ),
    }[session_id]
    return tuple(
        Row(
            item_id=item["itemId"],
            title=item["title"],
            kind=item["kind"],
            url=item["publicUri"] or None,
            detail=_detail(item),
            activation_message=(
                None if item["replaySupported"] else unavailable
            ),
            parent_id=item["parentId"] or None,
            service_id=item["externalId"] or None,
            service_kind=item["serviceKind"] or None,
            artist_name=item["artist"],
            related_album_service_id=(
                item["relatedAlbumExternalId"] or None
            ),
            related_album_title=item["relatedAlbumTitle"],
            related_artist_service_id=(
                item["relatedArtistExternalId"] or None
            ),
            related_artist_name=item["relatedArtistName"],
            duration_ticks=int(item["durationSeconds"] * 10_000_000),
        )
        for item in normalize_session_history(raw)
    )


def record_row(raw: object, row: Row, *, replay_supported: bool) -> list[dict]:
    """Przenieś potwierdzone odtworzenie na początek historii sesji."""

    current = normalize_session_history(raw)
    if not row.item_id or not row.title or row.kind not in _KINDS:
        return current
    entry = {
        "itemId": row.item_id,
        "title": row.title,
        "kind": row.kind,
        "artist": row.artist_name,
        "album": row.related_album_title,
        "source": row.detail,
        "publicUri": row.url or "",
        "externalId": row.service_id or "",
        "serviceKind": row.service_kind or "",
        "parentId": row.parent_id or "",
        "relatedAlbumExternalId": row.related_album_service_id or "",
        "relatedAlbumTitle": row.related_album_title,
        "relatedArtistExternalId": row.related_artist_service_id or "",
        "relatedArtistName": row.related_artist_name,
        "durationSeconds": max(0.0, row.duration_ticks / 10_000_000),
        "replaySupported": replay_supported,
    }
    identity = _identity(entry)
    return normalize_session_history([
        entry,
        *(item for item in current if _identity(item) != identity),
    ])


def activity_rows(
    rows: list[Row] | tuple[Row, ...],
    *,
    current_item_id: str,
    playback_state: object,
    position: object = "after",
) -> list[Row]:
    """Nałóż wspólną, krótką informację o odtwarzaniu na listę streamera."""

    state = _text(playback_state).casefold()
    label = (
        "odtwarzane"
        if state in {"playing", "play", "buffering", "odtwarzanie"}
        else "odtwarzanie wstrzymane"
        if state in {"paused", "pause", "wstrzymane"}
        else ""
    )
    placement = normalize_state_position(position)
    result: list[Row] = []
    for row in rows:
        active = bool(label and current_item_id and row.item_id == current_item_id)
        prefix = (
            join_spoken_prefix([label])
            if active and placement == "before"
            else ""
        )
        detail = label if active and placement == "after" else ""
        if placement == "off":
            prefix = ""
            detail = ""
        result.append(
            row
            if (
                row.state_prefix == prefix
                and row.state_detail == detail
                and row.playback_activity == active
            )
            else replace(
                row,
                state_prefix=prefix,
                state_detail=detail,
                playback_activity=active,
            )
        )
    return result


def observed_wiim_row(state: dict) -> Row | None:
    """Migawka grającego WiiM; zwykły odczyt stanu nie oznacza startu."""

    playback = _text(state.get("playbackState")).casefold()
    if playback not in {"odtwarzanie", "playing", "play"}:
        return None
    title = _text(state.get("title")) or _text(state.get("subtitle"))
    if not title:
        return None
    artist = _text(state.get("artist"))
    album = _text(state.get("album"))
    source = _text(state.get("source"))
    duration = _number(state.get("durationSeconds"))
    return Row(
        item_id=_observed_id("wiim", title, artist, album, source),
        title=title,
        kind="track" if duration > 0 else "station",
        detail=source,
        artist_name=artist,
        related_album_title=album,
        duration_ticks=int(duration * 10_000_000),
    )


def observed_sonos_row(state: dict) -> Row | None:
    """Migawka grającego Sonosa bez udawania, że API zwróciło URI replay."""

    playback = _text(state.get("playbackState")).casefold()
    if playback not in {"playing", "play", "buffering", "odtwarzanie"}:
        return None
    title = _text(state.get("title"))
    if not title or title.casefold() == "brak informacji":
        return None
    source = _text(state.get("source"))
    duration = _number(state.get("durationSeconds"))
    return Row(
        item_id=_observed_id("sonos", title, source),
        title=title,
        kind="track" if duration > 0 else "station",
        detail=source,
        duration_ticks=int(duration * 10_000_000),
    )


def observed_frontier_smart_row(state: dict) -> Row | None:
    """Zapisz tylko materiał zgłoszony przez grające urządzenie."""

    if _text(state.get("playbackState")).casefold() != "odtwarzanie":
        return None
    title = _text(state.get("title")) or _text(state.get("subtitle"))
    if not title:
        return None
    artist = _text(state.get("artist"))
    album = _text(state.get("album"))
    source = _text(state.get("deviceTitle"))
    duration = _number(state.get("durationSeconds"))
    return Row(
        item_id=_observed_id("frontierSmart", title, artist, album, source),
        title=title,
        kind="track" if duration > 0 else "station",
        detail=source,
        artist_name=artist,
        related_album_title=album,
        duration_ticks=int(duration * 10_000_000),
    )


def remove_rows(raw: object, item_ids: set[str]) -> list[dict]:
    """Usuń wyłącznie wpisy Historii, nie materiały źródłowe."""

    return [
        item for item in normalize_session_history(raw)
        if item["itemId"] not in item_ids
    ]
