"""Render HEOS DTOs as ordinary AMC list rows with intentional spoken text."""

from __future__ import annotations

from .heos_source import HeosItem, HeosQueueItem, HeosSource, HeosTarget
from .list_model import Row


def target_rows(targets: tuple[HeosTarget, ...]) -> tuple[Row, ...]:
    return tuple(Row(
        item_id=target.item_id,
        title=target.title,
        kind="heosGroup" if target.group_id else "device",
        service_id=target.player_id,
    ) for target in targets)


def source_rows(sources: tuple[HeosSource, ...]) -> tuple[Row, ...]:
    return tuple(Row(
        item_id=source.item_id,
        title=source.title,
        kind="heosSource",
        service_id=source.source_id,
    ) for source in sources)


def catalog_rows(items: tuple[HeosItem, ...]) -> tuple[Row, ...]:
    result: list[Row] = []
    for item in items:
        kind = {
            "wykonawca": "artist", "album": "album", "utwór": "track",
            "stacja": "station", "playlista": "playlist", "folder": "folder",
            "źródło": "heosSource",
        }.get(item.kind_label, "heosItem")
        result.append(Row(
            item_id=item.item_id,
            title=item.title,
            kind=kind,
            detail=item.detail,
            activation_message=(
                None if item.can_open or item.can_play
                else "Ta pozycja HEOS nie jest dostępna"
            ),
        ))
    return tuple(result)


def queue_rows(items: tuple[HeosQueueItem, ...]) -> tuple[Row, ...]:
    return tuple(Row(
        item_id=item.item_id,
        title=item.title,
        kind="track",
        detail=item.detail,
    ) for item in items)
