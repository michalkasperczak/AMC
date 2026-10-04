#!/usr/bin/env python3
"""Pomiar ZBIORCZEGO widoku zakladek na PELNEJ kopii danych.

Czego dowodzi
-------------
* widok czyta CALA tabele ``bookmarks``, nie tylko sesje ``local``,
* filtr ``IsBookmark`` zgadza sie z policzonym rozkladem ``Purpose``,
* kolejnosc jest IDENTYCZNA z uruchomionym C# -- Id w kolejnosci wyniku ida
  do sondy .NET, ktora liczy wlasna kolejnosc oryginalnym LINQ, i
  porownujemy SHA obu list,
* odczyt NICZEGO nie zmienil: SHA pliku bazy przed i po.

Czego NIE robi: nie uruchamia GUI, nie pisze do profilu i nie zapisuje
tytulow ani sciezek z fixture -- kwit ma same LICZBY i skroty.

Uzycie:
    python3 tools/measure_all_bookmarks.py [--db SCIEZKA] [--out KWIT.json]
    python3 tools/measure_all_bookmarks.py ... --probe SCIEZKA/probe.dll
"""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import os
import subprocess
import sys
import tempfile
import time
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from amc_wx_lite.collation import (  # noqa: E402
    COLLATION_TITLE_IGNORE_CASE,
    HostCollation,
)
from amc_wx_lite.library_activity import all_bookmark_rows  # noqa: E402
from amc_wx_lite.library_db import LibraryDatabase  # noqa: E402

DEFAULT_DB = Path(
    os.environ.get(
        "AMC_WX_FIXTURE",
        "/home/michal/projekty/amc_pomoc/wx-full-profile-after421/fixture",
    )
) / "library.db"


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def sha256_ids(ids: list[str]) -> str:
    return hashlib.sha256("\n".join(ids).encode("utf-8")).hexdigest()


def run_probe(probe: Path, payload: dict) -> dict:
    """Policz kolejnosc wzorcowa URUCHOMIONYM C# (ten sam LINQ co w AMC)."""
    with tempfile.TemporaryDirectory() as tmp:
        src = Path(tmp) / "in.json"
        dst = Path(tmp) / "out.json"
        src.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")
        subprocess.run(
            ["dotnet", str(probe), str(src), str(dst)],
            check=True,
            capture_output=True,
        )
        return json.loads(dst.read_text(encoding="utf-8"))


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--db", type=Path, default=DEFAULT_DB)
    parser.add_argument("--out", type=Path)
    parser.add_argument(
        "--probe",
        type=Path,
        help="probe.dll sondy display-order; bez niej mierzymy tylko wlasny przebieg",
    )
    parser.add_argument("--current-session", default="local")
    parser.add_argument("--current-item", default="")
    args = parser.parse_args(argv)

    if not args.db.exists():
        print(f"Brak bazy {args.db}", file=sys.stderr)
        return 2

    before = sha256_file(args.db)

    db = LibraryDatabase(args.db)
    try:
        total = db.connection.execute("SELECT COUNT(*) FROM bookmarks").fetchone()[0]
        purpose = Counter(
            {
                int(row[0]): int(row[1])
                for row in db.connection.execute(
                    "SELECT purpose, COUNT(*) FROM bookmarks GROUP BY purpose"
                )
            }
        )
        sessions = {
            f"{row[0]}|{row[1]}": int(row[2])
            for row in db.connection.execute(
                "SELECT session_id, session_name, COUNT(*) FROM bookmarks "
                "WHERE (purpose & 1) != 0 GROUP BY session_id, session_name"
            )
        }

        # Wszystkie wpisy z bitem Bookmark -- potrzebne i do kolatora, i do sondy.
        raw = [
            dict(
                id=str(row["id"]),
                sessionId=str(row["session_id"]),
                sessionName=str(row["session_name"]),
                itemId=str(row["item_id"]),
                itemTitle=str(row["item_title"]),
                positionTicks=int(row["position_ticks"]),
                createdUtcTicks=int(row["created_utc_ticks"]),
                purpose=int(row["purpose"]),
            )
            for row in db.connection.execute(
                "SELECT id, session_id, session_name, item_id, item_title, "
                "position_ticks, created_utc_ticks, purpose FROM bookmarks "
                "WHERE (purpose & 1) != 0 ORDER BY ordinal"
            )
        ]

        probe_result: dict | None = None
        collation: HostCollation | None = None
        if args.probe:
            # Sonda oddaje OBOJE: kolejnosc wzorcowa i klucze kolacji .NET.
            # Dzieki temu pomiar nie potrzebuje zywego LiteHosta, a klucze i
            # tak pochodza z prawdziwego CompareInfo, nie z Pythona.
            probe_result = run_probe(
                args.probe,
                {
                    "entries": raw,
                    "contexts": [
                        {
                            "label": "pomiar",
                            "currentSessionId": args.current_session,
                            "currentItemId": args.current_item,
                        }
                    ],
                },
            )
            collation = HostCollation.from_payload(
                {
                    "titles": probe_result["texts"],
                    "keys": probe_result["textKeys"],
                    "mode": COLLATION_TITLE_IGNORE_CASE,
                }
            )

        started = time.perf_counter()
        result = all_bookmark_rows(
            db,
            current_session_id=args.current_session,
            current_item_id=args.current_item,
            collation=collation,
        )
        elapsed_ms = (time.perf_counter() - started) * 1000

        ids = [b.bookmark_id for b in result.bookmarks]
        current = sum(1 for d in result.display if d.is_current_item)
        playable = sum(1 for d in result.display if d.can_play_locally)
    finally:
        db.close()

    after = sha256_file(args.db)

    receipt = {
        "schema": "amc-wx-all-bookmarks/measure/1",
        "dbSha256Before": before,
        "dbSha256After": after,
        "readOnly": before == after,
        "bookmarksTableRows": total,
        "purposeHistogram": {str(k): v for k, v in sorted(purpose.items())},
        "bookmarkBitRows": sum(v for k, v in purpose.items() if k & 1),
        "sessionsWithBookmarkBit": sessions,
        "distinctItemIds": len({e["itemId"] for e in raw}),
        "returnedRows": len(result.rows),
        "returnedBookmarks": len(ids),
        "currentItemRows": current,
        "playableLocallyRows": playable,
        "orderMatchesAmc": result.order_matches_amc,
        "seesLiveWrites": result.sees_live_writes,
        "heading": result.heading,
        "orderSha256": sha256_ids(ids),
        "elapsedMs": round(elapsed_ms, 2),
        "idsAreUnique": len(set(ids)) == len(ids),
        # Dowod, ze NIE zgubilismy ani nie dorobilismy zadnego Id: zbior
        # wynikowy musi byc dokladnie zbiorem wejsciowym.
        "idSetEqualsSource": set(ids) == {e["id"] for e in raw},
    }

    if probe_result is not None:
        csharp = probe_result["contexts"][0]["order"]
        receipt["csharpProbe"] = {
            "runtime": probe_result["runtime"],
            "culture": probe_result["culture"],
            "entries": probe_result["entries"],
            "bookmarkEntries": probe_result["bookmarkEntries"],
            "orderSha256": probe_result["contexts"][0]["orderSha256"],
            "currentItemBookmarks": probe_result["contexts"][0]["currentItemBookmarks"],
            "titleKeyPairMismatches": probe_result[
                "titleKeyPairMismatchesVsCurrentCultureIgnoreCase"
            ],
            "ordinalUtf16BePairMismatches": probe_result[
                "ordinalUtf16BePairMismatchesVsOrdinal"
            ],
            "ordinalUtf8PairMismatches": probe_result[
                "ordinalUtf8PairMismatchesVsOrdinal"
            ],
            "ordinalIgnoreCaseHostPairMismatches": probe_result[
                "ordinalIgnoreCaseHostPairMismatchesVsOrdinal"
            ],
        }
        receipt["orderEqualsCsharp"] = ids == csharp
        # Pierwsza rozbieznosc, jesli jest -- bez tego "False" nic nie mowi.
        receipt["firstDivergenceIndex"] = next(
            (i for i, (a, b) in enumerate(zip(ids, csharp)) if a != b), None
        )

    text = json.dumps(receipt, ensure_ascii=False, indent=1)
    if args.out:
        args.out.write_text(text, encoding="utf-8")
    print(text)
    ok = receipt["readOnly"] and receipt["idSetEqualsSource"]
    if "orderEqualsCsharp" in receipt:
        ok = ok and receipt["orderEqualsCsharp"]
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
