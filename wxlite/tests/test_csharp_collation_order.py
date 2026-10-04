"""Kolejnosc zgodna z ORYGINALNYM C#, mierzona na pelnym korpusie.

Dlaczego nie wlasny collator
----------------------------
Zmierzone wczesniej (``correction/collation-contract-measurement.json``):
kontrakt AMC_PL to ``CultureInfo.GetCultureInfo("pl-PL").CompareInfo.Compare``
z ``IgnoreCase|IgnoreNonSpace``, bez zadnego tie-breaku. Recznie portowany
``polish_collation`` rozjezdzal sie z nim na 10 522 z 11 203 pozycji (93,9%)
-- remisy A/a, diakrytyki, interpunkcja. Nie piszemy wlasnego collatora
Unicode: bierzemy kolejnosc z oryginalnej logiki.

Dlaczego klucze, a nie IPC po parze
-----------------------------------
11 203 tytuly to ~125 mln par. Zamiast tego host liczy ``GetSortKey`` raz na
tytul w JEDNYM wywolaniu wsadowym, a Python porownuje bajty u siebie. Zmierzone
(``resume-after422/sortkey-contract-*.json``): klucze odtwarzaja kolejnosc
``Compare`` co do pozycji (0 roznic, 0 zerwanych remisow) i daja IDENTYCZNY
wynik na Linuksie i na Windows.
"""

from __future__ import annotations

import json
import os
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite.collation import (  # noqa: E402
    HostCollation,
    HostCollationUnavailable,
    sort_key_order,
)

EVIDENCE = Path(
    os.environ.get(
        "AMC_WX_COLLATION_EVIDENCE",
        "/home/michal/projekty/amc_pomoc/wx-full-profile-after421/resume-after422",
    )
)
CORPUS = Path(
    os.environ.get(
        "AMC_WX_CORPUS",
        "/home/michal/projekty/amc_pomoc/wx-full-profile-after421/correction/corpus-full.txt",
    )
)


def _expected_order() -> tuple[list[str], list[int]]:
    payload = json.loads((EVIDENCE / "sortkey-contract-linux.json").read_text(encoding="utf-8"))
    corpus = [line for line in CORPUS.read_text(encoding="utf-8").splitlines() if line]
    return corpus, payload["order"]


requires_evidence = unittest.skipUnless(
    (EVIDENCE / "sortkey-contract-linux.json").exists() and CORPUS.exists(),
    "Brak kwitow pomiaru kolejnosci",
)


class SortKeyBytesDecideOrder(unittest.TestCase):
    """Porownanie bajtow klucza, bez wlasnych regul jezykowych."""

    def test_order_follows_key_bytes_then_length(self):
        rows = [("b", b"\x05"), ("a", b"\x03"), ("c", b"\x03\x01")]
        self.assertEqual([t for t, _ in sort_key_order(rows)], ["a", "c", "b"])

    def test_equal_keys_keep_input_order(self):
        """Remis MUSI zostac remisem -- C# AMC_PL nie ma tie-breaku."""
        rows = [("druga", b"\x07"), ("pierwsza", b"\x07")]
        self.assertEqual([t for t, _ in sort_key_order(rows)], ["druga", "pierwsza"])

    def test_missing_key_is_an_error_not_a_silent_fallback(self):
        """Cichy powrot do zlej kolejnosci jest gorszy niz blad."""
        with self.assertRaises(HostCollationUnavailable):
            sort_key_order([("a", b"\x01"), ("b", None)])


@requires_evidence
class MeasuredEvidenceIsConsistent(unittest.TestCase):
    def test_both_operating_systems_produced_the_same_order(self):
        linux = json.loads((EVIDENCE / "sortkey-contract-linux.json").read_text(encoding="utf-8"))
        windows_file = EVIDENCE / "sortkey-contract-windows.json"
        if not windows_file.exists():
            self.skipTest("Brak pomiaru z Windows")
        windows = json.loads(windows_file.read_text(encoding="utf-8"))
        self.assertEqual(linux["order"], windows["order"])
        self.assertTrue(linux["sortKeysReproduceCompareOrder"])
        self.assertTrue(windows["sortKeysReproduceCompareOrder"])
        self.assertEqual(linux["tieMismatches"], 0)
        self.assertEqual(windows["tieMismatches"], 0)

    def test_hand_written_python_collation_is_the_one_that_was_wrong(self):
        """Zabezpieczenie przed powrotem do portu 'z intuicji'."""
        from amc_wx_lite.library_db import polish_collation

        corpus, expected = _expected_order()
        import functools

        mine = sorted(range(len(corpus)), key=functools.cmp_to_key(
            lambda x, y: polish_collation(corpus[x], corpus[y])
        ))
        differing = sum(1 for i, idx in enumerate(expected) if mine[i] != idx)
        self.assertGreater(
            differing,
            1000,
            "polish_collation nagle zgadza sie z C#? Przemierz kontrakt, nie ufaj temu testowi.",
        )


@requires_evidence
class HostCollationReproducesCSharpOrderOnTheFullCorpus(unittest.TestCase):
    """Jeden sensowny finalny przebieg na 11 203 realnych tytulach."""

    @classmethod
    def setUpClass(cls) -> None:
        cls.corpus, cls.expected = _expected_order()

    def test_full_corpus_order_matches_csharp_position_by_position(self):
        keys_file = EVIDENCE / "sortkey-batch-linux.json"
        if not keys_file.exists():
            self.skipTest("Brak wsadu kluczy z hosta")
        payload = json.loads(keys_file.read_text(encoding="utf-8"))
        collation = HostCollation.from_payload(payload)

        ordered = collation.sort(self.corpus)
        expected_titles = [self.corpus[i] for i in self.expected]
        self.assertEqual(len(ordered), len(expected_titles))
        differing = sum(1 for a, b in zip(ordered, expected_titles) if a != b)
        self.assertEqual(
            differing,
            0,
            f"Kolejnosc rozjezdza sie z C# na {differing} z {len(ordered)} pozycji",
        )

    def test_one_batch_not_one_call_per_pair(self):
        """Koszt: liczba wywolan hosta nie moze rosnac z liczba par."""
        calls: list[int] = []

        def fake_call(op, args=None, timeout=20.0):
            calls.append(len(args["titles"]))
            return {
                "keys": [
                    __import__("base64").b64encode(t.encode("utf-8")).decode("ascii")
                    for t in args["titles"]
                ]
            }

        collation = HostCollation(fake_call)
        collation.load(self.corpus)
        self.assertEqual(len(calls), 1, f"Host wolany {len(calls)} razy zamiast raz")

        # Wsad jest ODCHUDZONY o powtorzenia: w realnej Bibliotece ten sam
        # tytul wystepuje wielokrotnie (rozne pliki), a klucz zalezy wylacznie
        # od tekstu. Liczymy wiec unikaty, nie wiersze.
        unique = len(dict.fromkeys(self.corpus))
        self.assertEqual(calls[0], unique)
        self.assertLess(unique, len(self.corpus), "Korpus bez powtorzen? Sprawdz dane.")

        # Drugie sortowanie NIE pyta hosta ponownie.
        collation.sort(self.corpus[:500])
        self.assertEqual(len(calls), 1, "Powtorne sortowanie wolalo hosta jeszcze raz")


@requires_evidence
class LibrarySourceActuallyUsesTheHostOrder(unittest.TestCase):
    """Zdolnosc musi byc PODLACZONA, nie tylko dostepna w module."""

    FIXTURE = Path(
        os.environ.get(
            "AMC_WX_FIXTURE",
            "/home/michal/projekty/amc_pomoc/wx-full-profile-after421/fixture",
        )
    )

    def setUp(self) -> None:
        if not (self.FIXTURE / "library.db").exists():
            self.skipTest(f"Brak kopii bazy w {self.FIXTURE}")
        from amc_wx_lite.profile_layout import private_sandbox

        self.layout = private_sandbox(self.FIXTURE)
        # Wsad dla WIERSZY listy, nie tylko dla tytulow z local_items:
        # nazwy podfolderow sa wyliczane ze SCIEZEK, wiec nie wystepuja
        # w kolumnie title i nie bylo dla nich kluczy.
        keys_file = EVIDENCE / "sortkey-batch-rows-linux.json"
        if not keys_file.exists():
            self.skipTest("Brak wsadu kluczy dla wierszy listy")
        self.payload = json.loads(keys_file.read_text(encoding="utf-8"))

    def test_without_collation_the_snapshot_admits_the_order_is_not_amc(self):
        from amc_wx_lite.library_source import LibrarySource

        snapshot = LibrarySource(self.layout).load(None)
        self.assertFalse(
            snapshot.order_matches_amc,
            "Bez hosta nie wolno twierdzic, ze kolejnosc jest zgodna z AMC",
        )

    def test_with_collation_track_rows_follow_csharp_order(self):
        from amc_wx_lite.collation import HostCollation
        from amc_wx_lite.library_source import LibrarySource

        collation = HostCollation.from_payload(self.payload)
        source = LibrarySource(self.layout, collation=collation)

        root = source.load(None)
        self.assertTrue(root.order_matches_amc)

        # Wejdz w zrodlo, gdzie sa utwory -- tam kolejnosc jest widoczna.
        folders = [r for r in root.rows if r.kind == "folder"]
        checked = 0
        for folder in folders:
            snapshot = source.load(folder.path)
            tracks = [r for r in snapshot.rows if r.kind == "track"]
            if len(tracks) < 2:
                continue
            titles = [r.title for r in tracks]
            self.assertEqual(
                titles,
                collation.sort(titles),
                f"Utwory w {folder.title} nie sa w kolejnosci C#",
            )
            checked += 1
        self.assertGreater(checked, 0, "Nie znalazlem poziomu z utworami do sprawdzenia")

    def test_group_order_is_preserved_parent_then_folders_then_tracks(self):
        from amc_wx_lite.collation import HostCollation
        from amc_wx_lite.library_source import LibrarySource

        collation = HostCollation.from_payload(self.payload)
        source = LibrarySource(self.layout, collation=collation)
        root = source.load(None)
        folder = next(r for r in root.rows if r.kind == "folder")
        snapshot = source.load(folder.path)

        kinds = [r.kind for r in snapshot.rows]
        self.assertEqual(kinds[0], "parent", "Wiersz '..' musi zostac pierwszy")
        # Zaden folder nie moze wyladowac po utworze.
        last_folder = max((i for i, k in enumerate(kinds) if k == "folder"), default=-1)
        first_track = next((i for i, k in enumerate(kinds) if k == "track"), len(kinds))
        self.assertLess(last_folder, first_track, f"Grupy sie przeplataja: {kinds[:12]}")

    def test_broken_collation_degrades_honestly_instead_of_crashing(self):
        from amc_wx_lite.collation import HostCollation
        from amc_wx_lite.library_source import LibrarySource

        def dead_host(*_a, **_k):
            raise HostCollationUnavailable("host nie zyje")

        source = LibrarySource(self.layout, collation=HostCollation(dead_host))
        snapshot = source.load(None)
        self.assertFalse(snapshot.is_empty, "Biblioteka musi sie pokazac mimo awarii kolacji")
        self.assertFalse(snapshot.order_matches_amc)


if __name__ == "__main__":
    unittest.main(verbosity=2)
