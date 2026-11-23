"""HEOS identity and user-facing text for AMC's device and catalog lists.

Keep IDs, source capabilities, and spoken labels separate.  Nothing in this
module needs wx or a network connection, so the same rules can be exercised
with device responses and with protocol fakes.
"""

from __future__ import annotations

import hashlib
from dataclasses import dataclass
from urllib.parse import parse_qs


def _text(value: object) -> str:
    return value.strip() if isinstance(value, str) else ""


def _id(value: object) -> str:
    if isinstance(value, bool) or not isinstance(value, (str, int)):
        return ""
    return str(value).strip()


def _yes(value: object) -> bool:
    return value is True or (isinstance(value, str) and value.strip().lower() in {"yes", "true"})


def _key(*parts: object) -> str:
    raw = "\0".join(str(part) for part in parts).encode("utf-8")
    return "heos:" + hashlib.sha256(raw).hexdigest()[:24]


@dataclass(frozen=True, slots=True)
class HeosTarget:
    item_id: str
    title: str
    player_id: str
    group_id: str = ""
    member_ids: tuple[str, ...] = ()

    @property
    def kind_label(self) -> str:
        return "grupa" if self.group_id else "urządzenie"


@dataclass(frozen=True, slots=True)
class HeosSource:
    item_id: str
    title: str
    source_id: str
    source_type: str


@dataclass(frozen=True, slots=True)
class HeosItem:
    item_id: str
    title: str
    kind_label: str
    source_id: str
    container_id: str
    media_id: str
    can_open: bool
    can_play: bool
    detail: str = ""
    child_source_id: str = ""


@dataclass(frozen=True, slots=True)
class HeosStatus:
    title: str
    source: str
    artist: str
    album: str
    state_label: str
    volume: int | None
    muted: bool | None
    media_type: str

    def spoken_label(self) -> str:
        parts = [self.title]
        for value in (self.source, self.artist, self.state_label):
            if value and value.casefold() not in {part.casefold() for part in parts}:
                parts.append(value)
        if self.muted is True:
            parts.append("wyciszone")
        elif self.volume is not None:
            parts.append(f"głośność {self.volume}")
        return ", ".join(parts)


def _message_value(response: object, key: str) -> str:
    if not isinstance(response, dict) or not isinstance(response.get("heos"), dict):
        return ""
    message = response["heos"].get("message")
    if not isinstance(message, str):
        return ""
    return _text(parse_qs(message).get(key, [""])[0])


def playback_status(play: object, media: object, volume: object, mute: object) -> HeosStatus:
    """Only intentional text reaches the player heading and NVDA status."""

    payload = media.get("payload") if isinstance(media, dict) else None
    details = payload if isinstance(payload, dict) else {}
    state = _message_value(play, "state")
    source = _text(details.get("station"))
    title = _text(details.get("song")) or source or "Nic nie jest odtwarzane"
    raw_volume = _message_value(volume, "level")
    level = int(raw_volume) if raw_volume.isdecimal() else None
    if level is not None and not 0 <= level <= 100:
        level = None
    raw_mute = _message_value(mute, "state")
    muted = True if raw_mute == "on" else False if raw_mute == "off" else None
    return HeosStatus(
        title=title,
        source=source if source != title else "",
        artist=_text(details.get("artist")),
        album=_text(details.get("album")),
        state_label={"play": "odtwarzanie", "pause": "wstrzymane", "stop": "zatrzymane"}.get(state, ""),
        volume=level,
        muted=muted,
        media_type=_text(details.get("type")),
    )


def target_choices(players: object, groups: object) -> tuple[HeosTarget, ...]:
    """Show existing groups plus ungrouped players; no IDs in labels."""

    if not isinstance(players, list) or not isinstance(groups, list):
        raise ValueError("HEOS nie zwrócił listy urządzeń i grup.")
    choices: list[HeosTarget] = []
    grouped: set[str] = set()
    for group in groups:
        if not isinstance(group, dict) or not isinstance(group.get("players"), list):
            continue
        gid = _id(group.get("gid"))
        members = tuple(
            pid for member in group["players"]
            if isinstance(member, dict) and (pid := _id(member.get("pid")))
        )
        leader = next((
            _id(member.get("pid")) for member in group["players"]
            if isinstance(member, dict) and _text(member.get("role")).lower() == "leader"
        ), "")
        if not gid or not leader or not members:
            continue
        grouped.update(members)
        choices.append(HeosTarget(
            item_id=_key("group", gid),
            title=_text(group.get("name")) or "Grupa HEOS",
            player_id=leader,
            group_id=gid,
            member_ids=members,
        ))
    for player in players:
        if not isinstance(player, dict):
            continue
        pid = _id(player.get("pid"))
        if not pid or pid in grouped:
            continue
        choices.append(HeosTarget(
            item_id=_key("player", pid),
            title=_text(player.get("name")) or _text(player.get("model")) or "Urządzenie HEOS",
            player_id=pid,
        ))
    return tuple(choices)


def source_choices(sources: object) -> tuple[HeosSource, ...]:
    """List only announced available sources; browsing remains a separate check."""

    if not isinstance(sources, list):
        raise ValueError("HEOS nie zwrócił listy źródeł.")
    result: list[HeosSource] = []
    for source in sources:
        if not isinstance(source, dict) or source.get("available") is False:
            continue
        if isinstance(source.get("available"), str) and source["available"].lower() == "false":
            continue
        sid = _id(source.get("sid"))
        kind = _text(source.get("type"))
        title = _text(source.get("name"))
        if not sid or not title or kind not in {
            "music_service", "heos_service", "heos_server", "dlna_server"
        }:
            continue
        result.append(HeosSource(_key("source", sid), title, sid, kind))
    return tuple(result)


_KIND_LABELS = {
    "artist": "wykonawca", "album": "album", "song": "utwór",
    "station": "stacja", "playlist": "playlista", "genre": "gatunek",
    "container": "folder", "folder": "folder",
}


def catalog_items(source_id: str, container_id: str | None, payload: object) -> tuple[HeosItem, ...]:
    """Preserve server order and duplicate titles, scoped to source and folder."""

    if not isinstance(payload, list):
        raise ValueError("HEOS nie zwrócił listy pozycji.")
    sid = _id(source_id)
    if not sid:
        raise ValueError("Nie wybrano źródła HEOS.")
    parent = _id(container_id)
    result: list[HeosItem] = []
    for index, raw in enumerate(payload):
        if not isinstance(raw, dict):
            continue
        kind = _text(raw.get("type")).lower()
        cid = _id(raw.get("cid"))
        mid = _id(raw.get("mid"))
        child_sid = _id(raw.get("sid")) if kind in {
            "music_service", "heos_service", "heos_server", "dlna_server"
        } else ""
        can_open = (_yes(raw.get("container")) and bool(cid)) or bool(child_sid)
        can_play = _yes(raw.get("playable")) and bool(mid or cid)
        title = _text(raw.get("name")) or _text(raw.get("song")) or _text(raw.get("station"))
        if not title or not (can_open or can_play):
            continue
        label = _KIND_LABELS.get(kind)
        if label is None:
            label = "źródło" if child_sid else "folder" if can_open else "utwór" if can_play else "pozycja"
        detail = _text(raw.get("artist"))
        if detail == title:
            detail = ""
        result.append(HeosItem(
            item_id=_key("item", sid, parent, cid, mid, child_sid, index),
            title=title,
            kind_label=label,
            source_id=sid,
            container_id=cid,
            media_id=mid,
            can_open=can_open,
            can_play=can_play,
            detail=detail,
            child_source_id=child_sid,
        ))
    return tuple(result)
