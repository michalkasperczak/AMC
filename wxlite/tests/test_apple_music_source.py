"""Apple Music resources expose only deliberate, user-facing list text."""

from __future__ import annotations

import unittest

from amc_wx_lite.apple_music_source import (
    AppleMusicPayloadError,
    records_from_resources,
)
from amc_wx_lite.list_model import ListModel


LIBRARY_ID = "i.library-song"
CATALOG_ID = "1440806041"
PRIVATE_TOKEN = "private-media-user-token"
PRIVATE_COOKIE = "media-user-token=private-session"
TECHNICAL_URL = "https://amp-api.music.apple.com/v1/me/library/songs"


def library_song(**attribute_overrides: object) -> dict[str, object]:
    attributes: dict[str, object] = {
        "name": "Pierwszy utwór",
        "artistName": "Przykładowy wykonawca",
        "albumName": "Przykładowy album",
        "durationInMillis": 185_000,
        "url": "https://music.apple.com/pl/song/example/1440806041",
        "playParams": {"catalogId": CATALOG_ID, "kind": "song"},
    }
    attributes.update(attribute_overrides)
    return {
        "id": LIBRARY_ID,
        "type": "library-songs",
        "href": TECHNICAL_URL,
        "attributes": attributes,
        "meta": {"token": PRIVATE_TOKEN},
        "cookies": PRIVATE_COOKIE,
    }


class AppleMusicSourceTests(unittest.TestCase):
    def test_library_song_keeps_ids_separate_from_accessible_text(self) -> None:
        record = records_from_resources([library_song()])[0]

        self.assertEqual(record.row.title, "Pierwszy utwór")
        self.assertEqual(record.row.kind, "track")
        self.assertEqual(record.row.detail, "Przykładowy wykonawca, 3:05")
        self.assertEqual(
            record.accessible_label,
            "Pierwszy utwór, utwór, Przykładowy wykonawca, 3:05",
        )
        self.assertEqual(str(record), record.accessible_label)
        self.assertEqual(repr(record), record.accessible_label)
        self.assertEqual(record.target.resource_id, LIBRARY_ID)
        self.assertEqual(record.target.catalog_id, CATALOG_ID)
        self.assertTrue(record.target.from_library)
        self.assertTrue(record.target.can_play)

        spoken = " ".join((record.accessible_label, record.row.detail))
        for private in (
            LIBRARY_ID,
            CATALOG_ID,
            PRIVATE_TOKEN,
            PRIVATE_COOKIE,
            TECHNICAL_URL,
            "AppleMusicRecord",
        ):
            self.assertNotIn(private, spoken)

    def test_list_columns_use_only_intentional_labels(self) -> None:
        record = records_from_resources([library_song()])[0]
        model = ListModel([record.row])
        columns = tuple(model.text_for(0, column) for column in range(3))

        self.assertEqual(
            columns,
            ("Pierwszy utwór", "utwór", "Przykładowy wykonawca, 3:05"),
        )
        joined = " ".join(columns)
        self.assertNotIn(LIBRARY_ID, joined)
        self.assertNotIn(CATALOG_ID, joined)

    def test_library_containers_have_stable_polish_kind_labels(self) -> None:
        resources = [
            {
                "id": "l.album-one",
                "type": "library-albums",
                "attributes": {"name": "Album", "artistName": "Wykonawca"},
                "relationships": {"catalog": {"data": [{"id": "123"}]}},
            },
            {
                "id": "r.artist-one",
                "type": "library-artists",
                "attributes": {"name": "Wykonawca"},
            },
            {
                "id": "p.playlist-one",
                "type": "library-playlists",
                "attributes": {"name": "Moja playlista"},
            },
        ]

        records = records_from_resources(resources)

        self.assertEqual(
            [record.accessible_label for record in records],
            [
                "Album, album, Wykonawca",
                "Wykonawca, wykonawca",
                "Moja playlista, playlista",
            ],
        )
        self.assertEqual(records[0].target.catalog_id, "123")
        self.assertFalse(any(record.target.can_play for record in records))

    def test_unknown_malformed_and_duplicate_resources_are_ignored(self) -> None:
        first = library_song()
        duplicate = library_song(name="Nie powinno się pojawić")
        resources = [
            None,
            {"id": "secret", "type": "internal-debug", "attributes": {"name": "Debug"}},
            {"id": "missing-attributes", "type": "songs"},
            {"id": "bad id with spaces", "type": "songs", "attributes": {"name": "Zły"}},
            first,
            duplicate,
        ]

        records = records_from_resources(resources)

        self.assertEqual(len(records), 1)
        self.assertEqual(records[0].row.title, "Pierwszy utwór")

    def test_controls_and_newlines_are_normalized_in_user_text(self) -> None:
        record = records_from_resources([
            library_song(
                name="  Tytuł\n  bez\x00 niespodzianki  ",
                artistName="Wykonawca\r\nDrugi człon",
            )
        ])[0]

        self.assertEqual(record.row.title, "Tytuł bez niespodzianki")
        self.assertEqual(record.row.artist_name, "Wykonawca Drugi człon")
        self.assertNotIn("\n", record.accessible_label)
        self.assertNotIn("\x00", record.accessible_label)

    def test_only_public_apple_music_https_url_is_retained(self) -> None:
        good = records_from_resources([library_song()])[0]
        api = records_from_resources([library_song(url=TECHNICAL_URL)])[0]
        foreign = records_from_resources([
            library_song(url="https://example.invalid/private")
        ])[0]
        credentials = records_from_resources([
            library_song(url="https://user:pass@music.apple.com/pl/song/example")
        ])[0]

        self.assertEqual(
            good.row.url,
            "https://music.apple.com/pl/song/example/1440806041",
        )
        self.assertIsNone(api.row.url)
        self.assertIsNone(foreign.row.url)
        self.assertIsNone(credentials.row.url)

    def test_song_with_or_without_catalog_id_cannot_start_amc_playback(self) -> None:
        record = records_from_resources([
            library_song(playParams={})
        ])[0]

        self.assertFalse(record.target.can_play)
        self.assertIsNone(record.target.catalog_id)
        self.assertEqual(
            record.row.activation_message,
            "Odtwarzanie w AMC jest niedostępne. Otwórz utwór w oryginalnym Apple Music z menu kontekstowego",
        )
        self.assertNotIn(LIBRARY_ID, record.row.activation_message)
        with_catalog_id = records_from_resources([library_song()])[0]
        self.assertTrue(with_catalog_id.target.can_play)
        self.assertEqual(
            with_catalog_id.row.activation_message,
            record.row.activation_message,
        )

    def test_catalog_resource_uses_its_id_only_as_private_target(self) -> None:
        record = records_from_resources([{
            "id": CATALOG_ID,
            "type": "songs",
            "attributes": {
                "name": "Utwór katalogowy",
                "artistName": "Wykonawca",
                "durationInMillis": 60_000,
            },
        }])[0]

        self.assertEqual(record.target.catalog_id, CATALOG_ID)
        self.assertTrue(record.target.can_play)
        self.assertFalse(record.target.from_library)
        self.assertEqual(
            record.accessible_label,
            "Utwór katalogowy, utwór, Wykonawca, 1:00",
        )
        self.assertNotIn(CATALOG_ID, record.accessible_label)

    def test_non_list_payload_has_generic_failure_without_raw_details(self) -> None:
        raw = {"errors": [{"detail": PRIVATE_TOKEN}], "next": TECHNICAL_URL}

        with self.assertRaises(AppleMusicPayloadError) as caught:
            records_from_resources(raw)

        message = str(caught.exception)
        self.assertEqual(
            message,
            "Apple Music zwróciło nieprawidłową listę materiałów.",
        )
        self.assertNotIn(PRIVATE_TOKEN, message)
        self.assertNotIn(TECHNICAL_URL, message)

    def test_boolean_and_unbounded_duration_never_become_spoken_time(self) -> None:
        boolean = records_from_resources([
            library_song(durationInMillis=True)
        ])[0]
        unbounded = records_from_resources([
            library_song(durationInMillis=999_999_999_999)
        ])[0]

        self.assertEqual(boolean.row.duration_ticks, 0)
        self.assertEqual(unbounded.row.duration_ticks, 0)
        self.assertNotIn(":", boolean.row.detail)
        self.assertNotIn(":", unbounded.row.detail)


if __name__ == "__main__":
    unittest.main()
