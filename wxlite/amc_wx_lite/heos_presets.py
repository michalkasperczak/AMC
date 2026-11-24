"""Stable, device-scoped AMC presets for playable HEOS stations."""

from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass

from .heos_source import HeosItem, HeosTarget
from .profile_presets import PresetEntry, PresetTarget


@dataclass(frozen=True, slots=True)
class HeosPresetReference:
    player_id: str
    source_id: str
    container_id: str
    media_id: str


def target_for_station(target: HeosTarget, item: HeosItem) -> PresetTarget | None:
    if not (
        target.player_id and item.source_id and item.media_id
        and item.media_type == "station" and item.can_play
    ):
        return None
    reference = HeosPresetReference(
        target.player_id, item.source_id, item.parent_container_id, item.media_id,
    )
    digest = hashlib.sha256(
        "\0".join((reference.player_id, reference.source_id,
                   reference.container_id, reference.media_id)).encode("utf-8")
    ).hexdigest()[:24]
    location = json.dumps({
        "player": reference.player_id,
        "source": reference.source_id,
        "container": reference.container_id,
        "media": reference.media_id,
    }, ensure_ascii=False, separators=(",", ":"))
    return PresetTarget("heos-station:" + digest, "heos-station", item.title, location)


def reference_for_entry(entry: PresetEntry, target: HeosTarget) -> HeosPresetReference | None:
    if entry.target_kind.casefold() != "heos-station" or not entry.location:
        return None
    try:
        payload = json.loads(entry.location)
    except (TypeError, ValueError):
        return None
    if not isinstance(payload, dict) or set(payload) != {
        "player", "source", "container", "media",
    }:
        return None
    values = tuple(payload[key] for key in ("player", "source", "container", "media"))
    if any(not isinstance(value, str) or len(value) > 1024 or "\n" in value or "\r" in value
           for value in values):
        return None
    player_id, source_id, container_id, media_id = values
    if player_id != target.player_id or not source_id or not media_id:
        return None
    return HeosPresetReference(player_id, source_id, container_id, media_id)
