"""Narrow, screen-reader-safe Apple Music resource contract.

The unofficial client returns Apple JSON dictionaries.  Those dictionaries
must never reach wx controls, logs, history or the problem reporter.  This
module copies only an allowlisted subset into AMC's existing :class:`Row`
model and keeps provider identifiers in a separate target object.

There is deliberately no network, browser, credential or optional dependency
import here.  The module can therefore be exercised before an Apple account is
connected and remains inert in AMC Public.
"""

from __future__ import annotations

from collections.abc import Sequence
from dataclasses import dataclass, field
import re
import unicodedata
from urllib.parse import urlsplit

from .list_model import Row
from .radio_recording import format_duration


_RESOURCE_KINDS: dict[str, tuple[str, bool]] = {
    "library-songs": ("track", True),
    "library-albums": ("album", False),
    "library-artists": ("artist", False),
    "library-playlists": ("playlist", False),
    "library-music-videos": ("video", True),
    "songs": ("track", True),
    "albums": ("album", False),
    "artists": ("artist", False),
    "playlists": ("playlist", False),
    "music-videos": ("video", True),
    "stations": ("station", True),
}
_IDENTIFIER = re.compile(r"[A-Za-z0-9._:-]{1,256}\Z")
_MAX_DURATION_MS = 7 * 24 * 60 * 60 * 1000


class AppleMusicPayloadError(RuntimeError):
    """A stable user-facing failure with no raw response details."""

    def __str__(self) -> str:
        return "Apple Music zwróciło nieprawidłową listę materiałów."


@dataclass(frozen=True, slots=True, repr=False)
class AppleMusicTarget:
    """Provider identity used by adapters, never by accessible controls."""

    resource_id: str = field(repr=False)
    resource_type: str = field(repr=False)
    catalog_id: str | None = field(default=None, repr=False)
    from_library: bool = field(default=False, repr=False)
    can_play: bool = field(default=False, repr=False)


@dataclass(frozen=True, slots=True, repr=False)
class AppleMusicRecord:
    """One deliberate presentation row paired with its private target."""

    row: Row = field(repr=False)
    target: AppleMusicTarget = field(repr=False)

    @property
    def accessible_label(self) -> str:
        """The only text representation suitable for a list or NVDA."""

        parts = [self.row.title, self.row.kind_label, self.row.detail]
        return ", ".join(dict.fromkeys(part for part in parts if part))

    def __str__(self) -> str:
        return self.accessible_label

    def __repr__(self) -> str:
        # A defensive fallback for accidental data binding: even repr contains
        # only the intentional user-facing text, never IDs or raw dictionaries.
        return self.accessible_label


def _display_text(value: object, *, maximum: int = 512) -> str:
    if not isinstance(value, str):
        return ""
    characters: list[str] = []
    for char in value:
        if char.isspace():
            characters.append(" ")
        elif not unicodedata.category(char).startswith("C"):
            characters.append(char)
    cleaned = "".join(characters)
    cleaned = " ".join(cleaned.split())
    if len(cleaned) <= maximum:
        return cleaned
    return cleaned[: maximum - 1].rstrip() + "…"


def _identifier(value: object) -> str:
    if not isinstance(value, str):
        return ""
    candidate = value.strip()
    return candidate if _IDENTIFIER.fullmatch(candidate) else ""


def _public_url(value: object) -> str | None:
    if not isinstance(value, str) or len(value) > 4096:
        return None
    candidate = value.strip()
    try:
        parsed = urlsplit(candidate)
    except ValueError:
        return None
    host = (parsed.hostname or "").casefold()
    if (
        parsed.scheme.casefold() != "https"
        or not host
        or host != "music.apple.com"
        or parsed.username is not None
        or parsed.password is not None
    ):
        return None
    return candidate


def _duration_milliseconds(value: object) -> int:
    if type(value) is not int or not 0 < value <= _MAX_DURATION_MS:
        return 0
    return value


def _catalog_id(
    entry: dict[object, object],
    attributes: dict[object, object],
    resource_type: str,
) -> str | None:
    if not resource_type.startswith("library-"):
        return _identifier(entry.get("id")) or None

    play_params = attributes.get("playParams")
    if isinstance(play_params, dict):
        candidate = _identifier(play_params.get("catalogId"))
        if candidate:
            return candidate

    relationships = entry.get("relationships")
    catalog = relationships.get("catalog") if isinstance(relationships, dict) else None
    data = catalog.get("data") if isinstance(catalog, dict) else None
    if isinstance(data, list) and data and isinstance(data[0], dict):
        return _identifier(data[0].get("id")) or None
    return None


def _record(entry: object) -> AppleMusicRecord | None:
    if not isinstance(entry, dict):
        return None
    resource_id = _identifier(entry.get("id"))
    resource_type = _display_text(entry.get("type"), maximum=64).casefold()
    kind_spec = _RESOURCE_KINDS.get(resource_type)
    attributes = entry.get("attributes")
    if not resource_id or kind_spec is None or not isinstance(attributes, dict):
        return None

    title = _display_text(attributes.get("name"))
    if not title:
        return None
    kind, potentially_playable = kind_spec
    artist = _display_text(attributes.get("artistName"))
    duration_ms = _duration_milliseconds(attributes.get("durationInMillis"))
    catalog_id = _catalog_id(entry, attributes, resource_type)
    can_play = potentially_playable and catalog_id is not None

    details: list[str] = []
    if artist and artist.casefold() != title.casefold():
        details.append(artist)
    if duration_ms:
        details.append(format_duration(duration_ms / 1000))

    row_id = f"appleMusic:{resource_type}:{resource_id}"
    row = Row(
        item_id=row_id,
        title=title,
        kind=kind,
        url=_public_url(attributes.get("url")),
        detail=", ".join(details),
        activation_message=(
            None
            if not potentially_playable or can_play
            else "Ten materiał nie ma identyfikatora odtwarzania Apple Music"
        ),
        service_id=resource_id,
        service_kind=kind,
        artist_name=artist,
        duration_ticks=duration_ms * 10_000,
    )
    target = AppleMusicTarget(
        resource_id=resource_id,
        resource_type=resource_type,
        catalog_id=catalog_id,
        from_library=resource_type.startswith("library-"),
        can_play=can_play,
    )
    return AppleMusicRecord(row=row, target=target)


def records_from_resources(raw: object) -> tuple[AppleMusicRecord, ...]:
    """Copy allowlisted Apple resources into stable AMC presentation records.

    Invalid individual resources are ignored.  A non-list top-level value is
    a contract failure because accepting a dictionary there could accidentally
    enumerate technical response keys as list items.
    """

    if (
        not isinstance(raw, Sequence)
        or isinstance(raw, (str, bytes, bytearray))
    ):
        raise AppleMusicPayloadError()

    result: list[AppleMusicRecord] = []
    seen: set[str] = set()
    for entry in raw:
        record = _record(entry)
        if record is None or record.row.item_id in seen:
            continue
        seen.add(record.row.item_id)
        result.append(record)
    return tuple(result)
