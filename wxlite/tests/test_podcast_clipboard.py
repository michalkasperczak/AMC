"""Ctrl+C for saved podcast episodes reads the shared SQLite payload once."""

from __future__ import annotations

import json
import sqlite3
import tempfile
from pathlib import Path
from types import SimpleNamespace

from test_gui_logic import install_wx_stub

install_wx_stub()

from amc_wx_lite.gui import LiteFrame, SearchDialog  # noqa: E402
from amc_wx_lite.list_model import Row  # noqa: E402
from amc_wx_lite.navigation import SessionId, View  # noqa: E402
from amc_wx_lite.podcast_source import PodcastProfileError, PodcastSource  # noqa: E402
from amc_wx_lite.profile_layout import private_sandbox  # noqa: E402
from amc_wx_lite.search_catalog import SearchEntry  # noqa: E402
from amc_wx_lite.command_palette import entries_for_session  # noqa: E402
from amc_wx_lite.shortcuts import Action, describe_action  # noqa: E402


def _source(base: Path) -> PodcastSource:
    database = base / "podcasts.db"
    connection = sqlite3.connect(database)
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
    connection.execute(
        "INSERT INTO podcast_subscriptions VALUES (?, 0, ?, ?, 1, 0, '', ?)",
        (
            "podcast-1",
            "Podcast",
            "https://example.invalid/feed.xml",
            json.dumps({
                "SourceKind": 0,
                "HomepageUrl": "https://example.invalid/podcast",
            }),
        ),
    )
    episodes = (
        (
            "episode-full",
            "Pełny odcinek",
            {
                "Description": "Pierwszy akapit.\n\nDrugi akapit.",
                "PageUrl": "https://example.invalid/episode",
                "MediaUrl": "https://cdn.example.invalid/audio.mp3",
            },
        ),
        (
            "episode-title-only",
            "Sam tytuł",
            {"Description": "  ", "PageUrl": "", "MediaUrl": ""},
        ),
    )
    for ordinal, (item_id, title, payload) in enumerate(episodes):
        connection.execute(
            """
            INSERT INTO podcast_episodes VALUES
            (?, ?, 'podcast-1', ?, 0, 1, 0, 0, 0, 0, 0, NULL, '', ?)
            """,
            (item_id, ordinal, title, json.dumps(payload)),
        )
    connection.commit()
    connection.close()
    return PodcastSource(private_sandbox(base))


def _frame(source: PodcastSource, rows: list[Row], captured: list[str], spoken: list[str]):
    frame = LiteFrame.__new__(LiteFrame)
    frame.navigator = SimpleNamespace(
        active=SessionId.PODCASTS,
        view=View.LIST,
        session=SimpleNamespace(view=View.LIST),
    )
    frame.podcasts = source
    frame._selected_action_rows = lambda: rows
    frame._in_download_history = lambda: False
    frame._clear_pending_external_moves = lambda: None
    frame._file_drop_path = lambda _address: None
    frame._to_clipboard = lambda text, **_kwargs: captured.append(text) or True
    frame.announcer = SimpleNamespace(say=spoken.append)
    return frame


def test_podcast_clipboard_blocks_read_full_sqlite_details_without_inventing_urls() -> None:
    with tempfile.TemporaryDirectory() as folder:
        source = _source(Path(folder))
        rows = source.episodes("podcast-1").rows

        blocks = source.clipboard_blocks(rows)

    assert blocks == [
        "\n".join((
            "Pełny odcinek",
            "Pierwszy akapit.\n\nDrugi akapit.",
            "https://example.invalid/episode",
            "https://cdn.example.invalid/audio.mp3",
            "https://example.invalid/podcast",
        )),
        "Sam tytuł\nhttps://example.invalid/podcast",
    ]


def test_ctrl_c_podcast_handler_keeps_selected_order_and_direct_url_of_ctrl_shift_c() -> None:
    with tempfile.TemporaryDirectory() as folder:
        source = _source(Path(folder))
        by_id = {row.item_id: row for row in source.episodes("podcast-1").rows}
        selected = [by_id["episode-title-only"], by_id["episode-full"]]
        copied: list[str] = []
        spoken: list[str] = []
        frame = _frame(source, selected, copied, spoken)

        LiteFrame._copy_name(frame)
        copied_name = copied.pop()
        address_frame = _frame(source, [by_id["episode-full"]], copied, spoken)
        LiteFrame._copy_address(address_frame)

    assert copied_name == (
        "Sam tytuł\nhttps://example.invalid/podcast\n\n"
        + "\n".join((
            "Pełny odcinek",
            "Pierwszy akapit.\n\nDrugi akapit.",
            "https://example.invalid/episode",
            "https://cdn.example.invalid/audio.mp3",
            "https://example.invalid/podcast",
        ))
    )
    assert copied == [
        "https://cdn.example.invalid/audio.mp3"
    ]
    assert spoken == [
        "Skopiowano szczegóły odcinków: 2 elementy",
        "Skopiowano pełną ścieżkę",
    ]


def test_search_copy_uses_the_same_podcast_blocks_without_changing_other_sessions() -> None:
    with tempfile.TemporaryDirectory() as folder:
        source = _source(Path(folder))
        episode = source.episodes("podcast-1").rows[0]
        radio = SearchEntry(
            SessionId.RADIO,
            Row("radio-1", "Radio bez zmian", "station", url="https://radio.invalid/live"),
        )
        podcast = SearchEntry(SessionId.PODCASTS, episode)
        copied: list[str] = []
        spoken: list[str] = []
        dialog = SimpleNamespace(
            _selected_entries=lambda: [radio, podcast],
            _podcast_clipboard_blocks=source.clipboard_blocks,
            _copy_text=lambda text: copied.append(text) or True,
            announcer=SimpleNamespace(say=spoken.append),
        )

        SearchDialog._copy(dialog, addresses=False)

    assert copied == [
        "Radio bez zmian\n"
        "Pełny odcinek\n"
        "Pierwszy akapit.\n\nDrugi akapit.\n"
        "https://example.invalid/episode\n"
        "https://cdn.example.invalid/audio.mp3\n"
        "https://example.invalid/podcast"
    ]
    assert spoken == ["Skopiowano nazwy: 2 elementów"]


def test_search_copy_reports_a_safe_message_when_podcast_data_cannot_be_read() -> None:
    episode = SearchEntry(
        SessionId.PODCASTS,
        Row("saved-episode", "Zapisany odcinek", "episode"),
    )
    copied: list[str] = []
    spoken: list[str] = []

    def unreadable(_rows: list[Row]) -> list[str]:
        raise PodcastProfileError("technical SQLite detail")

    dialog = SimpleNamespace(
        _selected_entries=lambda: [episode],
        _podcast_clipboard_blocks=unreadable,
        _copy_text=lambda text: copied.append(text) or True,
        announcer=SimpleNamespace(say=spoken.append),
    )

    SearchDialog._copy(dialog, addresses=False)

    assert copied == []
    assert spoken == ["Nie udało się odczytać danych odcinka do skopiowania"]


def test_podcast_copy_presentation_describes_details_in_help_and_palette() -> None:
    assert describe_action(Action.COPY_NAME, "Podcasty i YouTube") == (
        "Skopiuj tytuł, pełny opis oraz zapisane linki odcinka i podcastu"
    )
    entry = next(
        item for item in entries_for_session(SessionId.PODCASTS)
        if item.action is Action.COPY_NAME
    )
    assert entry.name == "Skopiuj szczegóły odcinka"
