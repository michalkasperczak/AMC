"""Narrow, validated playback handoff for a restarted AMC Test build."""

from __future__ import annotations

import math


SESSIONS = ("radio", "files", "podcasts", "radioarchiwista", "tidal")


def normalize_snapshot(raw: object) -> list[dict]:
    if not isinstance(raw, list):
        return []
    result: list[dict] = []
    seen: set[str] = set()
    for value in raw:
        if not isinstance(value, dict) or value.get("playing") is not True:
            continue
        session = value.get("sessionId")
        source = value.get("source")
        track_id = value.get("trackId")
        item_id = value.get("id")
        title = value.get("title")
        if (
            session not in SESSIONS or session in seen
            or (session != "tidal" and (not isinstance(source, str) or not source.strip()))
            or not isinstance(item_id, str) or not item_id.strip()
            or not isinstance(title, str) or not title.strip()
        ):
            continue
        if session == "tidal" and (
            not isinstance(track_id, str) or not track_id.isascii()
            or not track_id.isdecimal() or not 1 <= len(track_id) <= 18
        ):
            continue
        if session == "radio" and not source.lower().startswith(
            ("http://", "https://", "mms://", "icy://", "rtsp://")
        ):
            continue
        if session == "files" and source.lower().startswith(("http://", "https://")):
            continue
        try:
            volume = max(0, min(100, int(value.get("volume", 100))))
            rate = float(value.get("rate", 1.0))
            position = float(value.get("positionSeconds", 0.0))
        except (TypeError, ValueError, OverflowError):
            continue
        if not math.isfinite(rate) or not 0.25 <= rate <= 4.0:
            continue
        if not math.isfinite(position) or position < 0:
            continue
        result.append({
            "sessionId": session,
            "source": (
                source.strip() if session != "tidal" and isinstance(source, str)
                else ""
            ),
            "id": item_id.strip(),
            "title": title.strip(),
            "playing": True,
            "volume": volume,
            "rate": rate,
            "positionSeconds": position,
            "muted": value.get("muted") is True,
        })
        if session == "tidal":
            result[-1]["trackId"] = track_id
        seen.add(session)
    return result
