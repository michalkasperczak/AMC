"""Shared source/session contract for Cast and additional audio outputs."""
from __future__ import annotations

import re
from urllib.parse import urlsplit
from . import i18n

DIRECT_AUDIO_SESSIONS = ("files", "radio", "podcasts", "radioarchiwista")
NETWORK_AUDIO_SESSIONS = (*DIRECT_AUDIO_SESSIONS, "tidal")
SHARED_DECODER_SESSIONS = ("files", "podcasts", "radioarchiwista", "tidal")
PLAY_OPERATIONS = {"files.play": "files", "radio.play": "radio",
                   "media.play": "podcasts", "tidal.nativePlay": "tidal"}


def play_session(op: str, args: dict) -> str | None:
    if op == "media.play":
        session = args.get("sessionId", "podcasts")
        return session if session in ("podcasts", "radioarchiwista") else None
    return PLAY_OPERATIONS.get(op)


def retire_displaced_mirrors(router, op: str, args: dict) -> None:
    session = play_session(op, args)
    if session not in SHARED_DECODER_SESSIONS:
        return
    for owner in tuple(router.routes):
        if owner == session or owner not in SHARED_DECODER_SESSIONS:
            continue
        warning = None
        try:
            warning = router.disable(owner).get("warning")
        except Exception:
            warning = i18n.tr("cast.error.stopUnconfirmed")
        if warning and router.event:
            try:
                router.event("mirror.failed", {"sessionId": owner, "message": warning})
            except Exception:
                pass


def valid_tidal_request(request: object, *, cast: bool = False) -> bool:
    if not isinstance(request, dict):
        return False
    if not isinstance(request.get("proxyUrl"), str) or not isinstance(request.get("id"), str):
        return False
    try:
        url = urlsplit(request.get("proxyUrl", ""))
        return bool(
            re.fullmatch(r"[0-9]{1,18}", str(request.get("id", "")))
            and url.scheme == "http" and url.hostname == "127.0.0.1"
            and url.port and not url.username and not url.password
            and not url.query and not url.fragment
            and re.fullmatch(r"/audio/[A-Za-z0-9_-]{32}" + ("" if cast else r"(?:\.mpd)?"), url.path)
        )
    except (TypeError, ValueError):
        return False


def tidal_request(item: dict) -> dict:
    request = item.get("_tidal_request", item)
    return dict(request) if isinstance(request, dict) else {}


def source_for_session(session: str, item: dict) -> str:
    if session == "tidal":
        request = tidal_request(item)
        if not valid_tidal_request(request):
            raise ValueError(i18n.tr("cast.error.noMaterial"))
        return request["proxyUrl"]
    return str(item.get("source") or item.get("path") or item.get("url") or "")
