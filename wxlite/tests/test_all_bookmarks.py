"""Testy ZBIORCZEGO widoku wszystkich zakladek (``BookmarkIndex.GetForDisplay``).

Osobny plik, bo to osobny widok: ``bookmark_rows`` pokazuje zakladki JEDNEGO
elementu (``GetForItem``), a ``all_bookmark_rows`` -- wszystkie zakladki ze
WSZYSTKICH sesji. Mieszanie ich w jednym pliku zacieralo by to, ze maja inny
filtr, inna kolejnosc i inny kontekst wejsciowy.

Co te testy mierza:

* kolejnosc 1:1 z URUCHOMIONYM C# -- kwit ``tests/data/csharp-display-order.json``
  pochodzi z sondy .NET 8, ktora wykonuje ORYGINALNY lancuch LINQ z
  ``BookmarkIndex.cs:19-28`` razem z prawdziwymi ``StringComparer``-ami;
  klucze tekstowe i ordinalne w kwicie tez sa z .NET,
* bity ``Purpose`` (zakladka kontra rozdzial),
* kontekst pasujacy i niedopasowany (``IsCurrentItem``),
* remisy na kazdym szczeblu: nazwa sesji, tytul, pozycja, data, Id,
* brak kolatora NIE udaje zgodnosci (``order_matches_amc`` musi byc ``False``).

Czego NIE mierza: nie uruchamiaja WPF (``net8.0-windows`` nie zbuduje sie w
WSL) i nie dotykaja profilu uzytkownika. Etykiety i polaczenia SQLite sa
reuzyte z ``library_activity`` -- tutaj nie ma ich drugiej kopii.
"""

from __future__ import annotations

import json
import sqlite3
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite.collation import (  # noqa: E402
    COLLATION_ORDINAL_IGNORE_CASE,
    COLLATION_TITLE_IGNORE_CASE,
    HostCollation,
    HostCollationUnavailable,
)
from amc_wx_lite.library_activity import (  # noqa: E402
    all_bookmark_rows,
    bookmark_rows,
    ordinal_sort_key,
)
from amc_wx_lite.library_db import LibraryDatabase  # noqa: E402

#: Kwit sondy .NET. Zrodlo prawdy dla kolejnosci i dla kluczy kolacji.
RECEIPT = json.loads(
    (Path(__file__).resolve().parent / "data" / "csharp-display-order.json").read_text(
        encoding="utf-8"
    )
)

_SCHEMA = """
CREATE TABLE local_items (
    id TEXT PRIMARY KEY,
    title TEXT NOT NULL COLLATE AMC_PL,
    has_custom_title INTEGER NOT NULL,
    path TEXT NOT NULL,
    duration_ticks INTEGER NOT NULL,
    bitrate_estimated INTEGER NOT NULL,
    is_favorite INTEGER NOT NULL,
    is_in_library INTEGER NOT NULL,
    is_available INTEGER NOT NULL,
    is_in_queue INTEGER NOT NULL,
    is_play_next INTEGER NOT NULL,
    resume_mode INTEGER NOT NULL,
    resume_position_ticks INTEGER NOT NULL,
    is_radio_recording INTEGER NOT NULL
);
CREATE TABLE playback_history (
    session_id TEXT NOT NULL, ordinal INTEGER NOT NULL, item_id TEXT NOT NULL,
    PRIMARY KEY(session_id, ordinal)
);
CREATE TABLE queue_order (
    session_id TEXT NOT NULL, ordinal INTEGER NOT NULL, item_id TEXT NOT NULL,
    PRIMARY KEY(session_id, ordinal)
);
CREATE TABLE queue_regular_order (
    session_id TEXT NOT NULL, ordinal INTEGER NOT NULL, item_id TEXT NOT NULL,
    PRIMARY KEY(session_id, ordinal)
);
CREATE TABLE queue_play_next_order (
    session_id TEXT NOT NULL, ordinal INTEGER NOT NULL, item_id TEXT NOT NULL,
    PRIMARY KEY(session_id, ordinal)
);
CREATE TABLE bookmarks (
    id TEXT PRIMARY KEY,
    ordinal INTEGER NOT NULL,
    session_id TEXT NOT NULL,
    session_name TEXT NOT NULL,
    item_id TEXT NOT NULL,
    item_title TEXT NOT NULL COLLATE AMC_PL,
    name TEXT NOT NULL COLLATE AMC_PL,
    position_ticks INTEGER NOT NULL,
    created_utc_ticks INTEGER NOT NULL,
    purpose INTEGER NOT NULL DEFAULT 1,
    chapter_origin INTEGER NOT NULL DEFAULT 0,
    chapter_source_id TEXT NULL
);
CREATE TABLE metadata (key TEXT PRIMARY KEY, value TEXT NOT NULL);
"""


def _ordinal(left: str, right: str) -> int:
    return (left > right) - (left < right)


class _Builder:
    """Baza o PRAWDZIWYM schemacie fixture; writer zostaje otwarty (WAL)."""

    def __init__(self, path: Path) -> None:
        self.connection = sqlite3.connect(path, isolation_level=None)
        self.connection.create_collation("AMC_PL", _ordinal)
        self.connection.execute("PRAGMA journal_mode=WAL").fetchone()
        self.connection.executescript(_SCHEMA)
        self._ordinal = 0

    def item(self, item_id: str, title: str, path: str, *, available: bool = True) -> str:
        self.connection.execute(
            "INSERT INTO local_items (id, title, has_custom_title, path, "
            "duration_ticks, bitrate_estimated, is_favorite, is_in_library, "
            "is_available, is_in_queue, is_play_next, resume_mode, "
            "resume_position_ticks, is_radio_recording) "
            "VALUES (?, ?, 0, ?, 0, 0, 0, 1, ?, 0, 0, 0, 0, 0)",
            (item_id, title, path, int(available)),
        )
        return item_id

    def bookmark(
        self,
        bookmark_id: str,
        *,
        session: str = "local",
        session_name: str = "Pliki lokalne",
        item_id: str = "item-1",
        item_title: str = "Tytul",
        name: str = "",
        position_ticks: int = 0,
        created_utc_ticks: int = 1,
        purpose: int = 1,
    ) -> str:
        self.connection.execute(
            "INSERT INTO bookmarks(id, ordinal, session_id, session_name, item_id, "
            "item_title, name, position_ticks, created_utc_ticks, purpose, "
            "chapter_origin, chapter_source_id) "
            "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 0, NULL)",
            (
                bookmark_id,
                self._ordinal,
                session,
                session_name,
                item_id,
                item_title,
                name,
                position_ticks,
                created_utc_ticks,
                purpose,
            ),
        )
        self._ordinal += 1
        return bookmark_id

    def close(self) -> None:
        self.connection.close()


def _collation_for(*values: str) -> HostCollation:
    """Kolator z kluczami .NET dla podanych napisow (tryb TITLE_IGNORE_CASE).

    Klucze NIE sa liczone tutaj: pochodza z kwitu sondy, czyli z prawdziwego
    ``CompareInfo.GetSortKey(..., CompareOptions.IgnoreCase)`` na pl-PL. Gdyby
    test liczyl je wlasna funkcja, mierzylby sam siebie.
    """
    index = {t: k for t, k in zip(RECEIPT["texts"], RECEIPT["textKeys"])}
    missing = [v for v in values if v not in index]
    if missing:
        raise AssertionError(
            f"Kwit sondy nie ma kluczy dla {missing!r} -- dolacz je do sondy, "
            "zamiast wymyslac klucz w tescie."
        )
    return HostCollation.from_payload(
        {
            "titles": list(values),
            "keys": [index[v] for v in values],
            "mode": COLLATION_TITLE_IGNORE_CASE,
        }
    )


class _Case(unittest.TestCase):
    def setUp(self) -> None:
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.path = Path(self.tmp.name) / "library.db"
        self.build = _Builder(self.path)
        self.addCleanup(self.build.close)

    def open_db(self) -> LibraryDatabase:
        db = LibraryDatabase(self.path)
        self.addCleanup(db.close)
        return db


# ------------------------------------------------- 1. kolejnosc wzgledem C#


class DisplayOrderMatchesRunningCSharp(_Case):
    """Pelny przebieg przeciw kwitowi z URUCHOMIONEJ sondy .NET."""

    def _load_receipt_entries(self) -> None:
        for entry in RECEIPT["entriesEcho"]:
            self.build.bookmark(
                entry["id"],
                session=entry["sessionId"],
                session_name=entry["sessionName"],
                item_id=entry["itemId"],
                item_title=entry["itemTitle"],
                position_ticks=entry["positionTicks"],
                created_utc_ticks=entry["createdUtcTicks"],
                purpose=entry["purpose"],
            )

    def _collation(self) -> HostCollation:
        return HostCollation.from_payload(
            {
                "titles": RECEIPT["texts"],
                "keys": RECEIPT["textKeys"],
                "mode": COLLATION_TITLE_IGNORE_CASE,
            }
        )

    def test_probe_receipt_is_the_one_we_expect(self):
        """Kwit musi byc tym zmierzonym, a nie dowolnym plikiem o tej nazwie."""
        self.assertEqual(RECEIPT["schema"], "amc-wx-all-bookmarks/display-order/1")
        self.assertEqual(RECEIPT["culture"], "pl-PL")
        # Klucz tekstowy i ordinalny ZGADZAJA sie z oryginalnymi komparatorami.
        self.assertEqual(RECEIPT["titleKeyPairMismatchesVsCurrentCultureIgnoreCase"], 0)
        self.assertEqual(RECEIPT["ordinalUtf16BePairMismatchesVsOrdinal"], 0)
        # ...a odrzuceni kandydaci NIE. To dowod, ze wybor nie jest dowolny.
        self.assertGreater(RECEIPT["ordinalUtf8PairMismatchesVsOrdinal"], 0)
        self.assertGreater(RECEIPT["ordinalIgnoreCaseHostPairMismatchesVsOrdinal"], 0)

    def test_every_context_from_receipt(self):
        self._load_receipt_entries()
        db = self.open_db()
        collation = self._collation()
        for context in RECEIPT["contexts"]:
            with self.subTest(context["label"]):
                result = all_bookmark_rows(
                    db,
                    current_session_id=context["currentSessionId"],
                    current_item_id=context["currentItemId"],
                    collation=collation,
                )
                self.assertTrue(result.order_matches_amc)
                self.assertEqual(
                    [b.bookmark_id for b in result.bookmarks], context["order"]
                )
                self.assertEqual(
                    sum(1 for d in result.display if d.is_current_item),
                    context["currentItemBookmarks"],
                )

    def test_drops_non_bookmark_purpose(self):
        self._load_receipt_entries()
        result = all_bookmark_rows(
            self.open_db(),
            current_session_id="local",
            current_item_id="item-biez",
            collation=self._collation(),
        )
        self.assertEqual(len(result.bookmarks), RECEIPT["bookmarkEntries"])
        self.assertNotIn("purpose-zero", [b.bookmark_id for b in result.bookmarks])


# --------------------------------------------------------- 2. bity Purpose


class PurposeBits(_Case):
    def test_chapter_only_is_hidden_but_not_deleted(self):
        """Czysty ``Chapter`` (2) nie jest zakladka -- tylko niewidoczny."""
        self.build.bookmark("czysty-rozdzial", purpose=2)
        self.build.bookmark("zakladka", purpose=1)
        result = all_bookmark_rows(
            self.open_db(),
            current_session_id="",
            current_item_id="",
            collation=_collation_for("Pliki lokalne", "Tytul"),
        )
        self.assertEqual([b.bookmark_id for b in result.bookmarks], ["zakladka"])
        # Wpis nadal jest w bazie: to filtr WIDOKU, nie usuwanie.
        self.assertEqual(
            self.open_db().connection.execute(
                "SELECT COUNT(*) FROM bookmarks"
            ).fetchone()[0],
            2,
        )

    def test_both_bits_is_still_a_bookmark(self):
        """``Purpose`` 3 ma bit ``Bookmark``, wiec ``IsBookmark`` przepuszcza."""
        self.build.bookmark("oba-bity", purpose=3, position_ticks=10)
        result = all_bookmark_rows(
            self.open_db(),
            current_session_id="",
            current_item_id="",
            collation=_collation_for("Pliki lokalne", "Tytul"),
        )
        self.assertEqual([b.bookmark_id for b in result.bookmarks], ["oba-bity"])

    def test_purpose_zero_is_never_visible(self):
        self.build.bookmark("zero", purpose=0)
        result = all_bookmark_rows(
            self.open_db(),
            current_session_id="",
            current_item_id="",
            collation=_collation_for("Pliki lokalne", "Tytul"),
        )
        self.assertEqual(result.bookmarks, ())
        self.assertTrue(result.is_empty)


# -------------------------------------------------------------- 3. kontekst


class CurrentItemContext(_Case):
    """``IsCurrentItem``: sesja ``OrdinalIgnoreCase``, element ``Ordinal``."""

    def _two(self) -> None:
        # Tytul "Zzz..." przegralby alfabetycznie, wiec jego wejscie na gore
        # moze pochodzic TYLKO z pierwszego klucza (biezacy material).
        self.build.bookmark(
            "biezacy",
            item_id="item-biez",
            item_title="Zzz ostatni alfabetycznie",
            position_ticks=99,
        )
        self.build.bookmark("inny", item_id="item-inny", item_title="Aaa pierwszy")

    def test_matching_context_puts_current_item_first(self):
        self._two()
        result = all_bookmark_rows(
            self.open_db(),
            current_session_id="local",
            current_item_id="item-biez",
            collation=_collation_for(
                "Pliki lokalne", "Zzz ostatni alfabetycznie", "Aaa pierwszy"
            ),
        )
        self.assertEqual([b.bookmark_id for b in result.bookmarks], ["biezacy", "inny"])
        self.assertEqual([d.is_current_item for d in result.display], [True, False])

    def test_unmatched_context_falls_back_to_alphabet(self):
        self._two()
        result = all_bookmark_rows(
            self.open_db(),
            current_session_id="local",
            current_item_id="nie-ma-takiego",
            collation=_collation_for(
                "Pliki lokalne", "Zzz ostatni alfabetycznie", "Aaa pierwszy"
            ),
        )
        self.assertEqual([b.bookmark_id for b in result.bookmarks], ["inny", "biezacy"])
        self.assertEqual([d.is_current_item for d in result.display], [False, False])

    def test_session_id_ignores_case_but_item_id_does_not(self):
        """Dwa ROZNE porownania w jednym warunku (``BookmarkIndex.cs:182-187``)."""
        self._two()
        collation = _collation_for(
            "Pliki lokalne", "Zzz ostatni alfabetycznie", "Aaa pierwszy"
        )
        db = self.open_db()
        # Sesja inna wielkoscia liter -> NADAL pasuje.
        upper_session = all_bookmark_rows(
            db,
            current_session_id="LoCaL",
            current_item_id="item-biez",
            collation=collation,
        )
        self.assertEqual([d.is_current_item for d in upper_session.display], [True, False])
        # Id elementu inna wielkoscia liter -> NIE pasuje (``Ordinal``).
        upper_item = all_bookmark_rows(
            db,
            current_session_id="local",
            current_item_id="ITEM-BIEZ",
            collation=collation,
        )
        self.assertEqual([d.is_current_item for d in upper_item.display], [False, False])

    def test_context_is_explicit_not_guessed(self):
        """API bierze kontekst JAWNIE; pusty kontekst jest legalny i nie pasuje."""
        self._two()
        result = all_bookmark_rows(
            self.open_db(),
            current_session_id="",
            current_item_id="",
            collation=_collation_for(
                "Pliki lokalne", "Zzz ostatni alfabetycznie", "Aaa pierwszy"
            ),
        )
        self.assertEqual([d.is_current_item for d in result.display], [False, False])


# ---------------------------------------------------------------- 4. remisy


class TieBreaks(_Case):
    def test_session_name_before_item_title(self):
        self.build.bookmark("b-sesja", session="s2", session_name="Bbb sesja",
                            item_title="Aaa tytul")
        self.build.bookmark("a-sesja", session="s1", session_name="Aaa sesja",
                            item_title="Zzz tytul")
        result = all_bookmark_rows(
            self.open_db(),
            current_session_id="",
            current_item_id="",
            collation=_collation_for(
                "Aaa sesja", "Bbb sesja", "Aaa tytul", "Zzz tytul"
            ),
        )
        self.assertEqual([b.bookmark_id for b in result.bookmarks], ["a-sesja", "b-sesja"])

    def test_accent_is_not_ignored_in_text_keys(self):
        """``CurrentCultureIgnoreCase`` ignoruje WIELKOSC, nie akcenty.

        ``AMC_PL`` (``IgnoreCase|IgnoreNonSpace``) zrownalby ``"etap"`` z
        ``"étap"`` i remis rozstrzygnelby dopiero ``PositionTicks`` -- czyli
        inna kolejnosc. Dlatego ten widok musi uzyc ``TITLE_IGNORE_CASE``.
        """
        self.build.bookmark("accent", item_title="étap", position_ticks=1)
        self.build.bookmark("upper", item_title="ETAP", position_ticks=2)
        self.build.bookmark("plain", item_title="etap", position_ticks=3)
        result = all_bookmark_rows(
            self.open_db(),
            current_session_id="",
            current_item_id="",
            collation=_collation_for("Pliki lokalne", "etap", "ETAP", "étap"),
        )
        # Kolejnosc z kwitu .NET: "etap" == "ETAP" (remis -> pozycja), potem "étap".
        self.assertEqual(
            [b.bookmark_id for b in result.bookmarks], ["upper", "plain", "accent"]
        )

    def test_position_then_created_then_id(self):
        same = dict(session="c", session_name="Sesja C", item_title="Ten sam tytul")
        self.build.bookmark("p-late", position_ticks=120, created_utc_ticks=1, **same)
        self.build.bookmark("p-early", position_ticks=5, created_utc_ticks=9, **same)
        self.build.bookmark("d-new", position_ticks=60, created_utc_ticks=5, **same)
        self.build.bookmark("d-old", position_ticks=60, created_utc_ticks=1, **same)
        result = all_bookmark_rows(
            self.open_db(),
            current_session_id="",
            current_item_id="",
            collation=_collation_for("Sesja C", "Ten sam tytul"),
        )
        self.assertEqual(
            [b.bookmark_id for b in result.bookmarks],
            ["p-early", "d-old", "d-new", "p-late"],
        )

    def test_last_tie_is_case_sensitive_ordinal(self):
        """``StringComparer.Ordinal``: ``"B"`` (0x42) PRZED ``"a"`` (0x61).

        Gdyby tie-break uzyl hostowego ``ORDINAL_IGNORE_CASE``, oba Id bylyby
        rowne i kolejnosc zalezalaby od przypadku (kolejnosci z SQL).
        """
        same = dict(
            session="c", session_name="Sesja C", item_title="Ten sam tytul",
            position_ticks=30, created_utc_ticks=1,
        )
        self.build.bookmark("a-maly", **same)
        self.build.bookmark("B-duzy", **same)
        result = all_bookmark_rows(
            self.open_db(),
            current_session_id="",
            current_item_id="",
            collation=_collation_for("Sesja C", "Ten sam tytul"),
        )
        self.assertEqual([b.bookmark_id for b in result.bookmarks], ["B-duzy", "a-maly"])

    def test_ordinal_key_is_utf16_not_codepoints(self):
        """Rog Unicode: para zastepcza kontra gorny zakres BMP.

        W jednostkach UTF-16 ``U+1F600`` zaczyna sie od ``D83D`` i jest
        MNIEJSZE od ``U+FFFD``; w punktach kodowych (tak porownuje ``str``
        Pythona i UTF-8) jest WIEKSZE. ``Ordinal`` to UTF-16, wiec klucz musi
        byc ``utf-16-be``.
        """
        self.assertLess(ordinal_sort_key("\U0001F600"), ordinal_sort_key("\uFFFD"))
        self.assertGreater("\U0001F600", "\uFFFD")  # Python: odwrotnie
        self.assertLess(ordinal_sort_key("B"), ordinal_sort_key("a"))
        self.assertEqual(ordinal_sort_key(""), b"")

    def test_unicode_tie_order_follows_utf16(self):
        same = dict(
            session="c", session_name="Sesja C", item_title="Unicode w Id",
            position_ticks=70, created_utc_ticks=1,
        )
        self.build.bookmark("\uFFFD-bmp", **same)
        self.build.bookmark("\U0001F600-emoji", **same)
        result = all_bookmark_rows(
            self.open_db(),
            current_session_id="",
            current_item_id="",
            collation=_collation_for("Sesja C", "Unicode w Id"),
        )
        self.assertEqual(
            [b.bookmark_id for b in result.bookmarks],
            ["\U0001F600-emoji", "\uFFFD-bmp"],
        )


# ------------------------------------------- 5. kolator: brak nie udaje zgody


class CollationHonesty(_Case):
    def test_no_collation_is_not_a_match(self):
        self.build.bookmark("z", item_title="Zzz")
        self.build.bookmark("a", item_title="Aaa")
        result = all_bookmark_rows(
            self.open_db(),
            current_session_id="",
            current_item_id="",
            collation=None,
        )
        self.assertFalse(result.order_matches_amc)
        # Wiersze SA, ale w kolejnosci zastepczej (zapis), nie udawanej C#.
        self.assertEqual([b.bookmark_id for b in result.bookmarks], ["z", "a"])

    def test_missing_key_refuses_instead_of_sorting_wrong(self):
        self.build.bookmark("a", item_title="etap")
        collation = _collation_for("Pliki lokalne")  # brak klucza dla tytulu
        with self.assertRaises(HostCollationUnavailable):
            all_bookmark_rows(
                self.open_db(),
                current_session_id="",
                current_item_id="",
                collation=collation,
            )

    def test_rejects_wrong_collation_mode(self):
        """``AMC_PL`` i ``ORDINAL_IGNORE_CASE`` to NIE ten tryb co widok."""
        self.build.bookmark("a", item_title="etap")
        index = {t: k for t, k in zip(RECEIPT["texts"], RECEIPT["textKeys"])}
        wrong = HostCollation.from_payload(
            {
                "titles": ["Pliki lokalne", "etap"],
                "keys": [index["Pliki lokalne"], index["etap"]],
                "mode": COLLATION_ORDINAL_IGNORE_CASE,
            }
        )
        with self.assertRaises(HostCollationUnavailable):
            all_bookmark_rows(
                self.open_db(),
                current_session_id="",
                current_item_id="",
                collation=wrong,
            )


# ------------------------------------- 6. zakres: wszystkie sesje, bez filtra


class ScopeAndIntegrationContract(_Case):
    def test_shows_every_session_not_only_local(self):
        self.build.bookmark("l", session="local", session_name="Pliki lokalne",
                            item_title="Aaa pierwszy")
        self.build.bookmark("t", session="tidal", session_name="TIDAL",
                            item_title="Zzz tidal")
        self.build.bookmark("p", session="podcasts", session_name="Podcasty",
                            item_title="Zzz podcast")
        result = all_bookmark_rows(
            self.open_db(),
            current_session_id="",
            current_item_id="",
            collation=_collation_for(
                "Pliki lokalne", "TIDAL", "Podcasty",
                "Aaa pierwszy", "Zzz tidal", "Zzz podcast",
            ),
        )
        self.assertEqual({b.session_id for b in result.bookmarks},
                         {"local", "tidal", "podcasts"})
        # Kolejnosc po NAZWIE sesji: "Pliki lokalne" < "Podcasty" < "TIDAL".
        self.assertEqual([b.bookmark_id for b in result.bookmarks], ["l", "p", "t"])

    def test_keeps_entries_whose_item_is_gone(self):
        """Brak pliku NIE ukrywa zakladki -- oryginal pokazuje zapisany tytul.

        To rozni ten widok od historii: tam brak w katalogu usuwa wiersz,
        tutaj ``CreateBookmarkRow`` robi zastepczy ``MediaItem``
        (``MainWindow.xaml.cs:13749-13753``).
        """
        self.build.item("jest", "Jest w bibliotece", "/x/jest.mp3")
        self.build.bookmark("ma-plik", item_id="jest", item_title="Aaa jest")
        self.build.bookmark("nie-ma-pliku", item_id="znikl", item_title="Bbb znikl")
        result = all_bookmark_rows(
            self.open_db(),
            current_session_id="",
            current_item_id="",
            collation=_collation_for("Pliki lokalne", "Aaa jest", "Bbb znikl"),
        )
        self.assertEqual(
            [b.bookmark_id for b in result.bookmarks], ["ma-plik", "nie-ma-pliku"]
        )
        self.assertEqual(result.missing_item_count, 0)
        self.assertIn("Bbb znikl", result.rows[1].title)

    def test_returns_explicit_keys_for_integration(self):
        """Dalsza integracja potrzebuje sesji, elementu i pozycji -- JAWNIE."""
        self.build.bookmark(
            "bm", session="tidal", session_name="TIDAL", item_id="track-7",
            item_title="Tytul", position_ticks=18_500_000,
        )
        result = all_bookmark_rows(
            self.open_db(),
            current_session_id="",
            current_item_id="",
            collation=_collation_for("TIDAL", "Tytul"),
        )
        display = result.display[0]
        self.assertEqual(display.bookmark.session_id, "tidal")
        self.assertEqual(display.bookmark.item_id, "track-7")
        self.assertEqual(display.bookmark.position_ticks, 18_500_000)
        self.assertAlmostEqual(display.position_seconds, 1.85)
        # Klucz wiersza jest wlasny, zeby dwie zakladki sie nie zlaly.
        self.assertEqual(display.bookmark.row.item_id, "bookmark:bm")
        # Obca sesja nie ma kanalu odtwarzania w tym przyroscie -- mowimy to
        # wprost, zamiast obiecywac skok, ktorego nie ma.
        self.assertFalse(display.can_play_locally)

    def test_local_session_entry_is_playable(self):
        self.build.bookmark("bm", session="LOCAL", item_title="Tytul")
        result = all_bookmark_rows(
            self.open_db(),
            current_session_id="",
            current_item_id="",
            collation=_collation_for("Pliki lokalne", "Tytul"),
        )
        self.assertTrue(result.display[0].can_play_locally)

    def test_does_not_reuse_single_item_heading(self):
        """Zbiorczy widok to NIE zakladki jednego pliku; naglowek musi roznic."""
        self.build.bookmark("bm", item_id="item-1", item_title="Tytul")
        db = self.open_db()
        collective = all_bookmark_rows(
            db, current_session_id="", current_item_id="",
            collation=_collation_for("Pliki lokalne", "Tytul"),
        )
        single = bookmark_rows(db, item_id="item-1")
        self.assertNotEqual(collective.heading, single.heading)
        self.assertEqual(single.heading, "Biblioteka — Zakładki")

    def test_label_is_identical_to_single_item_view(self):
        """Etykieta nie jest tu pisana drugi raz -- to ta sama funkcja."""
        self.build.bookmark(
            "bm", item_id="item-1", item_title="Tytul", name="moja",
            position_ticks=65 * 10_000_000, created_utc_ticks=639_040_752_000_000_000,
        )
        db = self.open_db()
        collective = all_bookmark_rows(
            db, current_session_id="", current_item_id="",
            collation=_collation_for("Pliki lokalne", "Tytul"),
        )
        single = bookmark_rows(db, item_id="item-1")
        self.assertEqual(collective.rows[0].title, single.rows[0].title)
        self.assertIn("1:05", collective.rows[0].title)
        self.assertIn("moja", collective.rows[0].title)
        self.assertIn("zakładka", collective.rows[0].title)

    def test_reports_live_write_visibility(self):
        self.build.bookmark("bm", item_title="Tytul")
        result = all_bookmark_rows(
            self.open_db(),
            current_session_id="",
            current_item_id="",
            collation=_collation_for("Pliki lokalne", "Tytul"),
        )
        self.assertTrue(result.sees_live_writes)

    def test_read_only_never_writes(self):
        import hashlib

        self.build.bookmark("bm", item_title="Tytul")
        self.build.connection.execute("PRAGMA wal_checkpoint(TRUNCATE)").fetchone()
        before = hashlib.sha256(self.path.read_bytes()).hexdigest()
        all_bookmark_rows(
            self.open_db(),
            current_session_id="",
            current_item_id="",
            collation=_collation_for("Pliki lokalne", "Tytul"),
        )
        self.assertEqual(hashlib.sha256(self.path.read_bytes()).hexdigest(), before)
