"""Read-only podcast/YouTube rows from the shared AMC database."""

from __future__ import annotations

import json
import sqlite3
import tempfile
from pathlib import Path

from amc_wx_lite.podcast_source import (
    PAGE_SIZE,
    SORT_ADDED_NEWEST,
    SORT_ALPHABETICAL,
    SORT_CUSTOM,
    PodcastSource,
    subscription_rows,
)
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
    is_favorite: int = 0,
    resume_seconds: int = 90,
    download_path: str | None = None,
) -> None:
    connection = sqlite3.connect(path)
    connection.execute(
        """
        INSERT INTO podcast_episodes VALUES (
            ?, ?, ?, ?, ?, ?, ?, ?, ?, 0, 0, ?, '', ?
        )
        """,
        (
            item_id, ordinal, subscription_id, title, published,
            is_new, is_started, is_played,
            is_favorite,
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


def _favorite_subscription(path: Path, item_id: str) -> None:
    connection = sqlite3.connect(path)
    raw = connection.execute(
        "SELECT payload_json FROM podcast_subscriptions WHERE id = ?",
        (item_id,),
    ).fetchone()[0]
    payload = json.loads(raw)
    payload["IsFavorite"] = True
    connection.execute(
        "UPDATE podcast_subscriptions SET payload_json = ? WHERE id = ?",
        (json.dumps(payload), item_id),
    )
    connection.commit()
    connection.close()


def _favorite_order_database(
    base: Path, *, custom: list[str], added: list[str]
) -> None:
    connection = sqlite3.connect(base / "library.db")
    connection.executescript(
        """
        CREATE TABLE favorite_order (
            session_id TEXT NOT NULL, ordinal INTEGER NOT NULL, item_id TEXT NOT NULL
        );
        CREATE TABLE favorite_added_order (
            session_id TEXT NOT NULL, ordinal INTEGER NOT NULL, item_id TEXT NOT NULL
        );
        """
    )
    connection.executemany(
        "INSERT INTO favorite_order VALUES ('podcasts', ?, ?)",
        enumerate(custom),
    )
    connection.executemany(
        "INSERT INTO favorite_added_order VALUES ('podcasts', ?, ?)",
        enumerate(added),
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
        assert first.parent_id == "rss-id"
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
        assert [row.parent_id for row in page.rows] == ["rss-id", "yt-id"]
        spoken = " ".join(row.title + " " + row.detail for row in page.rows)
        assert "new-rss" not in spoken and "new-yt" not in spoken
        assert page.order_matches_amc


def test_inbox_supports_the_three_orders_from_the_full_amc() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        path = _database(base)
        _insert_episode(path, item_id="rss-new", subscription_id="rss-id",
                        title="Zebra", ordinal=0, published=300)
        _insert_episode(path, item_id="yt-middle", subscription_id="yt-id",
                        title="Alfa", ordinal=1, published=200)
        _insert_episode(path, item_id="rss-old", subscription_id="rss-id",
                        title="Beta", ordinal=2, published=100)
        source = PodcastSource(private_sandbox(base))
        collation = _ExactCasefoldCollation()

        added = source.inbox(
            sort_mode=SORT_ADDED_NEWEST, collation=collation
        )
        alphabetical = source.inbox(
            sort_mode=SORT_ALPHABETICAL, collation=collation
        )
        by_podcast = source.inbox(
            sort_mode=SORT_CUSTOM, collation=collation
        )

        assert [row.item_id for row in added.rows] == [
            "rss-new", "yt-middle", "rss-old"
        ]
        assert [row.item_id for row in alphabetical.rows] == [
            "yt-middle", "rss-old", "rss-new"
        ]
        assert [row.item_id for row in by_podcast.rows] == [
            "rss-new", "rss-old", "yt-middle"
        ]
        assert added.sort_mode == SORT_ADDED_NEWEST
        assert alphabetical.sort_mode == SORT_ALPHABETICAL
        assert by_podcast.sort_mode == SORT_CUSTOM
        assert all(page.order_matches_amc for page in (
            added, alphabetical, by_podcast
        ))


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


def test_favorites_mix_sources_and_episodes_in_the_saved_added_order() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        path = _database(base)
        _favorite_subscription(path, "rss-id")
        _insert_episode(
            path,
            item_id="favorite-archived-episode",
            subscription_id="hidden-id",
            title="Archiwalny ulubiony odcinek",
            ordinal=0,
            published=200,
            is_favorite=1,
        )
        _insert_episode(
            path,
            item_id="ordinary-episode",
            subscription_id="rss-id",
            title="Zwykły odcinek",
            ordinal=1,
            published=300,
        )
        _favorite_order_database(
            base,
            custom=["rss-id", "favorite-archived-episode"],
            added=["rss-id", "favorite-archived-episode"],
        )

        page = PodcastSource(private_sandbox(base)).favorites()

        assert [row.item_id for row in page.rows] == [
            "favorite-archived-episode", "rss-id"
        ]
        assert [row.kind for row in page.rows] == ["episode", "podcast"]
        assert "Ukryty" in page.rows[0].detail
        spoken = " ".join(row.title + " " + row.detail for row in page.rows)
        assert "favorite-archived-episode" not in spoken
        assert "rss-id" not in spoken
        assert "ordinary-episode" not in spoken
        assert page.order_matches_amc
        assert page.sort_mode == SORT_ADDED_NEWEST


def test_favorites_support_custom_and_exact_alphabetical_order() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        path = _database(base)
        _favorite_subscription(path, "rss-id")
        _insert_episode(
            path,
            item_id="favorite-episode",
            subscription_id="yt-id",
            title="Alfa materiał",
            ordinal=0,
            published=100,
            is_favorite=1,
        )
        _favorite_order_database(
            base,
            custom=["rss-id", "favorite-episode"],
            added=["favorite-episode", "rss-id"],
        )
        source = PodcastSource(private_sandbox(base))

        custom = source.favorites(sort_mode=SORT_CUSTOM)
        alphabetical = source.favorites(
            sort_mode=SORT_ALPHABETICAL,
            collation=_ExactCasefoldCollation(),
        )

        assert [row.item_id for row in custom.rows] == [
            "rss-id", "favorite-episode"
        ]
        assert [row.item_id for row in alphabetical.rows] == [
            "favorite-episode", "rss-id"
        ]
        assert custom.order_matches_amc
        assert alphabetical.order_matches_amc


def test_favorites_name_missing_order_as_fallback_instead_of_pretending() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        path = _database(base)
        _favorite_subscription(path, "rss-id")

        page = PodcastSource(private_sandbox(base)).favorites()

        assert [row.item_id for row in page.rows] == ["rss-id"]
        assert not page.order_matches_amc


def test_full_description_starts_with_content_and_never_exposes_ids() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        path = _database(base)
        connection = sqlite3.connect(path)
        podcast_payload = {
            "SourceKind": 0,
            "Author": "© 2026 Autor audycji",
            "Description": "Pierwszy akapit.\nDrugi akapit.",
            "HomepageUrl": "https://example.invalid/podcast",
            "IsFavorite": True,
        }
        connection.execute(
            """
            UPDATE podcast_subscriptions
            SET feed_url = ?, last_refresh_utc_ticks = ?, payload_json = ?
            WHERE id = ?
            """,
            (
                "https://example.invalid/feed.xml",
                638_900_000_000_000_000,
                json.dumps(podcast_payload),
                "rss-id",
            ),
        )
        connection.commit()
        connection.close()
        _insert_episode(
            path,
            item_id="private-episode-id",
            subscription_id="rss-id",
            title="Odcinek próbny",
            ordinal=0,
            published=638_900_000_000_000_000,
        )
        connection = sqlite3.connect(path)
        episode_payload = {
            "Author": "Prowadzący",
            "Description": "Opis odcinka.\nDalsza część.",
            "MediaUrl": "https://example.invalid/audio.mp3",
            "PageUrl": "https://example.invalid/episode",
            "DurationTicks": 3_725 * 10_000_000,
        }
        connection.execute(
            "UPDATE podcast_episodes SET payload_json = ? WHERE id = ?",
            (json.dumps(episode_payload), "private-episode-id"),
        )
        connection.commit()
        connection.close()

        source = PodcastSource(private_sandbox(base))
        podcast = source.description("rss-id", "podcast")
        episode = source.description("private-episode-id", "episode")

        assert podcast is not None and episode is not None
        assert podcast.text.startswith("Pierwszy akapit.\nDrugi akapit.\n\nPodcast")
        assert "Autor: 2026 Autor audycji" in podcast.text
        assert "Odcinki: 1" in podcast.text
        assert "Opis:" not in podcast.text
        assert episode.text.startswith("Opis odcinka.\nDalsza część.\n\nOdcinek podcastu")
        assert "Podcast: Audycja tygodnia" in episode.text
        assert "Czas: 1:02:05" in episode.text
        spoken = podcast.text + episode.text + podcast.initial_focus_name
        assert "private-episode-id" not in spoken and "rss-id" not in spoken


def test_empty_description_and_archived_parent_are_reported_without_guessing() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        path = _database(base)
        _insert_episode(
            path,
            item_id="archived-episode",
            subscription_id="hidden-id",
            title="Archiwalny odcinek",
            ordinal=0,
            published=100,
        )
        source = PodcastSource(private_sandbox(base))

        assert source.description("rss-id", "podcast") is None
        assert source.description("archived-episode", "episode") is None
        assert source.related_podcast("archived-episode") is None


def test_related_podcast_returns_only_user_facing_parent_data() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        path = _database(base)
        _insert_episode(
            path,
            item_id="episode-to-open",
            subscription_id="rss-id",
            title="Wybrany odcinek",
            ordinal=0,
            published=100,
        )
        related = PodcastSource(private_sandbox(base)).related_podcast(
            "episode-to-open"
        )

        assert related is not None
        assert related.subscription_id == "rss-id"
        assert related.title == "Audycja tygodnia"
        assert "episode-to-open" not in related.title
