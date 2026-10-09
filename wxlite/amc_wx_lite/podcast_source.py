"""Read-only podcast and YouTube library source for the wxPython interface.

The C# host remains the sole owner of ``podcasts.db``.  This module opens the
same database with SQLite ``mode=ro`` (without ``immutable=1`` so committed WAL
changes stay visible) and turns only intentional, user-facing fields into list
rows.  JSON records and technical identifiers are never used as spoken labels.
"""

from __future__ import annotations

import json
import re
import sqlite3
from contextlib import closing
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from pathlib import Path

from .collation import (
    COLLATION_TITLE_IGNORE_CASE,
    HostCollation,
    HostCollationUnavailable,
)
from .library_activity import ordinal_sort_key
from .library_db import LibraryDatabase
from .library_views import _stored_order
from .list_model import Row
from .profile_layout import ProfileLayout, resolve_layout
from .radio_source import (
    SORT_ADDED_NEWEST,
    SORT_ALPHABETICAL,
    SORT_CUSTOM,
    collection_sort_modes_from_amc_state,
)


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
    order_matches_amc: bool = True
    sort_mode: str | None = None


@dataclass(frozen=True, slots=True)
class PodcastDescription:
    """Long-form user text for the native description dialog.

    Identifiers stay outside this value.  The dialog receives only the title,
    the requested description and intentional, human-readable metadata.
    """

    window_title: str
    text: str
    initial_focus_name: str


@dataclass(frozen=True, slots=True)
class _EpisodeRecord:
    item_id: str
    subscription_id: str
    title: str
    parent_title: str
    source_kind: int
    published_ticks: int
    is_new: bool
    is_started: bool
    is_played: bool
    download_path: str
    payload: dict


@dataclass(frozen=True, slots=True)
class _FavoriteRecord:
    """One favorite with the fields used by ``OrderCurrentCollection``.

    ``row`` contains only intentional user-facing text.  ``artist`` is kept
    outside the row solely for the exact C# alphabetical tie-break; it is
    never exposed as an object representation or technical identifier.
    """

    row: Row
    artist: str


def _clean_text(value: object, fallback: str) -> str:
    if not isinstance(value, str):
        return fallback
    cleaned = " ".join(value.replace("\x00", " ").split())
    return cleaned or fallback


def _long_text(value: object) -> str:
    """Keep paragraph boundaries while removing unsafe/control-only noise."""
    if not isinstance(value, str):
        return ""
    normalized = value.replace("\x00", " ").replace("\r\n", "\n").replace("\r", "\n")
    lines = [" ".join(line.split()).strip() for line in normalized.split("\n")]
    while lines and not lines[0]:
        lines.pop(0)
    while lines and not lines[-1]:
        lines.pop()
    return "\n".join(lines)


def _author_label(value: object) -> str:
    """Port ``PodcastMetadataPresentation.FormatAuthor`` for spoken details."""
    author = _clean_text(value, "")
    if not author:
        return ""
    marker = re.compile(r"^(?:\s*(?:©|℗|®|™|\([CPR]\))\s*)+", re.IGNORECASE)
    cleaned = marker.sub("", author).lstrip("&+/\\|,;:-–—·• ")
    return cleaned or author


def _date_time_label(ticks: int) -> str:
    if ticks <= 0:
        return "jeszcze nie"
    try:
        value = (_DOTNET_EPOCH + timedelta(microseconds=ticks // 10)).astimezone()
    except (OverflowError, OSError, ValueError):
        return "jeszcze nie"
    return f"{value.day:02d}.{value.month:02d}.{value.year:04d}, {value.hour:02d}:{value.minute:02d}"


def _clock_duration(ticks: int) -> str:
    seconds = max(0, ticks // 10_000_000)
    hours, remainder = divmod(seconds, 3600)
    minutes, seconds = divmod(remainder, 60)
    return f"{hours}:{minutes:02d}:{seconds:02d}" if hours else f"{minutes}:{seconds:02d}"


def _container_label(source_kind: int) -> str:
    return {
        1: "Media internetowe",
        2: "Kanał YouTube",
        3: "Playlista YouTube",
    }.get(source_kind, "Podcast")


def _description_text(description: str, details: list[str]) -> str:
    body = description.strip()
    metadata = "\n".join(line for line in details if line).strip()
    if not body:
        return metadata
    if not metadata:
        return body
    return f"{body}\n\n{metadata}"


def _initial_focus_name(description: str) -> str:
    normalized = " ".join(description.split())
    if len(normalized) <= 500:
        return normalized
    sentence_end = max(
        (index + 1 for index, character in enumerate(normalized[:500]) if character in ".!?"),
        default=0,
    )
    if sentence_end >= 40:
        return normalized[:sentence_end]
    word_end = normalized.rfind(" ", 0, 500)
    if word_end < 1:
        word_end = 500
    return normalized[:word_end].rstrip() + "…"


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


def _episode_row(record: _EpisodeRecord, *, aggregate: bool) -> Row:
    """One episode with only intentional, user-facing accessible text."""
    media_url = str(_get(record.payload, "MediaUrl", "") or "").strip()
    local_download = (
        record.download_path
        if record.download_path and Path(record.download_path).is_file()
        else ""
    )
    source = local_download or media_url
    detail_parts = [
        _date_label(record.published_ticks),
        _duration_label(_as_int(_get(record.payload, "DurationTicks", 0))),
        _progress_label(
            is_new=record.is_new,
            is_started=record.is_started,
            is_played=record.is_played,
        ),
    ]
    if aggregate and record.parent_title.casefold() != record.title.casefold():
        detail_parts.insert(0, record.parent_title)
    if aggregate and record.source_kind in (2, 3):
        detail_parts.append("materiał YouTube")
    return Row(
        item_id=record.item_id,
        title=record.title,
        kind="episode",
        path=source or None,
        url=media_url or None,
        detail=", ".join(detail_parts),
        show_kind=False,
        activation_message=(
            None if source else "Ten odcinek nie ma adresu do odtworzenia"
        ),
        position_seconds=max(
            0.0,
            _as_int(_get(record.payload, "ResumePositionTicks", 0)) / 10_000_000,
        ),
        parent_id=record.subscription_id,
    )


def _materialize_episode(record: sqlite3.Row) -> _EpisodeRecord:
    payload = _payload(record["payload_json"])
    return _EpisodeRecord(
        item_id=str(record["id"]),
        subscription_id=str(record["subscription_id"]),
        title=_clean_text(record["title"], "Odcinek bez nazwy"),
        parent_title=_clean_text(record["parent_title"], "Podcast bez nazwy"),
        source_kind=_source_kind(_payload(record["parent_payload_json"])),
        published_ticks=_as_int(record["published_utc_ticks"]),
        is_new=_as_bool(record["is_new"]),
        is_started=_as_bool(record["is_started"]),
        is_played=_as_bool(record["is_played"]),
        download_path=str(record["download_path"] or "").strip(),
        payload=payload,
    )


def _load_more_row(*, view: str, requested: int, remaining: int) -> Row:
    return Row(
        item_id=f"podcast-load-more:{view}:{requested}",
        title="Załaduj więcej odcinków",
        kind="loadMore",
        detail=f"pozostało {remaining}",
        show_kind=False,
    )


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

    def description(self, item_id: str, item_kind: str) -> PodcastDescription | None:
        """Return the same description-first information exposed by full AMC."""
        if item_kind == "podcast":
            return self._subscription_description(item_id)
        if item_kind == "episode":
            return self._episode_description(item_id)
        return None

    def related_podcast(self, episode_id: str) -> PodcastSubscription | None:
        """Resolve an episode's parent only when it remains in the Library."""
        try:
            with closing(self._open()) as connection:
                record = connection.execute(
                    """
                    SELECT s.id, s.title, s.payload_json
                    FROM podcast_episodes AS e
                    JOIN podcast_subscriptions AS s
                      ON s.id = e.subscription_id
                    WHERE e.id = ? AND s.is_in_library = 1
                    """,
                    (episode_id,),
                ).fetchone()
        except sqlite3.Error as error:
            raise PodcastProfileError(
                f"Nie można odnaleźć podcastu tego odcinka: {error}"
            ) from error
        if record is None:
            return None
        payload = _payload(record["payload_json"])
        return PodcastSubscription(
            subscription_id=str(record["id"]),
            title=_clean_text(record["title"], "Podcast bez nazwy"),
            source_kind=_source_kind(payload),
        )

    def _subscription_description(self, item_id: str) -> PodcastDescription | None:
        try:
            with closing(self._open()) as connection:
                record = connection.execute(
                    """
                    SELECT s.title, s.feed_url, s.is_in_library,
                           s.last_refresh_utc_ticks, s.payload_json,
                           (SELECT COUNT(*) FROM podcast_episodes AS e
                            WHERE e.subscription_id = s.id) AS episode_count
                    FROM podcast_subscriptions AS s
                    WHERE s.id = ?
                    """,
                    (item_id,),
                ).fetchone()
        except sqlite3.Error as error:
            raise PodcastProfileError(
                f"Nie można odczytać opisu podcastu: {error}"
            ) from error
        if record is None:
            return None

        payload = _payload(record["payload_json"])
        description = _long_text(_get(payload, "Description", ""))
        if not description:
            return None
        source_kind = _source_kind(payload)
        internet_media = source_kind == 1
        youtube = source_kind in (2, 3)
        title = _clean_text(record["title"], "Podcast bez nazwy")
        author = _author_label(_get(payload, "Author", ""))
        feed_url = str(record["feed_url"] or "").strip()
        homepage = str(_get(payload, "HomepageUrl", "") or "").strip()
        in_library = _as_bool(record["is_in_library"])
        favorite = _as_bool(_get(payload, "IsFavorite", False))
        details = [
            _container_label(source_kind),
            f"Nazwa: {title}",
        ]
        if author:
            details.append(f"Autor: {author}")
        details.append(
            f"{'Materiały' if internet_media or youtube else 'Odcinki'}: "
            f"{max(0, _as_int(record['episode_count']))}"
        )
        if not internet_media:
            details.append(
                "Ostatnie odświeżenie: "
                + _date_time_label(_as_int(record["last_refresh_utc_ticks"]))
            )
        details.extend((
            f"Ulubiony: {'tak' if favorite else 'nie'}",
            f"W Bibliotece: {'tak' if in_library else 'nie'}",
        ))
        if not internet_media and feed_url:
            details.append(
                f"{'Adres YouTube' if youtube else 'Kanał RSS lub Atom'}: {feed_url}"
            )
        if homepage:
            details.append(f"Strona: {homepage}")
        return PodcastDescription(
            window_title="Opis podcastu",
            text=_description_text(description, details),
            initial_focus_name=_initial_focus_name(description),
        )

    def _episode_description(self, item_id: str) -> PodcastDescription | None:
        try:
            with closing(self._open()) as connection:
                record = connection.execute(
                    """
                    SELECT e.title, e.published_utc_ticks, e.is_new,
                           e.is_started, e.is_played, e.download_path,
                           e.payload_json, s.title AS parent_title, s.feed_url,
                           s.payload_json AS parent_payload_json
                    FROM podcast_episodes AS e
                    LEFT JOIN podcast_subscriptions AS s
                      ON s.id = e.subscription_id
                    WHERE e.id = ?
                    """,
                    (item_id,),
                ).fetchone()
        except sqlite3.Error as error:
            raise PodcastProfileError(
                f"Nie można odczytać opisu odcinka: {error}"
            ) from error
        if record is None:
            return None

        payload = _payload(record["payload_json"])
        description = _long_text(_get(payload, "Description", ""))
        if not description:
            return None
        parent_payload = _payload(record["parent_payload_json"])
        source_kind = _source_kind(parent_payload)
        internet_media = source_kind == 1
        youtube = source_kind in (2, 3)
        title = _clean_text(record["title"], "Odcinek bez nazwy")
        parent_title = _clean_text(record["parent_title"], "")
        author = _clean_text(_get(payload, "Author", ""), "")
        duration_ticks = _as_int(_get(payload, "DurationTicks", 0))
        media_url = str(_get(payload, "MediaUrl", "") or "").strip()
        page_url = str(_get(payload, "PageUrl", "") or "").strip()
        feed_url = str(record["feed_url"] or "").strip()
        homepage = str(_get(parent_payload, "HomepageUrl", "") or "").strip()
        details = [
            "Medium internetowe" if internet_media else (
                "Materiał YouTube" if youtube else "Odcinek podcastu"
            ),
            f"Tytuł: {title}",
        ]
        if parent_title:
            container = (
                "Kolekcja" if internet_media else (
                    _container_label(source_kind) if youtube else "Podcast"
                )
            )
            details.append(f"{container}: {parent_title}")
        if author:
            details.append(
                f"{'Kanał' if internet_media or youtube else 'Autor'}: {author}"
            )
        if not internet_media:
            published = _date_time_label(_as_int(record["published_utc_ticks"]))
            details.append(
                f"Data publikacji: {'nieznana' if published == 'jeszcze nie' else published}"
            )
        if duration_ticks > 0:
            details.append(f"Czas: {_clock_duration(duration_ticks)}")
        details.extend((
            "Stan odsłuchania: " + _progress_label(
                is_new=_as_bool(record["is_new"]),
                is_started=_as_bool(record["is_started"]),
                is_played=_as_bool(record["is_played"]),
            ),
            f"Pobrany: {'tak' if str(record['download_path'] or '').strip() else 'nie'}",
        ))
        if internet_media or youtube:
            address = page_url or media_url
            if address:
                details.append(f"Adres strony: {address}")
        else:
            if media_url:
                details.append(f"Źródło audio: {media_url}")
            if page_url:
                details.append(f"Strona odcinka: {page_url}")
            if feed_url:
                details.append(f"Kanał RSS lub Atom: {feed_url}")
        if not internet_media and homepage:
            details.append(
                f"{'Strona kanału' if youtube else 'Strona podcastu'}: {homepage}"
            )

        return PodcastDescription(
            window_title="Opis odcinka",
            text=_description_text(description, details),
            initial_focus_name=_initial_focus_name(description),
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
                parent_id=subscription_id,
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

    def inbox_sort_mode(self) -> str:
        """Saved ordering of ``Nowe odcinki``; missing data means newest."""
        try:
            raw = json.loads(self.layout.state_json.read_text(encoding="utf-8"))
        except (OSError, UnicodeError, ValueError, json.JSONDecodeError):
            return SORT_ADDED_NEWEST
        if not isinstance(raw, dict):
            return SORT_ADDED_NEWEST
        modes = collection_sort_modes_from_amc_state(raw, session="podcasts")
        return modes.get("nowe odcinki", SORT_ADDED_NEWEST)

    def favorites_sort_mode(self) -> str:
        """Saved ordering of podcast favorites; missing data means newest."""
        try:
            raw = json.loads(self.layout.state_json.read_text(encoding="utf-8"))
        except (OSError, UnicodeError, ValueError, json.JSONDecodeError):
            return SORT_ADDED_NEWEST
        if not isinstance(raw, dict):
            return SORT_ADDED_NEWEST
        modes = collection_sort_modes_from_amc_state(raw, session="podcasts")
        return modes.get("ulubione", SORT_ADDED_NEWEST)

    def _favorite_subscriptions(self) -> list[_FavoriteRecord]:
        """Favorite sources in the same base order as full AMC's working set."""
        try:
            with closing(self._open()) as connection:
                records = connection.execute(
                    """
                    SELECT id, title, payload_json
                    FROM podcast_subscriptions
                    ORDER BY ordinal
                    """
                ).fetchall()
        except sqlite3.Error as error:
            raise PodcastProfileError(
                f"Nie można odczytać ulubionych podcastów: {error}"
            ) from error

        result: list[_FavoriteRecord] = []
        for record in records:
            item_id = str(record["id"] or "").strip()
            if not item_id:
                continue
            payload = _payload(record["payload_json"])
            if not _as_bool(_get(payload, "IsFavorite", False)):
                continue
            subscription = PodcastSubscription(
                subscription_id=item_id,
                title=_clean_text(record["title"], "Podcast bez nazwy"),
                source_kind=_source_kind(payload),
            )
            result.append(_FavoriteRecord(
                row=subscription.as_row(),
                artist=_author_label(_get(payload, "Author", "")),
            ))
        return result

    def _favorite_order(self, table: str) -> list[str] | None:
        """Read one host-owned order table without ever opening it for write."""
        database: LibraryDatabase | None = None
        try:
            database = LibraryDatabase(self.layout.library_db)
            return _stored_order(database, table, "podcasts")
        except Exception:
            # The list remains usable and the caller marks its order as a
            # fallback instead of pretending it matches AMC.
            return None
        finally:
            if database is not None:
                database.close()

    def _history_ids(self) -> list[str]:
        """Podcast history IDs in persisted newest-first order."""
        database: LibraryDatabase | None = None
        try:
            database = LibraryDatabase(self.layout.library_db)
            rows = database.connection.execute(
                """
                SELECT item_id
                FROM playback_history
                WHERE session_id = ? COLLATE NOCASE
                ORDER BY ordinal
                """,
                ("podcasts",),
            )
            seen: set[str] = set()
            result: list[str] = []
            for row in rows:
                item_id = str(row[0] or "")
                if not item_id.strip() or item_id in seen:
                    continue
                seen.add(item_id)
                result.append(item_id)
                if len(result) >= 500:
                    break
            return result
        except Exception as error:
            raise PodcastProfileError(
                f"Nie można odczytać historii podcastów: {error}"
            ) from error
        finally:
            if database is not None:
                database.close()

    def _queue_ids(self, table: str) -> list[str]:
        """Read one persisted podcast queue order owned by the C# profile."""
        if table not in (
            "queue_order",
            "queue_regular_order",
            "queue_play_next_order",
        ):
            raise ValueError("Nieznana tabela kolejki podcastów")
        database: LibraryDatabase | None = None
        try:
            database = LibraryDatabase(self.layout.library_db)
            rows = database.connection.execute(
                f"""
                SELECT item_id
                FROM {table}
                WHERE session_id = ? COLLATE NOCASE
                ORDER BY ordinal
                """,
                ("podcasts",),
            )
            seen: set[str] = set()
            result: list[str] = []
            for row in rows:
                item_id = str(row[0] or "")
                if not item_id.strip() or item_id in seen:
                    continue
                seen.add(item_id)
                result.append(item_id)
            return result
        except Exception as error:
            raise PodcastProfileError(
                f"Nie można odczytać kolejki podcastów: {error}"
            ) from error
        finally:
            if database is not None:
                database.close()

    def _episode_records_by_ids(
        self, item_ids: list[str]
    ) -> dict[str, _EpisodeRecord]:
        """Read requested episode IDs in chunks below SQLite's parameter cap."""
        if not item_ids:
            return {}
        rows: list[sqlite3.Row] = []
        try:
            with closing(self._open()) as connection:
                for start in range(0, len(item_ids), 400):
                    part = item_ids[start:start + 400]
                    placeholders = ", ".join("?" for _ in part)
                    rows.extend(connection.execute(
                        f"""
                        SELECT e.id, e.subscription_id, e.title,
                               e.published_utc_ticks, e.is_new, e.is_started,
                               e.is_played, e.download_path, e.payload_json,
                               s.title AS parent_title,
                               s.payload_json AS parent_payload_json
                        FROM podcast_episodes AS e
                        JOIN podcast_subscriptions AS s
                          ON s.id = e.subscription_id
                        WHERE e.id IN ({placeholders})
                        """,
                        part,
                    ).fetchall())
        except sqlite3.Error as error:
            raise PodcastProfileError(
                f"Nie można odczytać odcinków podcastów: {error}"
            ) from error
        return {
            record.item_id: record
            for record in (_materialize_episode(row) for row in rows)
        }

    @staticmethod
    def _manual_favorite_order(
        records: list[_FavoriteRecord], stored: list[str]
    ) -> list[_FavoriteRecord]:
        """Port ``LocalLibraryManualOrder.Order`` over mixed podcast rows."""
        first: dict[str, int] = {}
        for index, item_id in enumerate(stored):
            first.setdefault(str(item_id), index)
        sentinel = len(stored) + 1
        return [
            record
            for _, _, record in sorted(
                (
                    (first.get(record.row.item_id, sentinel), original, record)
                    for original, record in enumerate(records)
                ),
                key=lambda triple: (triple[0], triple[1]),
            )
        ]

    @staticmethod
    def _alphabetical_favorites(
        records: list[_FavoriteRecord],
        collation: HostCollation | None,
    ) -> tuple[list[_FavoriteRecord], bool]:
        """C# keys: navigation title, title, artist, then ordinal ID."""
        fallback = sorted(
            records,
            key=lambda record: (
                record.row.title.casefold(),
                record.artist.casefold(),
                record.row.item_id,
            ),
        )
        if collation is None:
            return fallback, False
        try:
            # Stable least-significant-first sorting reproduces the four
            # ``OrderBy``/``ThenBy`` keys used by OrderCurrentCollection.
            ordered = sorted(records, key=lambda record: record.row.item_id)
            ordered = collation.sort_rows(
                ordered,
                key=lambda record: record.artist,
                mode=COLLATION_TITLE_IGNORE_CASE,
            )
            ordered = collation.sort_rows(
                ordered,
                key=lambda record: record.row.title,
                mode=COLLATION_TITLE_IGNORE_CASE,
            )
            return ordered, True
        except HostCollationUnavailable:
            return fallback, False

    def favorites(
        self,
        *,
        loaded_count: int = PAGE_SIZE,
        sort_mode: str | None = None,
        collation: HostCollation | None = None,
    ) -> PodcastEpisodePage:
        """Favorite subscriptions and episodes, exactly as Ctrl+U in AMC."""
        records = self._favorite_subscriptions()
        for episode in self._aggregate_records(
            "e.is_favorite = 1", library_only=False
        ):
            author = _author_label(_get(episode.payload, "Author", ""))
            records.append(_FavoriteRecord(
                row=_episode_row(episode, aggregate=True),
                artist=author or episode.parent_title,
            ))

        mode = sort_mode or self.favorites_sort_mode()
        if mode not in (SORT_ADDED_NEWEST, SORT_ALPHABETICAL, SORT_CUSTOM):
            mode = SORT_ADDED_NEWEST

        order_matches_amc = True
        if mode == SORT_ALPHABETICAL:
            records, order_matches_amc = self._alphabetical_favorites(
                records, collation
            )
        else:
            table = (
                "favorite_order"
                if mode == SORT_CUSTOM
                else "favorite_added_order"
            )
            stored = self._favorite_order(table)
            if stored is None:
                stored = []
                order_matches_amc = False
            records = self._manual_favorite_order(records, stored)
            if mode == SORT_ADDED_NEWEST:
                records.reverse()

        requested = max(PAGE_SIZE, _as_int(loaded_count, PAGE_SIZE))
        visible = records[:requested]
        rows = [record.row for record in visible]
        remaining = max(0, len(records) - len(visible))
        if remaining:
            rows.append(Row(
                item_id=f"podcast-load-more:favorites:{requested}",
                title="Załaduj więcej ulubionych",
                kind="loadMore",
                detail=f"pozostało {remaining}",
                show_kind=False,
            ))
        return PodcastEpisodePage(
            rows=rows,
            loaded_count=len(visible),
            has_more=bool(remaining),
            order_matches_amc=order_matches_amc,
            sort_mode=mode,
        )

    def history(self, *, loaded_count: int = PAGE_SIZE) -> PodcastEpisodePage:
        """Played podcast episodes in the persisted order of full AMC."""
        item_ids = self._history_ids()
        by_id = self._episode_records_by_ids(item_ids)
        records = [by_id[item_id] for item_id in item_ids if item_id in by_id]
        return self._aggregate_page(
            records,
            loaded_count=loaded_count,
            view="history",
            order_matches_amc=True,
        )

    def queue(self, *, loaded_count: int = PAGE_SIZE) -> PodcastEpisodePage:
        """Persisted podcast queue, with Play Next entries kept first.

        The three order tables are authoritative, exactly as in
        ``TransientQueuePersistence.Restore``. Columns in ``podcasts.db`` are
        only an earlier snapshot and therefore cannot recreate consumed rows.
        """
        stored = self._queue_ids("queue_order")
        regular = set(self._queue_ids("queue_regular_order"))
        play_next = set(self._queue_ids("queue_play_next_order"))
        legacy_regular = not regular and not play_next

        by_id = self._episode_records_by_ids(stored)
        entries: list[tuple[int, bool, _EpisodeRecord]] = []
        for position, item_id in enumerate(stored):
            record = by_id.get(item_id)
            if record is None:
                continue
            in_queue = legacy_regular or item_id in regular
            is_play_next = item_id in play_next
            if not in_queue and not is_play_next:
                continue
            entries.append((position, is_play_next, record))

        # Stable equivalent of manual order followed by
        # OrderByDescending(IsPlayNext).
        entries.sort(key=lambda entry: entry[0])
        entries.sort(key=lambda entry: not entry[1])
        return self._aggregate_page(
            [entry[2] for entry in entries],
            loaded_count=loaded_count,
            view="queue",
            order_matches_amc=True,
        )

    def _aggregate_records(
        self, where: str, *, library_only: bool = True
    ) -> list[_EpisodeRecord]:
        membership = "s.is_in_library = 1 AND " if library_only else ""
        try:
            with closing(self._open()) as connection:
                records = connection.execute(
                    f"""
                    SELECT e.id, e.subscription_id, e.title, e.published_utc_ticks, e.is_new,
                           e.is_started, e.is_played, e.download_path,
                           e.payload_json, s.title AS parent_title,
                           s.payload_json AS parent_payload_json
                    FROM podcast_episodes AS e
                    JOIN podcast_subscriptions AS s
                      ON s.id = e.subscription_id
                    WHERE {membership}({where})
                    ORDER BY e.ordinal
                    """
                ).fetchall()
        except sqlite3.Error as error:
            raise PodcastProfileError(
                f"Nie można odczytać odcinków: {error}"
            ) from error
        return [_materialize_episode(record) for record in records]

    @staticmethod
    def _title_sort(
        records: list[_EpisodeRecord],
        *,
        key,
        collation: HostCollation | None,
    ) -> tuple[list[_EpisodeRecord], bool]:
        if collation is None:
            return sorted(records, key=lambda record: key(record).casefold()), False
        try:
            return (
                collation.sort_rows(
                    records,
                    key=key,
                    mode=COLLATION_TITLE_IGNORE_CASE,
                ),
                True,
            )
        except HostCollationUnavailable:
            return sorted(records, key=lambda record: key(record).casefold()), False

    def inbox(
        self,
        *,
        loaded_count: int = PAGE_SIZE,
        sort_mode: str | None = None,
        collation: HostCollation | None = None,
    ) -> PodcastEpisodePage:
        """New and unplayed episodes of sources that remain in the library."""
        records = self._aggregate_records("e.is_new = 1 AND e.is_played = 0")
        mode = sort_mode or self.inbox_sort_mode()
        if mode not in (SORT_ADDED_NEWEST, SORT_ALPHABETICAL, SORT_CUSTOM):
            mode = SORT_ADDED_NEWEST

        # Stable sorts run from the least significant key to the primary key.
        records.sort(key=lambda record: ordinal_sort_key(record.item_id))
        order_matches_amc = True
        if mode == SORT_ALPHABETICAL:
            records.sort(key=lambda record: record.published_ticks, reverse=True)
            records, exact = self._title_sort(
                records, key=lambda record: record.title, collation=collation
            )
            order_matches_amc &= exact
        elif mode == SORT_CUSTOM:
            records, exact_episode = self._title_sort(
                records, key=lambda record: record.title, collation=collation
            )
            records.sort(key=lambda record: record.published_ticks, reverse=True)
            records, exact_parent = self._title_sort(
                records, key=lambda record: record.parent_title, collation=collation
            )
            order_matches_amc &= exact_episode and exact_parent
        else:
            records, exact = self._title_sort(
                records, key=lambda record: record.title, collation=collation
            )
            records.sort(key=lambda record: record.published_ticks, reverse=True)
            order_matches_amc &= exact

        return self._aggregate_page(
            records,
            loaded_count=loaded_count,
            view="inbox",
            order_matches_amc=order_matches_amc,
            sort_mode=mode,
        )

    def in_progress(self, *, loaded_count: int = PAGE_SIZE) -> PodcastEpisodePage:
        """Started, not-yet-played episodes, ordered like the full AMC."""
        records = self._aggregate_records("e.is_started = 1 AND e.is_played = 0")
        records.sort(
            key=lambda record: (
                _as_int(_get(record.payload, "ResumePositionTicks", 0)),
                record.published_ticks,
            ),
            reverse=True,
        )
        return self._aggregate_page(
            records,
            loaded_count=loaded_count,
            view="in-progress",
            order_matches_amc=True,
        )

    def downloads(self, *, loaded_count: int = PAGE_SIZE) -> PodcastEpisodePage:
        """Episodes whose downloaded file still exists, including archived sources."""
        records = self._aggregate_records(
            "e.download_path IS NOT NULL AND length(trim(e.download_path)) > 0",
            library_only=False,
        )
        records = [
            record for record in records
            if record.download_path and Path(record.download_path).is_file()
        ]
        # The full AMC specifies only PublishedUtcTicks descending here. The
        # SQL input order remains stable for equal publication times.
        records.sort(key=lambda record: record.published_ticks, reverse=True)
        return self._aggregate_page(
            records,
            loaded_count=loaded_count,
            view="downloads",
            order_matches_amc=True,
        )

    @staticmethod
    def _aggregate_page(
        records: list[_EpisodeRecord],
        *,
        loaded_count: int,
        view: str,
        order_matches_amc: bool,
        sort_mode: str | None = None,
    ) -> PodcastEpisodePage:
        requested = max(PAGE_SIZE, _as_int(loaded_count, PAGE_SIZE))
        visible = records[:requested]
        rows = [_episode_row(record, aggregate=True) for record in visible]
        remaining = max(0, len(records) - len(visible))
        if remaining:
            rows.append(
                _load_more_row(view=view, requested=requested, remaining=remaining)
            )
        return PodcastEpisodePage(
            rows=rows,
            loaded_count=len(visible),
            has_more=bool(remaining),
            order_matches_amc=order_matches_amc,
            sort_mode=sort_mode,
        )


def subscription_rows(subscriptions: list[PodcastSubscription]) -> list[Row]:
    return [subscription.as_row() for subscription in subscriptions]
