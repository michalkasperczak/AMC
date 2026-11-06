"""Read-only podcast/YouTube rows from the shared AMC database."""

from __future__ import annotations

import json
import sqlite3
import tempfile
from contextlib import closing
from pathlib import Path

import amc_wx_lite.podcast_source as podcast_source_module
from amc_wx_lite.podcast_source import (
    PAGE_SIZE,
    SORT_ADDED_NEWEST,
    SORT_ALPHABETICAL,
    SORT_CUSTOM,
    PodcastProfileError,
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
        ("rss-id", 0, "  Audycja\n tygodnia  ", 0, "https://example.invalid/feed", "© Autor audycji"),
        ("yt-id", 1, "Kanał dostępny", 2, "https://youtube.invalid/channel", "Kanał autora"),
        (
            "soundcloud-id", 2, "Dźwięki dostępne", 4,
            "https://feeds.soundcloud.com/users/soundcloud:users:878535568/sounds.rss",
            "Redakcja SoundCloud",
        ),
        ("hidden-id", 3, "Ukryty", 0, "https://example.invalid/hidden", ""),
    ]
    for item_id, ordinal, title, kind, feed_url, author in subscriptions:
        connection.execute(
            "INSERT INTO podcast_subscriptions VALUES (?, ?, ?, ?, ?, 0, '', ?)",
            (item_id, ordinal, title, feed_url,
             0 if item_id == "hidden-id" else 1,
             json.dumps({"SourceKind": kind, "Author": author})),
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
        "MediaLength": 62_400_000,
        "MediaType": "audio/mpeg",
        "Author": "© Autor odcinka",
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


def _history_database(base: Path, item_ids: list[str]) -> None:
    connection = sqlite3.connect(base / "library.db")
    connection.execute(
        """
        CREATE TABLE playback_history (
            session_id TEXT NOT NULL, ordinal INTEGER NOT NULL, item_id TEXT NOT NULL
        )
        """
    )
    connection.executemany(
        "INSERT INTO playback_history VALUES ('PoDcAsTs', ?, ?)",
        enumerate(item_ids),
    )
    connection.commit()
    connection.close()


def _queue_database(
    base: Path,
    *,
    order: list[str],
    regular: list[str],
    play_next: list[str],
) -> None:
    connection = sqlite3.connect(base / "library.db")
    connection.executescript(
        """
        CREATE TABLE queue_order (
            session_id TEXT NOT NULL, ordinal INTEGER NOT NULL, item_id TEXT NOT NULL
        );
        CREATE TABLE queue_regular_order (
            session_id TEXT NOT NULL, ordinal INTEGER NOT NULL, item_id TEXT NOT NULL
        );
        CREATE TABLE queue_play_next_order (
            session_id TEXT NOT NULL, ordinal INTEGER NOT NULL, item_id TEXT NOT NULL
        );
        """
    )
    for table, values in (
        ("queue_order", order),
        ("queue_regular_order", regular),
        ("queue_play_next_order", play_next),
    ):
        connection.executemany(
            f"INSERT INTO {table} VALUES ('PoDcAsTs', ?, ?)",
            enumerate(values),
        )
    connection.commit()
    connection.close()


def _playlist_database(
    base: Path,
    *,
    first_items: list[str],
    second_items: list[str],
) -> None:
    """Minimal shared-library fixture for Podcast Ctrl+P views."""
    connection = sqlite3.connect(base / "library.db")
    connection.executescript(
        """
        CREATE TABLE playlists (
            id TEXT PRIMARY KEY, session_id TEXT NOT NULL,
            ordinal INTEGER NOT NULL, name TEXT NOT NULL,
            created_utc_ticks INTEGER NOT NULL
        );
        CREATE TABLE playlist_items (
            playlist_id TEXT NOT NULL, ordinal INTEGER NOT NULL,
            item_id TEXT NOT NULL
        );
        """
    )
    connection.executemany(
        "INSERT INTO playlists VALUES (?, ?, ?, ?, ?)",
        [
            ("podcast-second", "PoDcAsTs", 1, "Do później", 2),
            ("podcast-first", "podcasts", 0, "Poranek", 1),
            ("files-private", "local", 0, "Pliki", 3),
        ],
    )
    for playlist_id, values in (
        ("podcast-first", first_items),
        ("podcast-second", second_items),
        ("files-private", ["episode-one"]),
    ):
        connection.executemany(
            "INSERT INTO playlist_items VALUES (?, ?, ?)",
            ((playlist_id, ordinal, item_id) for ordinal, item_id in enumerate(values)),
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

        assert [row.title for row in rows] == [
            "Audycja tygodnia", "Kanał dostępny", "Dźwięki dostępne"
        ]
        assert [row.detail for row in rows] == [
            "podcast", "kanał YouTube", "podcast SoundCloud"
        ]
        assert rows[2].service_kind == "soundcloudPodcast"
        assert rows[2].url.startswith("https://feeds.soundcloud.com/")
        spoken = " ".join(row.title + " " + row.detail for row in rows)
        assert "rss-id" not in spoken and "yt-id" not in spoken
        assert "soundcloud-id" not in spoken
        assert all(row.kind == "podcast" for row in rows)
        assert rows[0].url == "https://example.invalid/feed"
        assert rows[0].artist_name == "Autor audycji"
        assert rows[1].url == "https://youtube.invalid/channel"


def test_global_settings_are_read_as_plain_values_not_database_records() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        path = _database(base)
        connection = sqlite3.connect(path)
        connection.executescript(
            """
            CREATE TABLE podcast_state (
                singleton INTEGER PRIMARY KEY,
                downloads_folder TEXT NULL,
                rss_refresh_interval_minutes INTEGER NOT NULL,
                youtube_refresh_interval_minutes INTEGER NOT NULL,
                automatic_refresh_batch_size INTEGER NOT NULL
            );
            INSERT INTO podcast_state VALUES (1, 'D:\\Podcasty', 30, 180, 7);
            """
        )
        connection.commit()
        connection.close()
        settings = PodcastSource(private_sandbox(base)).global_settings()
        assert settings.downloads_folder == r"D:\Podcasty"
        assert settings.rss_refresh_interval_minutes == 30
        assert settings.youtube_refresh_interval_minutes == 180
        assert settings.automatic_refresh_batch_size == 7


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
        assert page.total_count == 2
        first = page.rows[0]
        assert first.position_seconds == 90.0
        assert "1 godz. 5 min" in first.detail
        assert "w trakcie" in first.detail
        assert first.path == "https://example.invalid/newer"
        assert first.address == "https://example.invalid/newer"
        assert first.parent_id == "rss-id"
        assert first.artist_name == "Autor odcinka"
        assert first.duration_ticks == 3_900 * 10_000_000
        assert first.podcast_media_length == 62_400_000
        assert first.podcast_media_type == "audio/mpeg"
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
        assert first.total_count == PAGE_SIZE + 1
        assert first.rows[-1].title == "Załaduj więcej odcinków"
        assert first.rows[-1].detail == (
            f"wczytano {PAGE_SIZE} z {PAGE_SIZE + 1}; "
            "Enter ładuje kolejne odcinki, najwyżej 1"
        )
        assert first.rows[0].title == "Materiał 0"
        assert expanded.loaded_count == PAGE_SIZE + 1 and not expanded.has_more
        assert expanded.total_count == PAGE_SIZE + 1
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


def test_manual_only_source_stays_in_library_but_not_in_any_inbox_order() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        path = _database(base)
        _insert_episode(path, item_id="rss-new", subscription_id="rss-id",
                        title="Podcast", ordinal=0, published=200)
        _insert_episode(path, item_id="yt-new", subscription_id="yt-id",
                        title="Film", ordinal=1, published=300)
        with closing(sqlite3.connect(path)) as connection:
            data = json.loads(connection.execute(
                "SELECT payload_json FROM podcast_subscriptions WHERE id = 'yt-id'"
            ).fetchone()[0])
            data["RefreshIntervalMinutes"] = 0
            connection.execute(
                "UPDATE podcast_subscriptions SET payload_json = ? WHERE id = 'yt-id'",
                (json.dumps(data),),
            )
            connection.commit()
        source = PodcastSource(private_sandbox(base))
        assert any(item.subscription_id == "yt-id" for item in source.subscriptions())
        for mode in (SORT_ADDED_NEWEST, SORT_ALPHABETICAL, SORT_CUSTOM):
            page = source.inbox(sort_mode=mode, collation=_ExactCasefoldCollation())
            assert [row.item_id for row in page.rows] == ["rss-new"]


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


def test_inbox_materializes_only_visible_dates_and_boundary_ties(monkeypatch) -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        path = _database(base)
        for number in range(PAGE_SIZE - 1):
            _insert_episode(
                path, item_id=f"recent-{number}", subscription_id="rss-id",
                title=f"Najnowszy {number}", ordinal=number,
                published=1000 - number,
            )
        for offset, (item_id, title) in enumerate(
            (("tie-z", "Zeta"), ("tie-a", "Alfa"), ("tie-m", "Mu"))
        ):
            _insert_episode(
                path, item_id=item_id, subscription_id="rss-id",
                title=title, ordinal=PAGE_SIZE - 1 + offset, published=10,
            )
        for number in range(48):
            _insert_episode(
                path, item_id=f"older-{number}", subscription_id="rss-id",
                title=f"Starszy {number}", ordinal=PAGE_SIZE + 2 + number,
                published=9 - number,
            )

        materialized: list[str] = []
        original = podcast_source_module._materialize_episode

        def materialize(row):
            materialized.append(row["id"])
            return original(row)

        monkeypatch.setattr(podcast_source_module, "_materialize_episode", materialize)
        page = PodcastSource(private_sandbox(base)).inbox(
            sort_mode=SORT_ADDED_NEWEST, collation=_ExactCasefoldCollation()
        )

        assert page.rows[PAGE_SIZE - 1].item_id == "tie-a"
        assert page.rows[-1].detail == "pozostało 50"
        assert len(materialized) == PAGE_SIZE + 2
        for mode in (SORT_ALPHABETICAL, SORT_CUSTOM):
            materialized.clear()
            sorted_page = PodcastSource(private_sandbox(base)).inbox(
                sort_mode=mode, collation=_ExactCasefoldCollation()
            )
            assert sorted_page.rows[-1].detail == "pozostało 50"
            assert len(materialized) == PAGE_SIZE


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


def test_history_uses_persisted_order_and_keeps_archived_episode_labels() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        path = _database(base)
        _insert_episode(
            path,
            item_id="newest-history",
            subscription_id="rss-id",
            title="Najnowszy słuchany",
            ordinal=0,
            published=300,
        )
        _insert_episode(
            path,
            item_id="archived-history",
            subscription_id="hidden-id",
            title="Archiwalny słuchany",
            ordinal=1,
            published=200,
        )
        _history_database(base, [
            "newest-history",
            "missing-private-id",
            "archived-history",
            "newest-history",
        ])

        page = PodcastSource(private_sandbox(base)).history()

        assert [row.item_id for row in page.rows] == [
            "newest-history", "archived-history"
        ]
        assert all(row.kind == "episode" for row in page.rows)
        assert "Audycja tygodnia" in page.rows[0].detail
        assert "Ukryty" in page.rows[1].detail
        spoken = " ".join(row.title + " " + row.detail for row in page.rows)
        assert "missing-private-id" not in spoken
        assert "newest-history" not in spoken
        assert "archived-history" not in spoken
        assert page.order_matches_amc


def test_queue_uses_persisted_membership_and_places_play_next_first() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        path = _database(base)
        for ordinal, item_id in enumerate(
            ("regular-b", "play-next", "regular-a", "ignored")
        ):
            _insert_episode(
                path,
                item_id=item_id,
                subscription_id="rss-id",
                title=item_id,
                ordinal=ordinal,
                published=ordinal,
            )
        _queue_database(
            base,
            order=["regular-b", "play-next", "regular-a", "missing", "ignored"],
            regular=["regular-b", "regular-a"],
            play_next=["play-next"],
        )

        page = PodcastSource(private_sandbox(base)).queue()

        assert [row.title for row in page.rows] == [
            "play-next",
            "regular-b",
            "regular-a",
        ]
        assert all(row.kind == "episode" for row in page.rows)


def test_queue_accepts_legacy_order_when_membership_tables_are_empty() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        path = _database(base)
        for ordinal, item_id in enumerate(("one", "two")):
            _insert_episode(
                path,
                item_id=item_id,
                subscription_id="rss-id",
                title=item_id,
                ordinal=ordinal,
                published=ordinal,
            )
        _queue_database(base, order=["two", "one"], regular=[], play_next=[])

        page = PodcastSource(private_sandbox(base)).queue()

        assert [row.title for row in page.rows] == ["two", "one"]


def test_podcast_playlists_preserve_names_order_and_hide_technical_ids() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        path = _database(base)
        _insert_episode(
            path,
            item_id="episode-one",
            subscription_id="rss-id",
            title="Pierwszy odcinek",
            ordinal=0,
            published=100,
        )
        _insert_episode(
            path,
            item_id="episode-two",
            subscription_id="yt-id",
            title="Drugi materiał",
            ordinal=1,
            published=200,
        )
        _playlist_database(
            base,
            first_items=["episode-two", "missing-private-id", "episode-one"],
            second_items=["episode-one"],
        )
        source = PodcastSource(private_sandbox(base))

        catalogue = source.playlists()
        contents = source.playlist("podcast-first")

        assert [row.title for row in catalogue.rows] == ["Poranek", "Do później"]
        assert [row.item_id for row in catalogue.rows] == [
            "podcast-playlist:podcast-first",
            "podcast-playlist:podcast-second",
        ]
        assert catalogue.rows[0].detail == "dostępne 2 z 3"
        assert catalogue.rows[1].detail == "1 element"
        assert [row.title for row in contents.rows] == [
            "Drugi materiał",
            "Pierwszy odcinek",
        ]
        assert contents.playlist_title == "Poranek"
        spoken = " ".join(
            row.title + " " + row.detail
            for row in catalogue.rows + contents.rows
        )
        assert "podcast-first" not in spoken
        assert "episode-one" not in spoken
        assert "episode-two" not in spoken
        assert "missing-private-id" not in spoken


def test_missing_podcast_playlist_fails_with_user_facing_text() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        _database(base)
        _playlist_database(base, first_items=[], second_items=[])

        try:
            PodcastSource(private_sandbox(base)).playlist("not-visible-id")
        except PodcastProfileError as error:
            assert str(error) == "Tej playlisty już nie ma."
            assert "not-visible-id" not in str(error)
        else:
            raise AssertionError("Brakująca playlista powinna zakończyć odczyt")


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


def test_long_multi_paragraph_description_is_not_shortened_for_nvda() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        path = _database(base)
        final_paragraph = "Końcowy akapit musi zostać przeczytany automatycznie."
        description = (
            ("Pierwszy akapit jest celowo długi. " * 20).strip()
            + "\n\n"
            + final_paragraph
        )
        connection = sqlite3.connect(path)
        payload = {
            "SourceKind": 0,
            "Description": description,
        }
        connection.execute(
            "UPDATE podcast_subscriptions SET payload_json = ? WHERE id = ?",
            (json.dumps(payload), "rss-id"),
        )
        connection.commit()
        connection.close()

        information = PodcastSource(private_sandbox(base)).description(
            "rss-id", "podcast"
        )

        assert information is not None
        assert len(information.initial_focus_name) > 500
        assert information.initial_focus_name.endswith(final_paragraph)
        assert "\n" not in information.initial_focus_name


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


def test_youtube_without_saved_description_requests_public_alt_d_metadata() -> None:
    with _temporary_folder() as folder:
        base = Path(folder)
        path = _database(base)
        _insert_episode(
            path,
            item_id="youtube-video-id",
            subscription_id="yt-id",
            title="Materiał bez zapisanego opisu",
            ordinal=0,
            published=638_900_000_000_000_000,
        )
        connection = sqlite3.connect(path)
        payload = json.loads(connection.execute(
            "SELECT payload_json FROM podcast_episodes WHERE id = ?",
            ("youtube-video-id",),
        ).fetchone()[0])
        payload["MediaUrl"] = "https://www.youtube.com/watch?v=PublicVideo1"
        payload["PageUrl"] = payload["MediaUrl"]
        connection.execute(
            "UPDATE podcast_episodes SET payload_json = ? WHERE id = ?",
            (json.dumps(payload), "youtube-video-id"),
        )
        connection.commit()
        connection.close()

        source = PodcastSource(private_sandbox(base))
        channel = source.description("yt-id", "podcast")
        video = source.description("youtube-video-id", "episode")

        assert channel is not None and video is not None
        assert channel.window_title == "Opis kanału YouTube"
        assert channel.youtube_kind == "collection"
        assert channel.youtube_address == "https://youtube.invalid/channel"
        assert channel.text.startswith("YouTube nie udostępnia opisu tego kanału.")
        assert "Kanał: Kanał autora" in channel.text
        assert video.window_title == "Opis materiału YouTube"
        assert video.youtube_kind == "video"
        assert video.youtube_address == "https://www.youtube.com/watch?v=PublicVideo1"
        assert video.text.startswith("YouTube nie udostępnia opisu tego materiału.")
        assert "Materiał YouTube" in video.metadata_text
        assert "youtube-video-id" not in channel.text + video.text


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
