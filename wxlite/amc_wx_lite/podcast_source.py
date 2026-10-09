"""Read-only podcast and YouTube library source for the wxPython interface.

The C# host remains the sole owner of ``podcasts.db``.  This module opens the
same database with SQLite ``mode=ro`` (without ``immutable=1`` so committed WAL
changes stay visible) and turns only intentional, user-facing fields into list
rows.  JSON records and technical identifiers are never used as spoken labels.
"""

from __future__ import annotations

import json
import sqlite3
from contextlib import closing
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from pathlib import Path

from .list_model import Row
from .profile_layout import ProfileLayout, resolve_layout


PAGE_SIZE = 150
_DOTNET_EPOCH = datetime(1, 1, 1, tzinfo=timezone.utc)


class PodcastProfileError(RuntimeError):
    """The shared podcast profile cannot be read safely."""


@dataclass(frozen=True, slots=True)
class PodcastSubscription:
    subscription_id: str
    title: str
    source_kind: int

    @property
    def kind_label(self) -> str:
        return {
            0: "podcast",
            1: "media internetowe",
            2: "kanał YouTube",
            3: "playlista YouTube",
        }.get(self.source_kind, "źródło podcastów")

    def as_row(self) -> Row:
        return Row(
            item_id=self.subscription_id,
            title=self.title,
            kind="podcast",
            detail=self.kind_label,
            show_kind=False,
        )


@dataclass(frozen=True, slots=True)
class PodcastEpisodePage:
    rows: list[Row]
    loaded_count: int
    has_more: bool


def _clean_text(value: object, fallback: str) -> str:
    if not isinstance(value, str):
        return fallback
    cleaned = " ".join(value.replace("\x00", " ").split())
    return cleaned or fallback


def _payload(raw: object) -> dict:
    if not isinstance(raw, str) or not raw:
        return {}
    try:
        value = json.loads(raw)
    except (TypeError, ValueError, json.JSONDecodeError):
        return {}
    return value if isinstance(value, dict) else {}


def _get(payload: dict, name: str, default=None):
    """Read one known field from PascalCase or camelCase JSON only."""
    if name in payload:
        return payload[name]
    camel = name[:1].lower() + name[1:]
    return payload.get(camel, default)


def _as_int(value: object, default: int = 0) -> int:
    if isinstance(value, bool):
        return int(value)
    try:
        return int(value)
    except (TypeError, ValueError, OverflowError):
        return default


def _as_bool(value: object) -> bool:
    return value is True or value == 1


def _date_label(ticks: int) -> str:
    if ticks <= 0:
        return "data nieznana"
    try:
        value = _DOTNET_EPOCH + timedelta(microseconds=ticks // 10)
        local = value.astimezone()
    except (OverflowError, OSError, ValueError):
        return "data nieznana"
    return f"{local.day:02d}.{local.month:02d}.{local.year:04d}"


def _duration_label(ticks: int) -> str:
    seconds = max(0, ticks // 10_000_000)
    if seconds <= 0:
        return "czas nieznany"
    hours, remainder = divmod(seconds, 3600)
    minutes, _ = divmod(remainder, 60)
    if hours:
        return f"{hours} godz. {minutes} min" if minutes else f"{hours} godz."
    return f"{max(1, minutes)} min"


def _progress_label(*, is_new: bool, is_started: bool, is_played: bool) -> str:
    if is_played:
        return "odtworzony"
    if is_started:
        return "w trakcie"
    return "nowy" if is_new else "nieodtworzony"


def _source_kind(payload: dict) -> int:
    value = _get(payload, "SourceKind", 0)
    if isinstance(value, str):
        by_name = {
            "rss": 0,
            "publicinternetmedia": 1,
            "youtubechannel": 2,
            "youtubeplaylist": 3,
        }
        return by_name.get(value.replace("_", "").casefold(), 0)
    return _as_int(value)


class PodcastSource:
    """Fresh, read-only views over the shared AMC podcast database."""

    def __init__(self, layout: ProfileLayout | None = None) -> None:
        self.layout = layout or resolve_layout()

    @property
    def database_path(self) -> Path:
        return self.layout.podcasts_db

    @property
    def is_available(self) -> bool:
        return self.database_path.exists()

    def _open(self) -> sqlite3.Connection:
        path = self.database_path
        if not path.exists():
            raise PodcastProfileError("Nie znaleziono biblioteki podcastów AMC.")
        try:
            connection = sqlite3.connect(
                f"{path.as_uri()}?mode=ro",
                uri=True,
                timeout=2.0,
                check_same_thread=False,
            )
            connection.row_factory = sqlite3.Row
            connection.execute("PRAGMA query_only = ON")
            connection.execute("SELECT count(*) FROM sqlite_schema").fetchone()
            return connection
        except sqlite3.Error as error:
            try:
                connection.close()
            except (UnboundLocalError, sqlite3.Error):
                pass
            raise PodcastProfileError(
                f"Nie można odczytać biblioteki podcastów AMC: {error}"
            ) from error

    def subscriptions(self) -> list[PodcastSubscription]:
        try:
            with closing(self._open()) as connection:
                records = connection.execute(
                    """
                    SELECT id, title, payload_json
                    FROM podcast_subscriptions
                    WHERE is_in_library = 1
                    ORDER BY ordinal
                    """
                ).fetchall()
        except sqlite3.Error as error:
            raise PodcastProfileError(
                f"Nie można odczytać listy podcastów: {error}"
            ) from error

        result: list[PodcastSubscription] = []
        for record in records:
            subscription_id = str(record["id"] or "").strip()
            if not subscription_id:
                continue
            data = _payload(record["payload_json"])
            result.append(PodcastSubscription(
                subscription_id=subscription_id,
                title=_clean_text(record["title"], "Podcast bez nazwy"),
                source_kind=_source_kind(data),
            ))
        return result

    def subscription(self, subscription_id: str) -> PodcastSubscription | None:
        try:
            with closing(self._open()) as connection:
                record = connection.execute(
                    """
                    SELECT id, title, payload_json
                    FROM podcast_subscriptions
                    WHERE id = ? AND is_in_library = 1
                    """,
                    (subscription_id,),
                ).fetchone()
        except sqlite3.Error as error:
            raise PodcastProfileError(
                f"Nie można odczytać podcastu: {error}"
            ) from error
        if record is None:
            return None
        data = _payload(record["payload_json"])
        return PodcastSubscription(
            subscription_id=str(record["id"]),
            title=_clean_text(record["title"], "Podcast bez nazwy"),
            source_kind=_source_kind(data),
        )

    def episodes(self, subscription_id: str, *, loaded_count: int = PAGE_SIZE) -> PodcastEpisodePage:
        subscription = self.subscription(subscription_id)
        if subscription is None:
            raise PodcastProfileError("Tego podcastu nie ma już w Bibliotece.")
        requested = max(PAGE_SIZE, _as_int(loaded_count, PAGE_SIZE))
        # FeedOrdinal is authoritative for YouTube.  It lives in payload_json;
        # SQLite JSON extraction lets us page without loading tens of thousands
        # of rows into Python.  RSS/public media keep C#'s date/title/id order.
        if subscription.source_kind in (2, 3):
            order = """
                CASE WHEN json_extract(payload_json, '$.FeedOrdinal') IS NULL
                     THEN 2147483647
                     ELSE CAST(json_extract(payload_json, '$.FeedOrdinal') AS INTEGER)
                END,
                ordinal
            """
        else:
            order = "published_utc_ticks DESC, title COLLATE NOCASE, id"
        try:
            with closing(self._open()) as connection:
                records = connection.execute(
                    f"""
                    SELECT id, title, published_utc_ticks, is_new, is_started,
                           is_played, download_path, payload_json
                    FROM podcast_episodes
                    WHERE subscription_id = ?
                    ORDER BY {order}
                    LIMIT ?
                    """,
                    (subscription_id, requested + 1),
                ).fetchall()
        except sqlite3.Error as error:
            raise PodcastProfileError(
                f"Nie można odczytać odcinków: {error}"
            ) from error

        has_more = len(records) > requested
        visible = records[:requested]
        rows: list[Row] = []
        for record in visible:
            data = _payload(record["payload_json"])
            media_url = str(_get(data, "MediaUrl", "") or "").strip()
            download_path = str(record["download_path"] or "").strip()
            local_download = download_path if download_path and Path(download_path).is_file() else ""
            source = local_download or media_url
            playable = bool(source)
            detail = ", ".join((
                _date_label(_as_int(record["published_utc_ticks"])),
                _duration_label(_as_int(_get(data, "DurationTicks", 0))),
                _progress_label(
                    is_new=_as_bool(record["is_new"]),
                    is_started=_as_bool(record["is_started"]),
                    is_played=_as_bool(record["is_played"]),
                ),
            ))
            rows.append(Row(
                item_id=str(record["id"]),
                title=_clean_text(record["title"], "Odcinek bez nazwy"),
                kind="episode",
                path=source or None,
                url=media_url or None,
                detail=detail,
                show_kind=False,
                activation_message=None if playable else "Ten odcinek nie ma adresu do odtworzenia",
                position_seconds=max(0.0, _as_int(_get(data, "ResumePositionTicks", 0)) / 10_000_000),
            ))

        if has_more:
            rows.append(Row(
                item_id=f"podcast-load-more:{subscription_id}:{requested}",
                title="Załaduj więcej odcinków",
                kind="loadMore",
                detail="kolejne 150 odcinków",
                show_kind=False,
            ))
        return PodcastEpisodePage(rows, len(visible), has_more)


def subscription_rows(subscriptions: list[PodcastSubscription]) -> list[Row]:
    return [subscription.as_row() for subscription in subscriptions]
