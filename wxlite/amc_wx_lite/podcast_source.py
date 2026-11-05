"""Read-only podcast, SoundCloud and YouTube source for the wxPython interface.

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
from typing import TypeVar
from urllib.parse import urlparse

from .collation import (
    COLLATION_TITLE_IGNORE_CASE,
    HostCollation,
    HostCollationUnavailable,
)
from .library_activity import ordinal_sort_key
from .library_db import LibraryDatabase
from .library_views import _stored_order
from .list_model import Row
from .paging import PAGE_SIZE
from .profile_layout import ProfileLayout, resolve_layout
from .radio_source import (
    SORT_ADDED_NEWEST,
    SORT_ALPHABETICAL,
    SORT_CUSTOM,
    collection_sort_modes_from_amc_state,
)

_DOTNET_EPOCH = datetime(1, 1, 1, tzinfo=timezone.utc)
PUBLIC_INTERNET_MEDIA_SAVED_ID = "internet-media:public"


class PodcastProfileError(RuntimeError):
    """The shared podcast profile cannot be read safely."""


@dataclass(frozen=True, slots=True)
class PodcastSubscription:
    subscription_id: str
    title: str
    source_kind: int
    feed_url: str = ""
    author: str = ""
    # Trwała kolejność z ``podcast_subscriptions.ordinal``. Pozostaje danymi
    # modelu i nigdy nie wchodzi do etykiety dostępnościowej wiersza.
    ordinal: int = 0

    @property
    def kind_label(self) -> str:
        return {
            0: "podcast",
            1: "media internetowe",
            2: "kanał YouTube",
            3: "playlista YouTube",
            4: "podcast SoundCloud",
            5: "strona Facebooka",
        }.get(self.source_kind, "źródło podcastów")

    def as_row(self) -> Row:
        return Row(
            item_id=self.subscription_id,
            title=self.title,
            kind="podcast",
            # Pełne AMC udostępnia źródło tylko dla źródeł, które można
            # odświeżać. Publiczne medium internetowe (rodzaj 1) jest
            # rozwiązywane ponownie przy odtwarzaniu i nie udaje kanału.
            url=self.feed_url if self.source_kind in (0, 2, 3, 4, 5) else None,
            detail=self.kind_label,
            show_kind=False,
            artist_name=self.author,
            service_kind={
                2: "youtubeChannel",
                3: "youtubePlaylist",
                4: "soundcloudPodcast",
                5: "facebookPage",
            }.get(self.source_kind),
        )


@dataclass(frozen=True, slots=True)
class PodcastEpisodePage:
    rows: list[Row]
    loaded_count: int
    has_more: bool
    order_matches_amc: bool = True
    sort_mode: str | None = None
    total_count: int = 0


@dataclass(frozen=True, slots=True)
class PodcastPlaylistPage:
    """One playlist catalogue or one playlist's ordered episode page.

    ``playlist_id`` is navigation state only.  It is never copied into a row
    label, accessible name or announcement.
    """

    rows: list[Row]
    loaded_count: int
    has_more: bool
    playlist_id: str | None = None
    playlist_title: str = ""


@dataclass(frozen=True, slots=True)
class PodcastDescription:
    """Long-form user text for the native description dialog.

    Identifiers stay outside this value.  The dialog receives only the title,
    the requested description and intentional, human-readable metadata.
    """

    window_title: str
    text: str
    initial_focus_name: str
    metadata_text: str = ""
    youtube_address: str | None = None
    youtube_kind: str | None = None
    youtube_fallback: str = ""


@dataclass(frozen=True, slots=True)
class PodcastGlobalSettings:
    """Cztery ustawienia przeznaczone do kontrolek, bez rekordow modelu."""

    downloads_folder: str = ""
    rss_refresh_interval_minutes: int = 60
    youtube_refresh_interval_minutes: int = 60
    automatic_refresh_batch_size: int = 4


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
class _InboxSortRecord:
    item_id: str
    title: str
    parent_title: str
    published_ticks: int


_SortableRecord = TypeVar("_SortableRecord")


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
        4: "Podcast SoundCloud",
        5: "Strona Facebooka",
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
    """Expose the complete description when NVDA focuses the native dialog.

    The 500-character preview previously used here became the accessible name
    of the dialog and its text field.  NVDA therefore stopped automatically
    after that preview even though the complete description remained visible.
    Source import already applies explicit size and control-character limits,
    so this presentation layer must not shorten the user-facing text again.
    """

    return " ".join(description.split())


def complete_youtube_description(
    information: PodcastDescription,
    payload: object,
    *,
    error_message: str = "",
) -> PodcastDescription:
    """Complete an Alt+D value without exposing a model value or account.

    The host returns only a public description string.  Technical identifiers
    remain in the request and never become the dialog's accessible name.
    """

    description = ""
    if isinstance(payload, dict):
        description = _long_text(payload.get("description", ""))
    if description:
        body = description
    elif error_message:
        readable_error = _clean_text(error_message, "")[:500].rstrip()
        body = (
            "Nie udało się teraz pobrać opisu z YouTube."
            + (f" {readable_error}" if readable_error else "")
        )
    else:
        body = information.youtube_fallback or "YouTube nie udostępnia opisu tego elementu."
    return PodcastDescription(
        window_title=information.window_title,
        text=_description_text(body, [information.metadata_text]),
        initial_focus_name=_initial_focus_name(body),
        metadata_text=information.metadata_text,
    )


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
            "soundcloudpodcast": 4,
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
    if aggregate and record.source_kind in (2, 3, 5):
        detail_parts.append(
            "film z Facebooka" if record.source_kind == 5 else "materiał YouTube"
        )
    duration_ticks = max(0, _as_int(_get(record.payload, "DurationTicks", 0)))
    media_length = _as_int(_get(record.payload, "MediaLength", 0))
    media_type = str(_get(record.payload, "MediaType", "") or "").strip()
    author = _author_label(_get(record.payload, "Author", "")) or record.parent_title
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
        artist_name=author,
        service_kind=(
            "youtubeVideo" if record.source_kind in (2, 3) else None
        ),
        duration_ticks=duration_ticks,
        podcast_media_length=media_length if media_length > 0 else None,
        podcast_media_type=media_type or None,
    )


def is_supported_video_row(row: Row | None) -> bool:
    """Whether an episode is a supported public video, without speaking its URL."""

    if row is None or getattr(row, "kind", "") != "episode":
        return False
    if getattr(row, "service_kind", "") == "youtubeVideo":
        return True
    address = str(getattr(row, "url", "") or "").strip()
    try:
        host = (urlparse(address).hostname or "").casefold()
    except ValueError:
        return False
    if host in ("youtu.be", "youtube.com") or host.endswith(".youtube.com"):
        return True
    path = urlparse(address).path.strip("/").casefold()
    if host == "fb.watch":
        return bool(path)
    if host == "facebook.com" or host.endswith(".facebook.com"):
        return (
            "/videos/" in path
            or path.startswith("reel/")
            or path.startswith("watch")
            or path == "video.php"
            or "/posts/" in path
        )
    if host == "instagram.com" or host.endswith(".instagram.com"):
        parts = path.split("/")
        kind_index = 1 if len(parts) >= 3 else 0
        return (
            len(parts) >= kind_index + 2
            and parts[kind_index] in ("p", "reel", "reels", "tv")
        )
    return False


def is_youtube_video_row(row: Row | None) -> bool:
    """Compatibility name for older callers; includes supported social videos."""

    return is_supported_video_row(row)


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

    def global_settings(self) -> PodcastGlobalSettings:
        """Odczytaj ustawienia z tej samej bazy, z ktorej korzysta downloader."""
        try:
            with closing(self._open()) as connection:
                record = connection.execute(
                    """
                    SELECT downloads_folder, rss_refresh_interval_minutes,
                           youtube_refresh_interval_minutes,
                           automatic_refresh_batch_size
                    FROM podcast_state WHERE singleton = 1
                    """
                ).fetchone()
        except sqlite3.Error as error:
            raise PodcastProfileError(
                f"Nie można odczytać ustawień Podcastów i YouTube: {error}"
            ) from error
        if record is None:
            return PodcastGlobalSettings()
        return PodcastGlobalSettings(
            downloads_folder=str(record["downloads_folder"] or "").strip(),
            rss_refresh_interval_minutes=max(0, min(10_080, int(record[1]))),
            youtube_refresh_interval_minutes=max(0, min(10_080, int(record[2]))),
            automatic_refresh_batch_size=max(1, min(50, int(record[3]))),
        )

    def subscriptions(self) -> list[PodcastSubscription]:
        try:
            with closing(self._open()) as connection:
                records = connection.execute(
                    """
                    SELECT id, ordinal, title, feed_url, payload_json
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
                feed_url=str(record["feed_url"] or "").strip(),
                author=_author_label(_get(data, "Author", "")),
                ordinal=_as_int(record["ordinal"]),
            ))
        return result

    def ordered_subscriptions(
        self,
        sort_mode: str,
        *,
        collation: HostCollation | None = None,
    ) -> tuple[list[PodcastSubscription], bool]:
        """Trzy jawne porządki głównej Biblioteki bez zapisu do jej bazy.

        ``Custom`` zachowuje dokładne ``ORDER BY ordinal``. ``AddedNewest``
        odwraca tę trwałą kolejność, bo nowe źródło jest dopisywane na jej
        końcu. ``Alphabetical`` używa kluczy hosta C# zgodnych z polskim
        porównaniem. Gdy host nie może ich policzyć, zwracamy deterministyczny
        porządek zastępczy i jawnie oznaczamy go jako niezgodny.
        """

        subscriptions = self.subscriptions()
        mode = (
            sort_mode
            if sort_mode in (SORT_ADDED_NEWEST, SORT_ALPHABETICAL, SORT_CUSTOM)
            else SORT_CUSTOM
        )
        if mode == SORT_CUSTOM:
            return subscriptions, True
        if mode == SORT_ADDED_NEWEST:
            return list(reversed(subscriptions)), True

        fallback = sorted(
            subscriptions,
            key=lambda item: (
                item.title.casefold(),
                item.ordinal,
                item.subscription_id,
            ),
        )
        if collation is None:
            return fallback, False
        try:
            return (
                collation.sort_rows(
                    subscriptions,
                    key=lambda item: item.title,
                    mode=COLLATION_TITLE_IGNORE_CASE,
                ),
                True,
            )
        except HostCollationUnavailable:
            return fallback, False

    def subscription(self, subscription_id: str) -> PodcastSubscription | None:
        try:
            with closing(self._open()) as connection:
                record = connection.execute(
                    """
                    SELECT id, title, feed_url, payload_json
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
            feed_url=str(record["feed_url"] or "").strip(),
            author=_author_label(_get(data, "Author", "")),
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
                    SELECT s.id, s.title, s.feed_url, s.payload_json
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
            feed_url=str(record["feed_url"] or "").strip(),
            author=_author_label(_get(payload, "Author", "")),
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
        source_kind = _source_kind(payload)
        internet_media = source_kind == 1
        youtube = source_kind in (2, 3)
        facebook = source_kind == 5
        description = _long_text(_get(payload, "Description", ""))
        if not description and not youtube:
            return None
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
            details.append(f"{'Kanał' if youtube else 'Autor'}: {author}")
        details.append(
            f"{'Materiały' if internet_media or youtube or facebook else 'Odcinki'}: "
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
                f"{'Adres Facebooka' if facebook else 'Adres YouTube' if youtube else 'Kanał RSS lub Atom'}: {feed_url}"
            )
        if homepage:
            details.append(f"Strona: {homepage}")
        metadata_text = _description_text("", details)
        fallback = (
            "YouTube nie udostępnia opisu tego kanału."
            if source_kind == 2
            else "YouTube nie udostępnia opisu tej playlisty."
        )
        body = description or fallback
        return PodcastDescription(
            window_title=(
                "Opis kanału YouTube"
                if source_kind == 2
                else "Opis playlisty YouTube" if source_kind == 3
                else "Opis strony Facebooka" if facebook else "Opis podcastu"
            ),
            text=_description_text(body, details),
            initial_focus_name=_initial_focus_name(body),
            metadata_text=metadata_text,
            youtube_address=(homepage or feed_url) if youtube and not description else None,
            youtube_kind="collection" if youtube and not description else None,
            youtube_fallback=fallback if youtube and not description else "",
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

        parent_payload = _payload(record["parent_payload_json"])
        source_kind = _source_kind(parent_payload)
        internet_media = source_kind == 1
        youtube = source_kind in (2, 3)
        facebook = source_kind == 5
        payload = _payload(record["payload_json"])
        description = _long_text(_get(payload, "Description", ""))
        if not description and not youtube:
            return None
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
                "Materiał YouTube" if youtube else
                "Film z Facebooka" if facebook else "Odcinek podcastu"
            ),
            f"Tytuł: {title}",
        ]
        if parent_title:
            container = (
                "Kolekcja" if internet_media else (
                    _container_label(source_kind) if youtube or facebook else "Podcast"
                )
            )
            details.append(f"{container}: {parent_title}")
        if author:
            details.append(
                f"{'Strona' if facebook else 'Kanał' if internet_media or youtube else 'Autor'}: {author}"
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

        metadata_text = _description_text("", details)
        fallback = "YouTube nie udostępnia opisu tego materiału."
        body = description or fallback
        return PodcastDescription(
            window_title="Opis materiału YouTube" if youtube else "Opis odcinka",
            text=_description_text(body, details),
            initial_focus_name=_initial_focus_name(body),
            metadata_text=metadata_text,
            youtube_address=(page_url or media_url) if youtube and not description else None,
            youtube_kind="video" if youtube and not description else None,
            youtube_fallback=fallback if youtube and not description else "",
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
                           is_played, download_path, payload_json,
                           COUNT(*) OVER() AS total_count
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

        total_count = _as_int(records[0]["total_count"]) if records else 0
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
            duration_ticks = max(0, _as_int(_get(data, "DurationTicks", 0)))
            media_length = _as_int(_get(data, "MediaLength", 0))
            media_type = str(_get(data, "MediaType", "") or "").strip()
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
                artist_name=(
                    _author_label(_get(data, "Author", ""))
                    or subscription.title
                ),
                duration_ticks=duration_ticks,
                podcast_media_length=media_length if media_length > 0 else None,
                podcast_media_type=media_type or None,
            ))

        if has_more:
            next_count = min(PAGE_SIZE, max(0, total_count - len(visible)))
            rows.append(Row(
                item_id=f"podcast-load-more:{subscription_id}:{requested}",
                title="Załaduj więcej odcinków",
                kind="loadMore",
                detail=(
                    f"wczytano {len(visible)} z {total_count}; "
                    f"Enter ładuje kolejne odcinki, najwyżej {next_count}"
                ),
                show_kind=False,
            ))
        return PodcastEpisodePage(
            rows,
            len(visible),
            has_more,
            total_count=total_count,
        )

    def search_rows(self) -> list[Row]:
        """All locally cached sources and episodes, without paging markers.

        Search is run in the background by the GUI.  Loading the complete
        cached catalogue here prevents ``Ctrl+F`` from silently searching
        only the first 150 visible episodes of a podcast.  The database stays
        read-only and technical identifiers remain in :class:`Row` fields;
        only intentional titles, authors and details are later presented.
        """

        sources = subscription_rows(self.subscriptions())
        episodes = [
            _episode_row(record, aggregate=True)
            for record in self._aggregate_records("1 = 1")
        ]
        return [*sources, *episodes]

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
                    SELECT id, title, feed_url, payload_json
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
                feed_url=str(record["feed_url"] or "").strip(),
                author=_author_label(_get(payload, "Author", "")),
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

    def _podcast_playlists(self) -> list[tuple[str, str, list[str]]]:
        """Read playlist names and stored order from the shared library DB."""

        database: LibraryDatabase | None = None
        try:
            database = LibraryDatabase(self.layout.library_db)
            entries = database.connection.execute(
                """
                SELECT id, name
                FROM playlists
                WHERE session_id = 'podcasts' COLLATE NOCASE
                ORDER BY ordinal
                """
            ).fetchall()
            result: list[tuple[str, str, list[str]]] = []
            for entry in entries:
                playlist_id = str(entry[0] or "").strip()
                title = _clean_text(entry[1], "Playlista bez nazwy")
                if not playlist_id:
                    continue
                item_rows = database.connection.execute(
                    """
                    SELECT item_id
                    FROM playlist_items
                    WHERE playlist_id = ?
                    ORDER BY ordinal
                    """,
                    (playlist_id,),
                ).fetchall()
                item_ids = [
                    str(row[0]) for row in item_rows
                    if str(row[0] or "").strip()
                ]
                result.append((playlist_id, title, item_ids))
            return result
        except Exception as error:
            raise PodcastProfileError(
                f"Nie można odczytać playlist podcastów: {error}"
            ) from error
        finally:
            if database is not None:
                database.close()

    @staticmethod
    def _playlist_count_label(stored: int, available: int) -> str:
        if stored == available:
            if stored == 1:
                return "1 element"
            last_two = stored % 100
            last = stored % 10
            suffix = "elementy" if 2 <= last <= 4 and not 12 <= last_two <= 14 else "elementów"
            return f"{stored} {suffix}"
        return f"dostępne {available} z {stored}"

    def playlists(self) -> PodcastPlaylistPage:
        """Top-level Ctrl+P view for the Podcasts and YouTube session."""

        entries = self._podcast_playlists()
        requested_ids = list(dict.fromkeys(
            item_id for _, _, item_ids in entries for item_id in item_ids
        ))
        by_id = self._episode_records_by_ids(requested_ids)
        rows = [
            Row(
                item_id=f"podcast-playlist:{playlist_id}",
                title=title,
                kind="playlist",
                detail=self._playlist_count_label(
                    len(item_ids),
                    sum(1 for item_id in item_ids if item_id in by_id),
                ),
            )
            for playlist_id, title, item_ids in entries
        ]
        return PodcastPlaylistPage(
            rows=rows,
            loaded_count=len(rows),
            has_more=False,
        )

    def playlist(
        self,
        playlist_id: str,
        *,
        loaded_count: int = PAGE_SIZE,
    ) -> PodcastPlaylistPage:
        """Episodes of one AMC playlist, preserving its exact manual order."""

        entry = next(
            (
                candidate for candidate in self._podcast_playlists()
                if candidate[0] == playlist_id
            ),
            None,
        )
        if entry is None:
            raise PodcastProfileError("Tej playlisty już nie ma.")
        _, title, item_ids = entry
        by_id = self._episode_records_by_ids(item_ids)
        ordered = [by_id[item_id] for item_id in item_ids if item_id in by_id]
        requested = max(PAGE_SIZE, _as_int(loaded_count, PAGE_SIZE))
        visible = ordered[:requested]
        rows = [_episode_row(record, aggregate=True) for record in visible]
        remaining = max(0, len(ordered) - len(visible))
        if remaining:
            rows.append(Row(
                item_id=f"podcast-load-more:playlist:{playlist_id}:{requested}",
                title="Załaduj więcej elementów playlisty",
                kind="loadMore",
                detail=f"pozostało {remaining}",
                show_kind=False,
            ))
        return PodcastPlaylistPage(
            rows=rows,
            loaded_count=len(visible),
            has_more=bool(remaining),
            playlist_id=playlist_id,
            playlist_title=title,
        )

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
        records: list[_SortableRecord],
        *,
        key,
        collation: HostCollation | None,
    ) -> tuple[list[_SortableRecord], bool]:
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
        mode = sort_mode or self.inbox_sort_mode()
        if mode not in (SORT_ADDED_NEWEST, SORT_ALPHABETICAL, SORT_CUSTOM):
            mode = SORT_ADDED_NEWEST

        if mode == SORT_ADDED_NEWEST:
            records, total_count = self._recent_inbox_records(loaded_count)
        else:
            records = self._inbox_sort_records()
            total_count = len(records)

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

        if mode != SORT_ADDED_NEWEST:
            requested = max(PAGE_SIZE, _as_int(loaded_count, PAGE_SIZE))
            selected_ids = [record.item_id for record in records[:requested]]
            by_id = self._episode_records_by_ids(selected_ids)
            records = [by_id[item_id] for item_id in selected_ids if item_id in by_id]

        return self._aggregate_page(
            records,
            loaded_count=loaded_count,
            view="inbox",
            order_matches_amc=order_matches_amc,
            sort_mode=mode,
            total_count=total_count,
        )

    def _inbox_sort_records(self) -> list[_InboxSortRecord]:
        """Do sortowania całej skrzynki wystarczą cztery lekkie kolumny."""
        try:
            with closing(self._open()) as connection:
                rows = connection.execute("""
                    SELECT e.id, e.title, e.published_utc_ticks,
                           s.title AS parent_title
                    FROM podcast_episodes AS e
                    JOIN podcast_subscriptions AS s
                      ON s.id = e.subscription_id
                    WHERE e.is_new = 1 AND e.is_played = 0
                      AND s.is_in_library = 1
                """).fetchall()
        except sqlite3.Error as error:
            raise PodcastProfileError(
                f"Nie można odczytać odcinków: {error}"
            ) from error
        return [
            _InboxSortRecord(
                item_id=str(row["id"]),
                title=_clean_text(row["title"], "Odcinek bez nazwy"),
                parent_title=_clean_text(row["parent_title"], "Podcast bez nazwy"),
                published_ticks=_as_int(row["published_utc_ticks"]),
            )
            for row in rows
        ]

    def _recent_inbox_records(
        self, loaded_count: int
    ) -> tuple[list[_EpisodeRecord], int]:
        """Wczytaj bieżącą stronę i remis jej ostatniej daty, nie całą skrzynkę.

        Datę sortujemy w SQLite dzięki indeksowi skrzynki. Przy równej dacie
        kolejność tytułów nadal wyznacza istniejąca kolacja hosta, więc remis
        na granicy strony musi w całości trafić do późniejszego sortowania.
        """
        requested = max(PAGE_SIZE, _as_int(loaded_count, PAGE_SIZE))
        source = """
            FROM podcast_episodes AS e
            JOIN podcast_subscriptions AS s ON s.id = e.subscription_id
            WHERE e.is_new = 1 AND e.is_played = 0 AND s.is_in_library = 1
        """
        fields = """
            SELECT e.id, e.subscription_id, e.title, e.published_utc_ticks,
                   e.is_new, e.is_started, e.is_played, e.download_path,
                   e.payload_json, s.title AS parent_title,
                   s.payload_json AS parent_payload_json
        """
        try:
            with closing(self._open()) as connection:
                connection.execute("BEGIN")
                total_count = int(connection.execute(
                    "SELECT COUNT(*) " + source
                ).fetchone()[0])
                rows = connection.execute(
                    fields + source
                    + " ORDER BY e.published_utc_ticks DESC LIMIT ?",
                    (requested + 1,),
                ).fetchall()
                if (
                    len(rows) > requested
                    and rows[requested]["published_utc_ticks"]
                        == rows[requested - 1]["published_utc_ticks"]
                ):
                    boundary = rows[requested - 1]["published_utc_ticks"]
                    rows = connection.execute(
                        fields + source
                        + " AND e.published_utc_ticks >= ?"
                        + " ORDER BY e.published_utc_ticks DESC",
                        (boundary,),
                    ).fetchall()
        except sqlite3.Error as error:
            raise PodcastProfileError(
                f"Nie można odczytać odcinków: {error}"
            ) from error
        return [_materialize_episode(row) for row in rows], total_count

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
        total_count: int | None = None,
    ) -> PodcastEpisodePage:
        requested = max(PAGE_SIZE, _as_int(loaded_count, PAGE_SIZE))
        visible = records[:requested]
        rows = [_episode_row(record, aggregate=True) for record in visible]
        remaining = max(
            0, (len(records) if total_count is None else total_count) - len(visible)
        )
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
