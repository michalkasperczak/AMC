"""Publiczne transmisje YouTube jako jawne, dostępne wiersze AMC.

Token i identyfikator kanału pozostają w hoście C#. Python dostaje wyłącznie
tytuł, stan, publiczny adres filmu i daty przeznaczone do pokazania użytkownikowi.
"""

from __future__ import annotations

import hashlib
from dataclasses import replace
from datetime import datetime, timezone
from typing import Mapping, Sequence
from urllib.parse import unquote, urlsplit, urlunsplit

from .list_model import Row


class YouTubeLivePayloadError(RuntimeError):
    """Odpowiedź hosta nie nadaje się do bezpiecznego pokazania."""


def is_youtube_station_url(source_url: str) -> bool:
    """Recognize a YouTube video or channel without accepting lookalike hosts."""

    try:
        parsed = urlsplit(source_url.strip())
    except ValueError:
        return False
    host = (parsed.hostname or "").casefold()
    if parsed.scheme.casefold() not in {"http", "https"}:
        return False
    if host == "youtu.be":
        return bool(parsed.path.strip("/"))
    if host != "youtube.com" and not host.endswith(".youtube.com"):
        return False
    parts = [part for part in parsed.path.split("/") if part]
    if parts == ["watch"]:
        return "v=" in parsed.query
    return bool(
        len(parts) >= 2 and parts[0].casefold() == "channel"
        or parts and parts[0].startswith("@")
        or len(parts) == 2 and parts[0].casefold() == "live"
    )


def current_or_next_broadcast(payload: object, *, now: datetime | None = None) -> Row | None:
    """Prefer a live event; otherwise choose the earliest future event."""

    moment = (now or datetime.now(timezone.utc)).astimezone(timezone.utc)
    rows = broadcast_rows(payload, subscription_id="radio-channel")
    live = [row for row in rows if row.service_kind == "youtubeLive"]
    if live:
        return max(
            live,
            key=lambda row: _read_time(row.service_start_utc) or datetime.min.replace(tzinfo=timezone.utc),
        )
    upcoming = [
        row for row in rows
        if row.service_kind == "youtubeUpcoming"
        and (start := _read_time(row.service_start_utc)) is not None
        and start >= moment
    ]
    return min(upcoming, key=lambda row: _read_time(row.service_start_utc)) if upcoming else None


def dynamic_live_url(source_url: str) -> str:
    """Zamień zapisany kanał na stały adres wybierający bieżące ``/live``."""

    try:
        parsed = urlsplit(source_url.strip())
    except ValueError as error:
        raise YouTubeLivePayloadError(
            "Ten kanał YouTube nie ma adresu potrzebnego do dynamicznej stacji."
        ) from error
    host = (parsed.hostname or "").casefold()
    if parsed.scheme.casefold() not in {"http", "https"} or not (
        host == "youtube.com" or host.endswith(".youtube.com")
    ):
        raise YouTubeLivePayloadError(
            "Ten kanał YouTube nie ma adresu potrzebnego do dynamicznej stacji."
        )
    segments = [unquote(part).strip() for part in parsed.path.split("/") if part]
    if len(segments) >= 2 and segments[0].casefold() == "channel":
        identity = segments[1]
        stem = f"/channel/{identity}"
    elif segments and segments[0].startswith("@") and len(segments[0]) > 1:
        identity = segments[0]
        stem = f"/{identity}"
    else:
        raise YouTubeLivePayloadError(
            "Ten kanał YouTube nie ma adresu potrzebnego do dynamicznej stacji."
        )
    if any(character.isspace() or character in "?#/\\" for character in identity):
        raise YouTubeLivePayloadError(
            "Ten kanał YouTube nie ma adresu potrzebnego do dynamicznej stacji."
        )
    return urlunsplit(("https", "www.youtube.com", f"{stem}/live", "", ""))


def broadcast_rows(
    payload: object,
    *,
    subscription_id: str,
) -> list[Row]:
    """Utwórz wiersze bez repr obiektów, enumów i technicznych identyfikatorów."""

    if not isinstance(payload, Mapping):
        raise YouTubeLivePayloadError("YouTube zwrócił nieczytelną listę transmisji.")
    items = payload.get("items")
    if not isinstance(items, list):
        raise YouTubeLivePayloadError("YouTube zwrócił nieczytelną listę transmisji.")
    rows: list[Row] = []
    for item in items:
        if not isinstance(item, Mapping):
            continue
        title = _text(item.get("title"), "Transmisja YouTube")
        page_url = _public_watch_url(item.get("pageUrl"))
        state = str(item.get("state") or "").strip().casefold()
        if page_url is None or state not in {"live", "upcoming"}:
            continue
        moment = _read_time(
            item.get("actualStartUtc") if state == "live" else item.get("scheduledStartUtc")
        )
        detail = _detail(state, moment)
        stable_id = hashlib.sha256(page_url.encode("utf-8")).hexdigest()[:24]
        rows.append(Row(
            item_id=f"youtube-broadcast:{stable_id}",
            title=title,
            kind="episode",
            path=page_url,
            url=page_url,
            detail=detail,
            show_kind=False,
            parent_id=subscription_id,
            service_kind="youtubeLive" if state == "live" else "youtubeUpcoming",
            service_start_utc=(moment.isoformat() if moment is not None else None),
        ))
    return rows


def aggregate_broadcast_rows(
    channels: Sequence[tuple[str, str, object]],
) -> list[Row]:
    """Połącz wyniki wybranych kanałów w jedną dostępną listę.

    ``subscription_id`` pozostaje polem modelu. Czytnik otrzymuje nazwę
    kanału, stan i czas, dzięki czemu dwie transmisje o tym samym tytule nie
    są nierozróżnialne. Trwające transmisje są pierwsze, a zaplanowane dalej
    chronologicznie.
    """

    rows: list[Row] = []
    seen_urls: set[str] = set()
    for subscription_id, channel_title, payload in channels:
        safe_channel_title = _text(channel_title, "Kanał YouTube")
        for row in broadcast_rows(payload, subscription_id=subscription_id):
            if not row.url or row.url in seen_urls:
                continue
            seen_urls.add(row.url)
            rows.append(replace(
                row,
                detail=f"{safe_channel_title}, {row.detail}",
                service_section=safe_channel_title,
            ))

    def sort_key(row: Row) -> tuple[object, ...]:
        live_priority = 0 if row.service_kind == "youtubeLive" else 1
        moment = _read_time(row.service_start_utc)
        moment_key = moment.timestamp() if moment is not None else float("inf")
        # Dla trwających materiałów najważniejsza jest czytelna, stabilna
        # kolejność kanał/tytuł. Zaplanowane porządkujemy po terminie.
        if live_priority == 0:
            moment_key = 0.0
        return (
            live_priority,
            moment_key,
            row.service_section.casefold() if row.service_section else "",
            row.title.casefold(),
        )

    return sorted(rows, key=sort_key)


def _text(value: object, fallback: str) -> str:
    text = " ".join(str(value or "").split())
    return (text[:500].strip() if text else fallback)


def _public_watch_url(value: object) -> str | None:
    try:
        parsed = urlsplit(str(value or "").strip())
    except ValueError:
        return None
    host = (parsed.hostname or "").casefold()
    if parsed.scheme.casefold() != "https" or not (
        host == "youtube.com" or host.endswith(".youtube.com")
    ):
        return None
    if parsed.path.rstrip("/").casefold() != "/watch" or not parsed.query:
        return None
    return urlunsplit(("https", "www.youtube.com", "/watch", parsed.query, ""))


def _read_time(value: object) -> datetime | None:
    text = str(value or "").strip()
    if not text:
        return None
    try:
        parsed = datetime.fromisoformat(text.replace("Z", "+00:00"))
    except ValueError:
        return None
    return parsed.astimezone()


def _detail(state: str, moment: datetime | None) -> str:
    prefix = "na żywo" if state == "live" else "zaplanowana"
    if moment is None:
        return prefix
    now = datetime.now().astimezone()
    if moment.date() == now.date():
        return f"{prefix} dzisiaj o {moment:%H:%M}"
    return f"{prefix} {moment:%d.%m.%Y} o {moment:%H:%M}"
