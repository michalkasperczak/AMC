"""Small HEOS operations facade for a background worker in AMC Test.

The caller owns view state and explicit target selection. These methods block
on the LAN, so none belongs on wx's GUI thread. Control methods are never
called by a read operation or retried automatically after an uncertain reply.
"""

from __future__ import annotations

from dataclasses import dataclass

from .heos_client import HeosControlClient, HeosPage
from .heos_source import (
    HeosItem, HeosQueueItem, HeosSource, HeosStatus, HeosTarget,
    catalog_items, playback_status, queue_items, source_choices, target_choices,
)


@dataclass(frozen=True, slots=True)
class HeosCatalogPage:
    items: tuple[HeosItem, ...]
    next_start: int
    has_more: bool
    total: int | None


class HeosService:
    """One LAN host; a player or group leader is passed on each operation."""

    def __init__(self, client: HeosControlClient) -> None:
        self.client = client

    def close(self) -> None:
        self.client.close()

    def targets(self) -> tuple[HeosTarget, ...]:
        return target_choices(self.client.players(), self.client.groups())

    def sources(self) -> tuple[HeosSource, ...]:
        return source_choices(self.client.sources())

    def catalog_page(self, source_id: str, container_id: str = "", *,
                     start: int = 0, limit: int = 100) -> HeosCatalogPage:
        page: HeosPage = self.client.browse_page(
            source_id, container_id or None, start=start, limit=limit,
        )
        return HeosCatalogPage(
            catalog_items(source_id, container_id, list(page.items)),
            page.next_start, page.has_more, page.total,
        )

    def queue(self, player_id: str) -> tuple[HeosQueueItem, ...]:
        return queue_items(self.client.queue(player_id))

    def status(self, player_id: str) -> HeosStatus:
        state = self.client.player_state(player_id)
        return playback_status(
            state["play"], state["media"], state["volume"], state["mute"],
        )

    def play_item(self, player_id: str, item: HeosItem, *,
                  placement: str = "now", replace_confirmed: bool = False) -> None:
        if not item.can_play:
            raise ValueError("Ta pozycja HEOS nie jest odtwarzalna.")
        if item.media_type == "station":
            if placement != "now":
                raise ValueError("Stacji HEOS nie można dodać do kolejki.")
            self.client.play_station(
                player_id, item.source_id, item.media_id, item.title,
                parent_cid=item.parent_container_id,
            )
            return
        self.client.add_to_queue(
            player_id, item.source_id,
            cid=item.container_id or item.parent_container_id,
            mid=item.media_id,
            placement=placement, replace_confirmed=replace_confirmed,
        )

    def play_queue_item(self, player_id: str, item: HeosQueueItem) -> None:
        self.client.play_queue_if_current(
            player_id, item.queue_id, item.fingerprint,
        )

    def set_play_state(self, player_id: str, state: str) -> None:
        self.client.set_play_state(player_id, state)

    def next(self, player_id: str) -> None:
        self.client.next(player_id)

    def previous(self, player_id: str) -> None:
        self.client.previous(player_id)

    def set_volume(self, player_id: str, level: int) -> None:
        self.client.set_volume(player_id, level)

    def set_mute(self, player_id: str, muted: bool) -> None:
        self.client.set_mute(player_id, muted)
