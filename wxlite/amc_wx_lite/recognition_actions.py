"""Shared command model for actions on recognized radio tracks.

The module is intentionally independent from wx and network access.  It owns
the labels, matching rules, cache payload and action availability used by the
Right-Arrow list, the context menu and keyboard handlers.  Opaque catalogue
identifiers never become accessible labels.
"""

from __future__ import annotations

import re
import unicodedata
from dataclasses import dataclass
from urllib.parse import quote, quote_plus, urlparse

from .list_model import Row


SPOTIFY = "spotify"
TIDAL = "tidal"
APPLE_MUSIC = "appleMusic"
DEFAULT_SERVICES = (SPOTIFY, TIDAL)


def normalize_default_service(value: object) -> str:
    return value if isinstance(value, str) and value in DEFAULT_SERVICES else SPOTIFY


def service_label(service: str) -> str:
    return {SPOTIFY: "Spotify", TIDAL: "TIDAL", APPLE_MUSIC: "Apple Music"}.get(
        service, "Usługa"
    )


def _text(value: object) -> str:
    return value.strip() if isinstance(value, str) else ""


def _recognized_subject(entry: dict) -> str:
    title = _text(entry.get("title"))
    artist = _text(entry.get("artist"))
    return ", ".join(value for value in (title, artist) if value) or "Rozpoznany utwór"


def _safe_http_url(value: object, *, service: str | None = None) -> str:
    url = _text(value)
    if not url:
        return ""
    try:
        parsed = urlparse(url)
    except ValueError:
        return ""
    if parsed.scheme.casefold() not in ("http", "https") or not parsed.netloc:
        return ""
    host = (parsed.hostname or "").casefold()
    allowed = {
        SPOTIFY: ("spotify.com",),
        TIDAL: ("tidal.com",),
        APPLE_MUSIC: ("apple.com",),
    }.get(service)
    if allowed and not any(host == suffix or host.endswith("." + suffix) for suffix in allowed):
        return ""
    return url


def search_links(entry: dict) -> dict[str, str]:
    query_text = " ".join(
        value for value in (_text(entry.get("artist")), _text(entry.get("title"))) if value
    )
    query = quote_plus(query_text)
    path_query = quote(query_text, safe="")
    return {
        "Apple Music": f"https://music.apple.com/search?term={query}",
        "Spotify": f"https://open.spotify.com/search/{path_query}",
        "TIDAL": f"https://listen.tidal.com/search?q={query}",
        "YouTube Music": f"https://music.youtube.com/search?q={query}",
        "Discogs": f"https://www.discogs.com/search/?q={query}&type=all",
        "MusicBrainz": (
            f"https://musicbrainz.org/search?query={query}&type=recording&method=indexed"
        ),
    }


def _normalized_words(value: object) -> tuple[str, ...]:
    text = unicodedata.normalize("NFKD", _text(value).casefold())
    plain = "".join(character for character in text if not unicodedata.combining(character))
    return tuple(re.findall(r"[a-z0-9]+", plain))


def _similarity(left: object, right: object) -> float:
    left_words = _normalized_words(left)
    right_words = _normalized_words(right)
    if not left_words or not right_words:
        return 0.0
    if left_words == right_words:
        return 1.0
    left_joined = " ".join(left_words)
    right_joined = " ".join(right_words)
    if min(len(left_joined), len(right_joined)) >= 5 and (
        left_joined in right_joined or right_joined in left_joined
    ):
        return 0.90
    left_set = set(left_words)
    right_set = set(right_words)
    return 2.0 * len(left_set & right_set) / (len(left_set) + len(right_set))


def select_catalog_match(entry: dict, candidates: list[dict]) -> dict | None:
    """Select a conservative title/artist match from sanitized candidates."""

    wanted_title = _text(entry.get("title"))
    wanted_artist = _text(entry.get("artist"))
    wanted_album = _text(entry.get("album"))
    best: tuple[float, dict] | None = None
    ambiguous = False
    for candidate in candidates:
        if not isinstance(candidate, dict):
            continue
        title_score = _similarity(wanted_title, candidate.get("title"))
        artist_score = _similarity(wanted_artist, candidate.get("artist"))
        if title_score < 0.72 or (wanted_artist and artist_score < 0.50):
            continue
        album_score = _similarity(wanted_album, candidate.get("album")) if wanted_album else 0.0
        # A known album distinguishes editions with the same title and artist.
        # A different edition must not be announced as an exact track link.
        if wanted_album and album_score < 0.72:
            continue
        score = title_score * 60.0 + artist_score * 35.0 + album_score * 5.0
        minimum = 82.0 if wanted_artist else 55.0
        if score < minimum:
            continue
        if best is None or score > best[0] + 0.01:
            best = score, candidate
            ambiguous = False
        elif best is not None and abs(score - best[0]) <= 0.01 and (
            _text(candidate.get("serviceId")) != _text(best[1].get("serviceId"))
        ):
            ambiguous = True
    return best[1] if best is not None and not ambiguous else None


def _service_identifier(value: object, kind: str) -> str:
    identifier = _text(value)
    prefix = kind + ":"
    return identifier[len(prefix):] if identifier.casefold().startswith(prefix.casefold()) else identifier


def match_from_row(service: str, row: Row) -> dict:
    """Create the persistent, sanitized cache record for one catalogue row."""

    service = normalize_default_service(service)
    track_id = _service_identifier(row.service_id, "track")
    album_id = _service_identifier(row.related_album_service_id, "album")
    artist_id = _service_identifier(row.related_artist_service_id, "artist")
    public_url = _safe_http_url(row.url, service=service)
    album_url = ""
    artist_url = ""
    app_uri = ""
    if service == SPOTIFY:
        public_url = public_url or (
            f"https://open.spotify.com/track/{quote(track_id, safe='')}" if track_id else ""
        )
        album_url = (
            f"https://open.spotify.com/album/{quote(album_id, safe='')}" if album_id else ""
        )
        artist_url = (
            f"https://open.spotify.com/artist/{quote(artist_id, safe='')}" if artist_id else ""
        )
        app_uri = f"spotify:track:{track_id}" if track_id else ""
    elif service == TIDAL:
        public_url = public_url or (
            f"https://tidal.com/browse/track/{quote(track_id, safe='')}" if track_id else ""
        )
        album_url = (
            f"https://tidal.com/browse/album/{quote(album_id, safe='')}" if album_id else ""
        )
        artist_url = (
            f"https://tidal.com/browse/artist/{quote(artist_id, safe='')}" if artist_id else ""
        )
    return sanitize_match({
        "service": service,
        "itemId": row.item_id,
        "serviceId": row.service_id or "",
        "title": row.title,
        "artist": row.artist_name,
        "albumServiceId": row.related_album_service_id or "",
        "albumTitle": row.related_album_title,
        "artistServiceId": row.related_artist_service_id or "",
        "artistName": row.related_artist_name,
        "publicUrl": public_url,
        "albumUrl": album_url,
        "artistUrl": artist_url,
        "appUri": app_uri,
    }, expected_service=service) or {}


def sanitize_match(raw: object, *, expected_service: str | None = None) -> dict | None:
    if not isinstance(raw, dict):
        return None
    service = _text(raw.get("service"))
    if service not in (SPOTIFY, TIDAL, APPLE_MUSIC):
        return None
    if expected_service is not None and service != expected_service:
        return None
    title = _text(raw.get("title"))
    artist = _text(raw.get("artist"))
    public_url = _safe_http_url(raw.get("publicUrl"), service=service)
    if not title or not public_url:
        return None
    app_uri = _text(raw.get("appUri"))
    if service != SPOTIFY or not app_uri.casefold().startswith("spotify:track:"):
        app_uri = ""
    return {
        "service": service,
        "itemId": _text(raw.get("itemId")),
        "serviceId": _text(raw.get("serviceId")),
        "title": title,
        "artist": artist,
        "albumServiceId": _text(raw.get("albumServiceId")),
        "albumTitle": _text(raw.get("albumTitle")),
        "artistServiceId": _text(raw.get("artistServiceId")),
        "artistName": _text(raw.get("artistName")),
        "publicUrl": public_url,
        "albumUrl": _safe_http_url(raw.get("albumUrl"), service=service),
        "artistUrl": _safe_http_url(raw.get("artistUrl"), service=service),
        "appUri": app_uri,
    }


def cached_matches(entry: dict) -> dict[str, dict]:
    raw = entry.get("serviceMatches")
    if not isinstance(raw, dict):
        return {}
    result: dict[str, dict] = {}
    for service in (SPOTIFY, TIDAL, APPLE_MUSIC):
        match = sanitize_match(raw.get(service), expected_service=service)
        if match is not None and (
            service == APPLE_MUSIC or select_catalog_match(
                entry, [{**match, "album": match["albumTitle"]}]
            ) is not None
        ):
            result[service] = match
    return result


def store_match(entry: dict, match: dict) -> bool:
    clean = sanitize_match(match)
    if clean is None:
        return False
    matches = cached_matches(entry)
    changed = matches.get(clean["service"]) != clean
    matches[clean["service"]] = clean
    entry["serviceMatches"] = matches
    return changed


def row_from_match(match: dict) -> Row | None:
    clean = sanitize_match(match)
    if clean is None or clean["service"] not in DEFAULT_SERVICES:
        return None
    service = clean["service"]
    item_id = clean["itemId"] or f"recognition:{service}:track:{clean['serviceId']}"
    return Row(
        item_id=item_id,
        title=clean["title"],
        kind="track",
        url=clean["publicUrl"],
        service_id=clean["serviceId"],
        service_kind="track",
        artist_name=clean["artist"],
        related_album_service_id=clean["albumServiceId"] or None,
        related_album_title=clean["albumTitle"],
        related_artist_service_id=clean["artistServiceId"] or None,
        related_artist_name=clean["artistName"],
    )


def membership_row(action: RecognitionAction, match: dict | None, *, favorite: bool) -> Row | None:
    """Use a verified catalogue entity for collection commands, never a search URL."""
    clean = sanitize_match(match, expected_service=action.service) if match else None
    if clean is None or clean["service"] not in DEFAULT_SERVICES:
        return None
    if favorite:
        return row_from_match(clean) if action.entity == "track" else None
    if action.entity == "artist":
        service_id, title, url, kind = (
            clean["artistServiceId"], clean["artistName"] or clean["artist"],
            clean["artistUrl"], "artist",
        )
    elif action.entity in ("track", "album"):
        service_id, title, url, kind = (
            clean["albumServiceId"], clean["albumTitle"], clean["albumUrl"], "album",
        )
    else:
        return None
    if not service_id or not title:
        return None
    return Row(
        item_id=f"recognition:{action.service}:{kind}:{service_id}",
        title=title, kind=kind, url=url or None,
        service_id=service_id, service_kind=kind,
        artist_name=clean["artist"],
    )


@dataclass(frozen=True, slots=True)
class RecognitionAction:
    action_id: str
    label: str
    operation: str
    service: str = ""
    entity: str = ""
    url: str = ""
    quick: bool = False

    @property
    def needs_match(self) -> bool:
        return self.operation != "open_url" and self.service in (SPOTIFY, TIDAL, APPLE_MUSIC) and self.entity in (
            "track", "album", "artist"
        )


def all_actions(
    entry: dict,
    default_service: object,
    *,
    internal_services: tuple[str, ...] = DEFAULT_SERVICES,
) -> tuple[RecognitionAction, ...]:
    default = normalize_default_service(default_service)
    internal = frozenset(internal_services).intersection(DEFAULT_SERVICES)
    artist = _text(entry.get("artist"))
    album = _text(entry.get("album"))
    subject = _recognized_subject(entry)
    links = search_links(entry)
    actions: list[RecognitionAction] = []
    if artist and default in internal:
        actions.append(RecognitionAction(
            "default.artist", f"{artist}, wykonawca — znajdź w {service_label(default)}",
            "show_amc", default, "artist", quick=True
        ))
    if album and default in internal:
        actions.append(RecognitionAction(
            "default.album", (
                f"{album}, album" + (f", {artist}" if artist else "")
                + f" — znajdź w {service_label(default)}"
            ),
            "show_amc", default, "album", quick=True
        ))
    if SPOTIFY in internal:
        actions.extend((RecognitionAction(
            "spotify.amc.track", f"{subject}, utwór — znajdź w Spotify",
            "show_amc",
            SPOTIFY, "track", quick=True,
        ),
        RecognitionAction(
            "spotify.external.track", "Spotify — otwórz w oryginalnej aplikacji",
            "open_external", SPOTIFY, "track",
        )))
    else:
        actions.append(RecognitionAction(
            "spotify.search", f"{subject}, utwór — wyszukaj na stronie Spotify", "open_url",
            SPOTIFY, "track", url=links["Spotify"], quick=True,
        ))
    if TIDAL in internal:
        actions.extend((RecognitionAction(
            "tidal.amc.track", f"{subject}, utwór — znajdź w TIDAL", "show_amc",
            TIDAL, "track", quick=True,
        ),
        RecognitionAction(
            "tidal.external.track", "TIDAL — otwórz w oryginalnej aplikacji",
            "open_external", TIDAL, "track",
        )))
    else:
        actions.append(RecognitionAction(
            "tidal.search", f"{subject}, utwór — wyszukaj na stronie TIDAL", "open_url",
            TIDAL, "track", url=links["TIDAL"], quick=True,
        ))
    actions.append(RecognitionAction(
        "apple.copy.track", f"{subject}, utwór — kopiuj link Apple Music",
        "copy_link", APPLE_MUSIC, "track", quick=True,
    ))
    actions.append(
        RecognitionAction(
            "apple.external.track", "Apple Music — otwórz utwór", "open_external",
            APPLE_MUSIC, "track",
        )
    )
    if SPOTIFY in internal:
        actions.extend((RecognitionAction(
            "spotify.amc.album", "Spotify — pokaż album w AMC", "show_amc",
            SPOTIFY, "album",
        ),
        RecognitionAction(
            "spotify.external.album", "Spotify — otwórz album", "open_external",
            SPOTIFY, "album",
        )))
    if TIDAL in internal:
        actions.extend((RecognitionAction(
            "tidal.amc.album", "TIDAL — pokaż album w AMC", "show_amc",
            TIDAL, "album",
        ),
        RecognitionAction(
            "tidal.external.album", "TIDAL — otwórz album", "open_external",
            TIDAL, "album",
        )))
    actions.extend((
        RecognitionAction(
            "apple.external.album", "Apple Music — otwórz album", "open_external",
            APPLE_MUSIC, "album",
        ),
        RecognitionAction(
            "apple.search", "Apple Music — wyszukaj w przeglądarce", "open_url",
            url=links["Apple Music"],
        ),
        RecognitionAction(
            "youtube.search", "YouTube Music — wyszukaj", "open_url",
            url=links["YouTube Music"],
        ),
        RecognitionAction(
            "discogs.search", "Discogs — wyszukaj", "open_url", url=links["Discogs"]
        ),
        RecognitionAction(
            "musicbrainz.search", "MusicBrainz — wyszukaj", "open_url",
            url=links["MusicBrainz"],
        ),
    ))
    provider = _safe_http_url(entry.get("providerUri"))
    if provider:
        actions.append(RecognitionAction(
            "provider.open", "Dostawca rozpoznawania — otwórz wynik", "open_url", url=provider
        ))
    return tuple(actions)


def quick_actions(
    entry: dict,
    default_service: object,
    *,
    internal_services: tuple[str, ...] = DEFAULT_SERVICES,
) -> tuple[RecognitionAction, ...]:
    actions = tuple(
        action
        for action in all_actions(
            entry,
            default_service,
            internal_services=internal_services,
        )
        if action.quick
    )
    default = normalize_default_service(default_service)
    other = TIDAL if default == SPOTIFY else SPOTIFY
    order = {
        default: 0,
        other: 1,
        APPLE_MUSIC: 2,
    }
    entity_order = {"track": 0, "album": 1, "artist": 2}
    return tuple(sorted(
        actions,
        key=lambda action: (
            0 if action.entity == "track" else 1,
            order.get(action.service, 3),
            entity_order.get(action.entity, 3),
        ),
    ))


def action_url(action: RecognitionAction, match: dict | None) -> str:
    if action.operation == "open_url":
        return _safe_http_url(action.url)
    clean = sanitize_match(match, expected_service=action.service) if match is not None else None
    if clean is None:
        return ""
    return {
        "track": clean["publicUrl"],
        "album": clean["albumUrl"],
        "artist": clean["artistUrl"],
    }.get(action.entity, "")


def quick_information(entry: dict) -> str:
    values = []
    if album := _text(entry.get("album")):
        values.append(f"Album {album}")
    if release := _text(entry.get("releaseDate")):
        values.append(f"Wydanie {release}")
    values.append(f"Stacja {_text(entry.get('stationName')) or 'nieznana'}")
    return ", ".join(values)
