"""LibrarySource.load_view na PRAWDZIWEJ kopii profilu.

Warstwa ``library_views`` ma wlasne 40 testow; ta ich nie powtarza. Sprawdzamy
JEDNO ogniwo, ktorego tamte nie dotykaja: czy okno potrafi te widoki wczytac
swoja droga (``LibrarySource``, uchwyt na kazdy odczyt) i czy to, co z niej
wychodzi, nadaje sie dla czytnika.

Bez ``wx`` -- dziala w WSL.
"""

from __future__ import annotations

import os
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite.library_source import LibrarySource  # noqa: E402
from amc_wx_lite.profile_layout import ProfileLayout, ProfileMode  # noqa: E402

FIXTURE = Path(
    os.environ.get(
        "AMC_WX_FIXTURE",
        "/home/michal/projekty/amc_pomoc/wx-full-profile-after421/fixture",
    )
)
LIBRARY_DB = FIXTURE / "library.db"

#: Zmierzone na tej kopii profilu, nie zgadniete.
EXPECTED_ACTIVE = 2475
EXPECTED_FAVORITES = 9
EXPECTED_PLAYLISTS = 1
EXPECTED_PLAYLIST_ITEMS = 54


@unittest.skipUnless(LIBRARY_DB.exists(), f"brak kopii profilu: {LIBRARY_DB}")
class LibraryViewsThroughTheWindowSource(unittest.TestCase):
    """Cztery widoki czytane tak, jak zrobi to okno."""

    def setUp(self) -> None:
        # Kopia profilu jest TYLKO do czytania -- tryb pilnuje, zeby test
        # nie mogl przypadkiem napisac do danych uzytkownika.
        layout = ProfileLayout(
            mode=ProfileMode.READ_ONLY_MIRROR,
            library_db=LIBRARY_DB,
            podcasts_db=FIXTURE / "podcasts.db",
            state_json=FIXTURE / "state.json",
            lite_settings_dir=FIXTURE / "_lite",
        )
        self.source = LibrarySource(layout)

    def test_all_files_returns_every_active_item(self) -> None:
        result = self.source.load_view("all_files")
        self.assertEqual(len(result.rows), EXPECTED_ACTIVE)
        self.assertTrue(result.heading)

    def test_favorites_match_the_measured_profile(self) -> None:
        result = self.source.load_view("favorites")
        self.assertEqual(len(result.rows), EXPECTED_FAVORITES)

    def test_playlists_carry_a_navigable_id(self) -> None:
        """Wiersz playlisty musi dac sie ZAMIENIC na zadanie zawartosci.

        To jest to ogniwo, ktore pekalo: Id wiersza ma prefiks ``playlist:``,
        a do danych idzie samo Id.
        """
        result = self.source.load_view("playlists")
        self.assertEqual(len(result.rows), EXPECTED_PLAYLISTS)
        row = result.rows[0]
        self.assertEqual(row.kind, "playlist")
        self.assertTrue(row.item_id.startswith("playlist:"))

        playlist_id = row.item_id.split(":", 1)[1]
        contents = self.source.load_view("playlist_contents", playlist_id=playlist_id)
        self.assertIsNone(contents.fallback_view)
        self.assertEqual(len(contents.rows), EXPECTED_PLAYLIST_ITEMS)

    def test_vanished_playlist_asks_for_the_list_instead_of_failing(self) -> None:
        result = self.source.load_view("playlist_contents", playlist_id="nie-ma-takiej")
        self.assertIsNotNone(result.fallback_view)

    def test_rows_are_readable_not_a_model_repr(self) -> None:
        """Czytnik czyta tytul i rodzaj. Pusty tytul brzmi jak awaria."""
        for view in ("all_files", "favorites", "playlists"):
            with self.subTest(view=view):
                for row in self.source.load_view(view).rows[:50]:
                    self.assertTrue(row.title.strip(), f"pusty tytul w {view}")
                    self.assertNotIn("object at 0x", row.title)

    def test_unknown_view_is_refused_loudly(self) -> None:
        with self.assertRaises(ValueError):
            self.source.load_view("wymyslony")

    def test_playlist_contents_without_id_is_refused(self) -> None:
        with self.assertRaises(ValueError):
            self.source.load_view("playlist_contents")


if __name__ == "__main__":
    unittest.main()
