from __future__ import annotations

import pytest

from amc_wx_lite.list_model import Row
from amc_wx_lite.navigation import LibraryView, SessionId
from amc_wx_lite.remote_search import (
    entries_from_payload,
    merge_saved_and_remote,
    normalize_public_address,
    public_address,
    radio_entries_from_payload,
)
from amc_wx_lite.search_catalog import SearchEntry


def test_remote_payload_creates_only_intentional_accessible_text() -> None:
    entries, warnings = entries_from_payload({
        "items": [{
            "id": "podcast-directory:youtube:private-channel-id",
            "title": "Kanał techniczny",
            "artist": "Autor",
            "kind": "podcast",
            "remoteKind": "youtubeChannel",
            "address": "https://www.youtube.com/channel/UCexample",
            "detail": "kanał YouTube",
            "durationTicks": 0,
        }],
        "warnings": ["Katalog Spreaker jest chwilowo niedostępny"],
    })

    assert warnings == ("Katalog Spreaker jest chwilowo niedostępny",)
    assert len(entries) == 1
    result = entries[0]
    assert result.remote_kind == "youtubeChannel"
    assert result.source_view is LibraryView.PODCAST_LIBRARY
    assert result.accessible_label == (
        "Kanał techniczny, podcast, Autor, kanał YouTube, Podcasty i YouTube"
    )
    assert "private-channel-id" not in result.accessible_label
    assert "SearchEntry" not in result.accessible_label


@pytest.mark.parametrize(
    "address",
    (
        "file:///C:/sekret.mp3",
        "https://uzytkownik:haslo@example.test/feed.xml",
        "javascript:alert(1)",
        "https://",
    ),
)
def test_remote_payload_rejects_non_public_or_credential_addresses(address) -> None:
    entries, _warnings = entries_from_payload({
        "items": [{
            "id": "remote",
            "title": "Nie wolno",
            "kind": "podcast",
            "remoteKind": "rss",
            "address": address,
            "detail": "Apple Podcasts",
        }],
        "warnings": [],
    })
    assert entries == []
    assert public_address(address) is None


def test_saved_catalog_wins_even_when_saved_title_does_not_match_query() -> None:
    saved = SearchEntry(
        SessionId.PODCASTS,
        Row(
            "saved-id",
            "Inna lokalna nazwa",
            "podcast",
            url="https://example.test/feed/",
        ),
        LibraryView.PODCAST_LIBRARY,
    )
    remote = SearchEntry(
        SessionId.PODCASTS,
        Row(
            "remote-id",
            "Nazwa katalogowa",
            "podcast",
            url="https://EXAMPLE.test/feed",
        ),
        LibraryView.PODCAST_LIBRARY,
        remote_kind="rss",
    )

    assert merge_saved_and_remote(
        [],
        [remote],
        saved_catalog=[saved],
    ) == []
    assert normalize_public_address(saved.row.url) == (
        normalize_public_address(remote.row.url)
    )


def test_remote_video_has_episode_semantics_without_speaking_model_kind() -> None:
    entries, _warnings = entries_from_payload({
        "items": [{
            "id": "internet-media-search:youtube:abc123",
            "title": "Transmisja",
            "artist": "Kanał",
            "kind": "episode",
            "remoteKind": "youtubeVideo",
            "address": "https://www.youtube.com/watch?v=abc123",
            "detail": "YouTube, transmisja na żywo",
            "durationTicks": 123,
        }],
        "warnings": [],
    })
    result = entries[0]
    assert result.parent_id == "remote:internet-media"
    assert result.source_view is LibraryView.PODCAST_EPISODES
    assert result.row.duration_ticks == 123
    assert result.accessible_label == (
        "Transmisja, odcinek, Kanał, YouTube, transmisja na żywo, "
        "Podcasty i YouTube"
    )


def test_soundcloud_directory_result_is_an_explicit_podcast_not_a_web_link() -> None:
    entries, warnings = entries_from_payload({
        "items": [{
            "id": "podcast-directory:apple:private-id",
            "title": "Audycja z SoundCloud",
            "artist": "Redakcja",
            "kind": "podcast",
            "remoteKind": "soundcloudPodcast",
            "address": (
                "https://feeds.soundcloud.com/users/"
                "soundcloud:users:878535568/sounds.rss"
            ),
            "detail": "podcast SoundCloud",
            "durationTicks": 0,
        }],
        "warnings": [],
    })

    assert warnings == ()
    assert len(entries) == 1
    result = entries[0]
    assert result.remote_kind == "soundcloudPodcast"
    assert result.row.service_kind == "soundcloudPodcast"
    assert result.source_view is LibraryView.PODCAST_LIBRARY
    assert result.accessible_label == (
        "Audycja z SoundCloud, podcast, Redakcja, podcast SoundCloud, "
        "Podcasty i YouTube"
    )
    assert "private-id" not in result.accessible_label


def test_invalid_contract_is_reported_without_object_repr() -> None:
    with pytest.raises(ValueError, match="Nieprawidłowa odpowiedź"):
        entries_from_payload([])


def test_radio_payload_has_user_facing_text_without_directory_identifier() -> None:
    entries, warnings = radio_entries_from_payload({
        "items": [{
            "id": "radio:private-directory-id",
            "title": "Radio Poznań",
            "kind": "station",
            "remoteKind": "radioStation",
            "address": "https://stream.example.test/radio",
            "pageAddress": "https://example.test/",
            "detail": "Polska, polski, MP3, 128 kb/s",
        }],
        "warnings": [],
    })

    assert warnings == ()
    assert len(entries) == 1
    result = entries[0]
    assert result.session_id is SessionId.RADIO
    assert result.remote_kind == "radioStation"
    assert result.accessible_label == (
        "Radio Poznań, stacja, Polska, polski, MP3, 128 kb/s, "
        "Radio"
    )
    assert "private-directory-id" not in result.accessible_label


def test_radio_payload_rejects_credentials_and_unknown_model_kind() -> None:
    entries, _warnings = radio_entries_from_payload({
        "items": [
            {
                "id": "one",
                "title": "Sekret",
                "kind": "station",
                "remoteKind": "radioStation",
                "address": "https://user:password@example.test/radio",
            },
            {
                "id": "two",
                "title": "Nieznany model",
                "kind": "station",
                "remoteKind": "RadioBrowserStationRecord",
                "address": "https://example.test/radio",
            },
        ],
        "warnings": [],
    })
    assert entries == []
