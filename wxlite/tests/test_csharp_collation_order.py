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
Nie dlatego, ze "to 125 mln par" -- sortowanie nie porownuje wszystkich par,
tylko okolo N log N, czyli ~150 tys. razy. Powod jest taki, ze kazde z tych
~150 tys. porownan byloby OSOBNA runda IPC blokujaca watek GUI. Host liczy
wiec ``GetSortKey`` raz na tytul (11 203 wywolania) w wywolaniach WSADOWYCH
(chunki <= 64 KiB JSON-a, bo jedna linia ze wszystkimi tytulami nie zmiescilaby
sie w buforze linii), a Python porownuje bajty u siebie. Zmierzone
(``resume-after422/sortkey-contract-*.json``): klucze odtwarzaja kolejnosc
``Compare`` co do pozycji (0 roznic, 0 zerwanych remisow) i daja IDENTYCZNY
wynik na Linuksie i na Windows.
"""

from __future__ import annotations

import base64
import json
import os
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite import collation as collation_module  # noqa: E402
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

    def test_batched_not_one_call_per_pair(self):
        """Koszt: liczba wywolan hosta nie moze rosnac z liczba PAR.

        Wczesniej ten test wymagal DOKLADNIE jednego wywolania. Pomiar na
        zywym hoscie pokazal, ze to niewykonalne: protokol odrzuca wiersze
        powyzej 64 KiB, a pelny korpus to ~430 KB. Wymagamy wiec wlasciwosci,
        ktora faktycznie ma znaczenie -- liczba wywolan jest proporcjonalna do
        ROZMIARU DANYCH i pozostaje o rzedy wielkosci mniejsza od liczby par.
        """
        calls: list[int] = []

        def fake_call(op, args=None, timeout=20.0):
            calls.append(len(args["titles"]))
            return {
                "keys": [
                    base64.b64encode(t.encode("utf-8")).decode("ascii")
                    for t in args["titles"]
                ]
            }

        collation = HostCollation(fake_call)
        collation.load(self.corpus)

        unique = len(dict.fromkeys(self.corpus))
        self.assertEqual(sum(calls), unique, "Wsady musza pokryc wszystkie unikaty")
        self.assertLess(unique, len(self.corpus), "Korpus bez powtorzen? Sprawdz dane.")

        # Istota kontraktu: garsc wywolan, nie tysiace porownan po IPC.
        # Przy ~11 tys. unikatow sortowanie parami to >100 tys. wywolan.
        self.assertLess(
            len(calls),
            50,
            f"Host wolany {len(calls)} razy -- to juz nie jest sortowanie wsadowe",
        )
        self.assertLess(len(calls), unique / 100)

        # Drugie sortowanie NIE pyta hosta ponownie.
        before = len(calls)
        collation.sort(self.corpus[:500])
        self.assertEqual(len(calls), before, "Powtorne sortowanie wolalo hosta")


class ModeContractMatchesOriginalComparers(unittest.TestCase):
    """Tryby kluczy zmierzone wzgledem ORYGINALNYCH komparatorow .NET.

    Kwit ``tests/data/csharp-mode-contract.json`` powstal z sondy
    ``probe-mode-contract``, ktora liczy klucze DOKLADNIE tymi wyrazeniami co
    ``LiteEngineHandlers.CollationKeys``, a wynik porownuje z ORYGINALNYMI
    ``StringComparer.CurrentCultureIgnoreCase``, ``StringComparer.OrdinalIgnoreCase``
    i ``CompareInfo.Compare(IgnoreCase|IgnoreNonSpace)`` -- nie z druga kopia
    tej samej implementacji.

    70 napisow kontrolnych, 4830 par na tryb. Sa tam pary zastepcze (poza BMP),
    tureckie ``ı``/``İ``, znaki skladane kontra gotowe, ligatury i ``ß``.
    """

    CONTRACT = Path(__file__).resolve().parent / "data" / "csharp-mode-contract.json"

    @classmethod
    def setUpClass(cls) -> None:
        cls.payload = json.loads(cls.CONTRACT.read_text(encoding="utf-8"))

    def test_every_mode_reproduces_its_original_comparer(self):
        """Zero niezgodnych par w KAZDYM trybie -- inaczej kwit jest bezwartosciowy."""
        for mode, data in self.payload["modes"].items():
            with self.subTest(mode=mode):
                self.assertGreater(data["pairsChecked"], 4000, "za maly pomiar")
                self.assertEqual(
                    data["mismatches"],
                    0,
                    f"{mode} rozjezdza sie z oryginalnym komparatorem",
                )
        self.assertEqual(self.payload["verdict"], "ZGODNE")

    def test_python_byte_order_equals_dotnet_order_in_every_mode(self):
        """To, co robi Python: uklada po BAJTACH klucza i musi trafic w .NET.

        Tu nie ma zadnej reguly jezykowej po stronie Pythona -- tylko
        ``sort_key_order`` na bajtach z hosta. Jesli ta funkcja rozjedzie sie
        z .NET, test padnie na konkretnym trybie.
        """
        strings = self.payload["strings"]
        for mode, data in self.payload["exported"].items():
            with self.subTest(mode=mode):
                keys = [base64.b64decode(k) for k in data["keys"]]
                ordered = sort_key_order(list(zip(range(len(strings)), keys)))
                self.assertEqual(
                    [index for index, _ in ordered],
                    data["referenceOrder"],
                    f"{mode}: kolejnosc po bajtach != kolejnosc .NET",
                )

    def test_title_mode_is_not_interchangeable_with_the_amc_pl_folder_mode(self):
        """Gdyby tryby byly rownowazne, cala poprawka nie mialaby znaczenia."""
        self.assertFalse(
            self.payload["amcPlKeysEqualTitleKeys"],
            "AMC_PL i TITLE_IGNORE_CASE daja te same klucze -- pomiar jest zepsuty",
        )
        # AMC_PL ma IgnoreNonSpace, wiec "e" i "é" to dla niego REMIS...
        self.assertEqual(self.payload["accentTieInAmcPl"], 0)
        # ...a widok "Wszystkie pliki" stawia "e" PRZED "é".
        self.assertLess(self.payload["accentOrderInTitleIgnoreCase"], 0)

    def test_ordinal_mode_keeps_eszett_after_ss(self):
        """``OrdinalIgnoreCase`` nie rozwija ``ß`` do ``SS`` -- inaczej niz Python."""
        self.assertGreater(
            self.payload["eszettPathOrdinal"],
            0,
            "U+00DF (223) musi byc ZA 'S' (83)",
        )
        # I dowod, ze Python sam by tego nie trafil:
        self.assertEqual("C:\\ß".upper(), "C:\\SS")
        self.assertEqual("C:\\ß".upper(), "C:\\ss".upper())

    def test_measurement_ran_on_the_culture_the_user_actually_has(self):
        """``CurrentCulture`` to kultura WATKU -- pomiar w innej nic nie znaczy."""
        self.assertEqual(self.payload["culture"], "pl-PL")
        self.assertTrue(self.payload["runtime"].startswith("8."))


class ModesDoNotShareOneKeyCache(unittest.TestCase):
    """Cache per tryb. Wspolny slownik mieszalby klucze roznych opcji."""

    def _recording_call(self, label: str):
        def call(op, args=None, timeout=None):
            # Klucz zalezy od TRYBU, tak jak u prawdziwego hosta.
            mode = (args or {}).get("mode")
            return {
                "collation": mode,
                "keys": [
                    base64.b64encode(f"{label}:{mode}:{t}".encode()).decode("ascii")
                    for t in (args or {}).get("titles") or []
                ],
            }

        return call

    def test_same_string_gets_different_keys_per_mode(self):
        collation = HostCollation(self._recording_call("k"))
        collation.load(["e"], mode=collation_module.COLLATION_AMC_PL)
        collation.load(["e"], mode=collation_module.COLLATION_TITLE_IGNORE_CASE)
        self.assertNotEqual(
            collation.key_for("e", mode=collation_module.COLLATION_AMC_PL),
            collation.key_for("e", mode=collation_module.COLLATION_TITLE_IGNORE_CASE),
            "Klucze z roznych trybow nie moga wpadac do jednego worka",
        )

    def test_loading_one_mode_does_not_satisfy_another(self):
        """Doczytanie AMC_PL nie moze udawac, ze mamy juz klucze IgnoreCase."""
        collation = HostCollation(self._recording_call("k"))
        collation.load(["e"], mode=collation_module.COLLATION_AMC_PL)
        self.assertIsNone(
            collation.key_for("e", mode=collation_module.COLLATION_ORDINAL_IGNORE_CASE)
        )

    def test_unknown_mode_is_refused_not_silently_treated_as_amc_pl(self):
        with self.assertRaises(HostCollationUnavailable):
            HostCollation(self._recording_call("k")).load(["a"], mode="WYMYSLONY")

    def test_default_mode_stays_amc_pl_for_existing_callers(self):
        """Stary kod wola ``load(titles)`` bez trybu i musi dostac AMC_PL."""
        seen: list[str | None] = []

        def call(op, args=None, timeout=None):
            seen.append((args or {}).get("mode"))
            return {
                "collation": (args or {}).get("mode"),
                "keys": [
                    base64.b64encode(t.encode()).decode("ascii")
                    for t in (args or {}).get("titles") or []
                ],
            }

        collation = HostCollation(call)
        collation.load(["a"])
        self.assertEqual(seen, [collation_module.COLLATION_AMC_PL])
        self.assertIsNotNone(collation.key_for("a"))

    def test_host_answering_with_a_different_mode_is_refused(self):
        """Stary host zignoruje ``mode`` i odpowie ``AMC_PL``.

        Bez tej kontroli dostalibysmy klucze z ``IgnoreNonSpace`` pod etykieta
        ``IgnoreCase``, czyli dokladnie naprawiany blad, tylko niewidoczny.
        """

        def stale_host(op, args=None, timeout=None):
            return {
                "collation": "AMC_PL",  # ignoruje prosbe o inny tryb
                "keys": [
                    base64.b64encode(t.encode()).decode("ascii")
                    for t in (args or {}).get("titles") or []
                ],
            }

        with self.assertRaises(HostCollationUnavailable) as caught:
            HostCollation(stale_host).load(
                ["e"], mode=collation_module.COLLATION_TITLE_IGNORE_CASE
            )
        self.assertIn("Przebuduj LiteHost", str(caught.exception))

    def test_mode_travels_in_the_request(self):
        """Tryb musi jechac w zadaniu, inaczej host nie ma skad go wziac."""
        sent: list[dict] = []

        def call(op, args=None, timeout=None):
            sent.append(dict(args or {}))
            return {
                "collation": (args or {}).get("mode"),
                "keys": [
                    base64.b64encode(t.encode()).decode("ascii")
                    for t in (args or {}).get("titles") or []
                ],
            }

        HostCollation(call).load(
            ["C:/a"], mode=collation_module.COLLATION_ORDINAL_IGNORE_CASE
        )
        self.assertEqual(
            sent[0]["mode"], collation_module.COLLATION_ORDINAL_IGNORE_CASE
        )
        self.assertEqual(sent[0]["titles"], ["C:/a"])


class PathBatchesAlsoRespectTheLineLimit(unittest.TestCase):
    """Sciezki ida tym samym kanalem i tez musza byc dzielone.

    Sciezki potrafia byc DLUZSZE od tytulow, wiec gdyby limit 64 KiB
    obowiazywal tylko tytuly, widok "Wszystkie pliki" wiesilby sie na duzej
    bibliotece -- host odrzuca wiersz, a odpowiedz bledu nie ma pola ``id``.
    """

    def test_long_paths_are_split_into_requests_under_the_limit(self):
        paths = [f"C:/Muzyka/Katalog {i:05d}/Bardzo dlugi plik {i:05d}.mp3" for i in range(2596)]
        sent: list[list[str]] = []

        def fake_call(op, args=None, timeout=None):
            batch = list((args or {}).get("titles") or [])
            sent.append(batch)
            line = json.dumps(
                {"id": "1", "op": op, "args": {"titles": batch, "mode": "ORDINAL_IGNORE_CASE"}},
                ensure_ascii=False,
            )
            self.assertLessEqual(
                len(line.encode("utf-8")), collation_module.MAX_REQUEST_BYTES
            )
            return {
                "collation": (args or {}).get("mode"),
                "keys": [
                    base64.b64encode(p.encode("utf-8")).decode("ascii") for p in batch
                ],
            }

        collation = HostCollation(fake_call)
        collation.load(paths, mode=collation_module.COLLATION_ORDINAL_IGNORE_CASE)
        self.assertGreater(len(sent), 1, "2596 dlugich sciezek musi sie podzielic")
        self.assertEqual(sum(len(b) for b in sent), len(paths))


class BatchesStayUnderTheProtocolLineLimit(unittest.TestCase):
    """Zmierzone na ZYWYM hoscie: wiersz > 64 KiB konczy sie 'line_too_long'.

    Host celowo pilnuje ``MaximumLineLength = 64 * 1024`` (LiteRequest.cs), a
    odpowiedz bledu nie ma pola ``id``, wiec klient nie dopasowuje jej do
    zadania i czeka do timeoutu. Pelny poziom Biblioteki (2596 tytulow) to
    ~100 KB zadania, czyli PONAD prog. Wsad musi byc dzielony po bajtach
    wiersza, nie po liczbie pozycji.
    """

    def test_full_level_is_split_into_several_requests_each_under_the_limit(self):
        titles = [f"Tytul numer {i:05d} zazolc gesla jaznia" for i in range(2596)]
        sent: list[list[str]] = []

        def fake_call(op, args=None, timeout=None):
            batch = list((args or {}).get("titles") or [])
            sent.append(batch)
            line = json.dumps(
                {"id": "1", "op": op, "args": {"titles": batch}}, ensure_ascii=False
            )
            self.assertLessEqual(
                len(line.encode("utf-8")),
                collation_module.MAX_REQUEST_BYTES,
                f"wiersz zadania {len(line.encode('utf-8'))} B przekracza prog hosta",
            )
            return {"keys": [base64.b64encode(t.encode()).decode() for t in batch]}

        collation = HostCollation(fake_call)
        collation.load(titles)

        self.assertGreater(len(sent), 1, "taki poziom MUSI sie podzielic")
        self.assertEqual(
            sorted(t for batch in sent for t in batch),
            sorted(titles),
            "podzial nie moze zgubic ani zdublowac tytulu",
        )

    def test_limit_is_below_the_hosts_hard_cap(self):
        self.assertLessEqual(collation_module.MAX_REQUEST_BYTES, 64 * 1024)

    def test_single_title_longer_than_the_limit_is_refused_not_sent(self):
        """Lepiej jawny blad niz wiersz, na ktory host nigdy nie odpowie."""
        monster = "x" * (70 * 1024)

        def fake_call(op, args=None, timeout=None):
            raise AssertionError("takiego wiersza nie wolno wyslac")

        with self.assertRaises(HostCollationUnavailable):
            HostCollation(fake_call).load([monster])


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
