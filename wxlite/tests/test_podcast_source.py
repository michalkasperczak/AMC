"""Read-only podcast/YouTube rows from the shared AMC database."""

from __future__ import annotations

import json
import sqlite3
import tempfile
from pathlib import Path

from amc_wx_lite.podcast_source import PAGE_SIZE, PodcastSource, subscription_rows
from amc_wx_lite.profile_layout import private_sandbox


def _temporary_folder() -> tempfile.TemporaryDirectory:
    """Keep SQLite fixtures on the writable project drive in a sandbox run."""
    root = Path(__file__).resolve().parents[2] / ".tmp-tests"
    root.mkdir(parents=True, exist_ok=True)
    return tempfile.TemporaryDirectory(dir=root)


def _database(base: Path) -> Path:
    path = base / "podcasts.db"
    connection = sqlite3.connect(path)
    connection.executescript(
        """
        CREATE TABLE podcast_subscriptions (
            id TEXT PRIMARY KEY, ordinal INTEGER NOT NULL, title TEXT NOT NULL,
            feed_url TEXT NOT NULL, is_in_library INTEGER NOT NULL,
            last_refresh_utc_ticks INTEGER NOT NULL, content_hash TEXT NOT NULL,
            payload_json TEXT NOT NULL
        );
        CREATE TABLE podcast_episodes (
            id TEXT PRIMARY KEY, ordinal INTEGER NOT NULL,
            subscription_id TEXT NOT NULL, title TEXT NOT NULL,
            published_utc_ticks INTEGER NOT NULL, is_new INTEGER NOT NULL,
            is_started INTEGER NOT NULL, is_played INTEGER NOT NULL,
            is_favorite INTEGER NOT NULL, is_in_queue INTEGER NOT NULL,
            is_play_next INTEGER NOT NULL, download_path TEXT NULL,
            content_hash TEXT NOT NULL, payload_json TEXT NOT NULL
        );
        """
    )
    subscriptions = [
        ("rss-id", 0, "  Audycja\n tygodnia  ", 0),
        ("yt-id", 1, "Kanał dostępny", 2),
        ("hidden-id", 2, "Ukryty", 0),
    ]
    for item_id, ordinal, title, kind in subscriptions:
        connection.execute(
            "INSERT INTO podcast_subscriptions VALUES (?, ?, ?, '', ?, 0, '', ?)",
            (item_id, ordinal, title, 0 if item_id == "hidden-id" else 1,
             json.dumps({"SourceKind": kind})),
        )
    connection.commit()
    connection.close()
    return path


def _episode_payload(
    url: str,
    *,
    feed_ordinal: int | None = None,
    resume_seconds: int = 90,
) -> str:
    payload = {
        "MediaUrl": url,
        "DurationTicks": 3_900 * 10_000_000,
        "ResumePositionTicks": resume_seconds * 10_000_000,
    }
    if feed_ordinal is not None:
        payload["FeedOrdinal"] = feed_ordinal
    return json.dumps(payload)


def _insert_episode(
    path: Path,
    *,
    item_id: str,
    subscription_id: str,
    title: str,
    ordinal: int,
    published: int,
    feed_ordinal: int | None = None,
    is_new: int = 1,
    is_started: int = 0,
    is_played: int = 0,
    resume_seconds: int = 90,
    download_path: str | None = None,
) -> None:
    connection = sqlite3.connect(path)
    connection.execute(
        """
        INSERT INTO podcast_episodes VALUES (
            ?, ?, ?, ?, ?, ?, ?, ?, 0, 0, 0, ?, '', ?
        )
        """,
        (
            item_id, ordinal, subscription_id, title, published,
            is_new, is_started, is_played,
            download_path,
            _episode_payload(
                f"https://example.invalid/{item_id}",
                feed_ordinal=feed_ordinal,
                resume_seconds=resume_seconds,
            ),
        ),
    )
    connection.commit()
    connection.close()


def test_subscription_rows_use_only_intentional_labels_and_hide_non_members() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        _database(base)
        source = PodcastSource(private_sandbox(base))

        subscriptions = source.subscriptions()
        rows = subscription_rows(subscriptions)

        assert [row.title for row in rows] == ["Audycja tygodnia", "Kanał dostępny"]
        assert [row.detail for row in rows] == ["podcast", "kanał YouTube"]
        spoken = " ".join(row.title + " " + row.detail for row in rows)
        assert "rss-id" not in spoken and "yt-id" not in spoken
        assert all(row.kind == "podcast" for row in rows)


def test_rss_episodes_follow_date_order_and_expose_resume_state() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        path = _database(base)
        _insert_episode(path, item_id="older", subscription_id="rss-id",
                        title="Starszy", ordinal=0, published=100, is_new=0)
        _insert_episode(path, item_id="newer", subscription_id="rss-id",
                        title="Nowszy", ordinal=1, published=200, is_started=1)
        page = PodcastSource(private_sandbox(base)).episodes("rss-id")

        assert [row.title for row in page.rows] == ["Nowszy", "Starszy"]
        first = page.rows[0]
        assert first.position_seconds == 90.0
        assert "1 godz. 5 min" in first.detail
        assert "w trakcie" in first.detail
        assert first.path == "https://example.invalid/newer"
        assert first.address == "https://example.invalid/newer"
        assert "newer" not in first.title and "newer" not in first.detail


def test_youtube_episodes_follow_feed_order_and_page_explicitly() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        path = _database(base)
        for number in range(PAGE_SIZE + 1):
            # Reverse insertion/date deliberately: FeedOrdinal must win.
            _insert_episode(
                path,
                item_id=f"episode-{number}",
                subscription_id="yt-id",
                title=f"Materiał {number}",
                ordinal=PAGE_SIZE - number,
                published=number,
                feed_ordinal=number,
            )
        source = PodcastSource(private_sandbox(base))
        first = source.episodes("yt-id")
        expanded = source.episodes("yt-id", loaded_count=PAGE_SIZE * 2)

        assert first.loaded_count == PAGE_SIZE and first.has_more
        assert first.rows[-1].title == "Załaduj więcej odcinków"
        assert first.rows[0].title == "Materiał 0"
        assert expanded.loaded_count == PAGE_SIZE + 1 and not expanded.has_more
        assert all(row.kind == "episode" for row in expanded.rows)


def test_connection_is_query_only() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        _database(base)
        source = PodcastSource(private_sandbox(base))
        connection = source._open()
        try:
            try:
                connection.execute("DELETE FROM podcast_subscriptions")
            except sqlite3.OperationalError:
                pass
            else:
                raise AssertionError("Biblioteka podcastów została otwarta do zapisu")
        finally:
            connection.close()


class _ExactCasefoldCollation:
    def sort_rows(self, rows, *, key, mode):
        del mode
        return sorted(rows, key=lambda row: key(row).casefold())


def test_inbox_contains_only_new_unplayed_library_items_with_parent_labels() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        path = _database(base)
        _insert_episode(path, item_id="new-rss", subscription_id="rss-id",
                        title="B odcinek", ordinal=0, published=300)
        _insert_episode(path, item_id="new-yt", subscription_id="yt-id",
                        title="A materiał", ordinal=1, published=200)
        _insert_episode(path, item_id="played", subscription_id="rss-id",
                        title="Odtworzony", ordinal=2, published=400, is_played=1)
        _insert_episode(path, item_id="old", subscription_id="rss-id",
                        title="Nienowy", ordinal=3, published=500, is_new=0)
        _insert_episode(path, item_id="hidden", subscription_id="hidden-id",
                        title="Ukryty", ordinal=4, published=600)

        page = PodcastSource(private_sandbox(base)).inbox(
            collation=_ExactCasefoldCollation()
        )

        assert [row.item_id for row in page.rows] == ["new-rss", "new-yt"]
        assert "Audycja tygodnia" in page.rows[0].detail
        assert "Kanał dostępny" in page.rows[1].detail
        assert "materiał YouTube" in page.rows[1].detail
        spoken = " ".join(row.title + " " + row.detail for row in page.rows)
        assert "new-rss" not in spoken and "new-yt" not in spoken
        assert page.order_matches_amc


def test_in_progress_is_ordered_by_resume_position_then_date() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        path = _database(base)
        _insert_episode(path, item_id="short", subscription_id="rss-id",
                        title="Krótko", ordinal=0, published=900,
                        is_new=0, is_started=1, resume_seconds=10)
        _insert_episode(path, item_id="long-old", subscription_id="rss-id",
                        title="Długo starsze", ordinal=1, published=100,
                        is_started=1, resume_seconds=200)
        _insert_episode(path, item_id="long-new", subscription_id="yt-id",
                        title="Długo nowsze", ordinal=2, published=200,
                        is_started=1, resume_seconds=200)
        _insert_episode(path, item_id="finished", subscription_id="rss-id",
                        title="Zakończony", ordinal=3, published=1000,
                        is_started=1, is_played=1, resume_seconds=400)

        page = PodcastSource(private_sandbox(base)).in_progress()

        assert [row.item_id for row in page.rows] == [
            "long-new", "long-old", "short"
        ]
        assert page.rows[0].position_seconds == 200.0
        assert page.order_matches_amc


def test_aggregate_page_reports_exact_remaining_count() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        path = _database(base)
        for number in range(PAGE_SIZE + 2):
            _insert_episode(
                path,
                item_id=f"inbox-{number}",
                subscription_id="rss-id",
                title=f"Odcinek {number}",
                ordinal=number,
                published=number,
            )
        page = PodcastSource(private_sandbox(base)).inbox(
            collation=_ExactCasefoldCollation()
        )

        assert page.loaded_count == PAGE_SIZE and page.has_more
        assert page.rows[-1].kind == "loadMore"
        assert page.rows[-1].detail == "pozostało 2"


def test_inbox_reads_the_saved_podcast_sort_mode_without_writing_state() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        _database(base)
        layout = private_sandbox(base)
        layout.state_json.write_text(json.dumps({
            "sessionNavigation": {
                "sessions": {
                    "PoDcAsTs": {
                        "collectionSortModes": {
                            "NOWE ODCINKI": "Alphabetical"
                        }
                    }
                }
            }
        }), encoding="utf-8")
        before = layout.state_json.read_bytes()

        source = PodcastSource(layout)
        assert source.inbox_sort_mode() == "Alphabetical"
        assert layout.state_json.read_bytes() == before


def test_downloads_include_only_existing_files_even_from_archived_sources() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        path = _database(base)
        newest = base / "najnowszy.mp3"
        archived = base / "archiwalny.mp3"
        newest.write_bytes(b"audio-new")
        archived.write_bytes(b"audio-archived")
        _insert_episode(
            path,
            item_id="new-download",
            subscription_id="rss-id",
            title="Najnowszy",
            ordinal=0,
            published=300,
            download_path=str(newest),
        )
        _insert_episode(
            path,
            item_id="archived-download",
            subscription_id="hidden-id",
            title="Archiwalny",
            ordinal=1,
            published=200,
            download_path=str(archived),
        )
        _insert_episode(
            path,
            item_id="missing-download",
            subscription_id="rss-id",
            title="Brak pliku",
            ordinal=2,
            published=400,
            download_path=str(base / "nie-istnieje.mp3"),
        )

        page = PodcastSource(private_sandbox(base)).downloads()

        assert [row.item_id for row in page.rows] == [
            "new-download", "archived-download"
        ]
        assert page.rows[0].path == str(newest)
        assert "Audycja tygodnia" in page.rows[0].detail
        assert "Ukryty" in page.rows[1].detail
        assert all("missing-download" not in row.item_id for row in page.rows)
