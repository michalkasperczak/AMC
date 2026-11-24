"""User-facing entries for the cross-session playback chooser."""

from __future__ import annotations

from dataclasses import dataclass

from . import i18n
from .navigation import SessionId


_PLAYING = {"play", "playing", "odtwarzanie"}
_PAUSED = {"pause", "paused", "wstrzymano", "wstrzymane"}


@dataclass(frozen=True, slots=True)
class ActivePlaybackItem:
    session: SessionId
    title: str
    service: str
    device: str
    state: str
    item_id: str = ""
    external: bool = False

    @property
    def label(self) -> str:
        return ", ".join((self.title, self.service, self.device,
                          i18n.ui_text(self.state)))

    def __str__(self) -> str:
        return self.label


def host_item(
    session: SessionId, status: object, *, service: str, device: str,
) -> ActivePlaybackItem | None:
    """Accept only a confirmed item belonging to the requested host session."""
    if not isinstance(status, dict):
        return None
    returned = str(status.get("sessionId") or "").strip()
    if returned and returned != session.value:
        return None
    playing = status.get("playing") is True
    paused = status.get("paused") is True
    if not playing and not paused:
        return None
    title = str(
        status.get("currentBroadcastText")
        or status.get("currentPlaybackText")
        or status.get("title")
        or ""
    ).strip()
    item_id = str(status.get("id") or "").strip()
    if not title and not item_id:
        return None
    return ActivePlaybackItem(
        session, title or "Odtwarzany materiał", service, device,
        "pauza" if paused else "odtwarzanie", item_id,
    )


def remote_item(
    session: SessionId, status: object, *, service: str,
    device: str, fallback_title: str = "",
) -> ActivePlaybackItem | None:
    """An idle selected speaker is not itself a playing item."""
    if not isinstance(status, dict):
        return None
    raw_state = str(status.get("playbackState") or "").strip().casefold()
    if raw_state not in _PLAYING | _PAUSED:
        return None
    title = str(status.get("title") or fallback_title or "").strip()
    return ActivePlaybackItem(
        session, title or "Odtwarzany materiał", service, device,
        "pauza" if raw_state in _PAUSED else "odtwarzanie",
        str(status.get("itemId") or "").strip(),
    )
